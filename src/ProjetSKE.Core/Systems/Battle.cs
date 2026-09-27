using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;

namespace ProjetSKE.Core.Systems;

public enum BattleOutcome { Ongoing, Victory, Defeat, Fled }

/// <summary>Un participant au combat (personnage de l'équipe ou monstre).</summary>
public sealed class Combatant
{
    public required string Name { get; init; }
    public bool IsAlly { get; init; }
    public CharacterState? Character { get; init; }
    public MonsterDef? Monster { get; init; }
    public required StatBlock Stats { get; init; }
    public required IReadOnlyList<SkillDef> Skills { get; init; }
    public int Hp { get; set; }
    public int Mana { get; set; }

    public bool IsAlive => Hp > 0;
    public bool IsBoss => Monster?.IsBoss == true;
}

/// <summary>
/// Combat au tour par tour. L'ordre de jeu dépend de la vitesse.
/// Les ennemis jouent automatiquement ; le combat s'arrête dès que c'est au tour d'un personnage du joueur.
/// </summary>
public sealed class Battle
{
    private readonly GameSession _session;
    private readonly Queue<Combatant> _queue = new();

    public IReadOnlyList<Combatant> Allies { get; }
    public IReadOnlyList<Combatant> Enemies { get; }
    public string? FixedBattleId { get; }
    public List<string> Log { get; } = [];
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
    public int Round { get; private set; }
    public Combatant? CurrentActor { get; private set; }

    public bool IsPlayerTurn => Outcome == BattleOutcome.Ongoing && CurrentActor is { IsAlly: true };
    public bool HasBoss => Enemies.Any(e => e.IsBoss);
    public bool CanFlee => _session.Config.Flee != FleeRule.Never && !HasBoss;

    internal Battle(GameSession session, IReadOnlyList<string> monsterIds, string? fixedBattleId)
    {
        _session = session;
        FixedBattleId = fixedBattleId;

        Allies = session.ActiveParty.Select(c =>
        {
            var stats = session.GetStats(c);
            return new Combatant
            {
                Name = session.DefOf(c).Name,
                IsAlly = true,
                Character = c,
                Stats = stats,
                Skills = session.GetSkills(c),
                Hp = Math.Min(c.CurrentHp, stats.MaxHp),
                Mana = Math.Min(c.CurrentMana, stats.MaxMana),
            };
        }).ToList();

        var defs = monsterIds.Select(id => session.Db.Monsters[id]).ToList();
        var enemies = new List<Combatant>();
        foreach (var def in defs)
        {
            session.DiscoverMonster(def.Id);
            // "Loup gris A", "Loup gris B" quand un même monstre apparaît plusieurs fois.
            var name = def.Name;
            if (defs.Count(d => d.Id == def.Id) > 1)
                name += " " + (char)('A' + enemies.Count(e => e.Monster!.Id == def.Id));
            enemies.Add(new Combatant
            {
                Name = name,
                Monster = def,
                Stats = def.Stats,
                Skills = def.SkillIds.Select(id => session.Db.Skills[id]).ToList(),
                Hp = def.Stats.MaxHp,
                Mana = def.Stats.MaxMana,
            });
        }
        Enemies = enemies;

        Log.Add($"Combat ! {string.Join(", ", Enemies.Select(e => e.Name))}");
        CheckEnd();
        NextTurn();
    }

    private IEnumerable<Combatant> All => Allies.Concat(Enemies);

    // ------------------------------------------------------------------ Actions du joueur

    public static bool NeedsTarget(SkillDef skill) => skill.Target is SkillTarget.SingleEnemy or SkillTarget.SingleAlly;

    public bool CanUse(SkillDef skill) => IsPlayerTurn && CurrentActor!.Mana >= skill.ManaCost;

    /// <summary>Cibles possibles pour une compétence à cible unique.</summary>
    public IReadOnlyList<Combatant> TargetsFor(SkillDef skill)
    {
        if (CurrentActor is null) return [];
        var foes = CurrentActor.IsAlly ? Enemies : Allies;
        var friends = CurrentActor.IsAlly ? Allies : Enemies;
        return skill.Target switch
        {
            SkillTarget.SingleEnemy => foes.Where(c => c.IsAlive).ToList(),
            SkillTarget.SingleAlly => friends.Where(c => c.IsAlive).ToList(),
            _ => [],
        };
    }

    public bool UseSkill(SkillDef skill, Combatant? target = null)
    {
        if (!CanUse(skill)) return false;
        var actor = CurrentActor!;
        var targets = ResolveTargets(actor, skill, target);
        if (targets.Count == 0) return false;
        Perform(actor, skill, targets);
        EndAction();
        return true;
    }

    public bool UseItem(ItemDef item, Combatant target)
    {
        if (!IsPlayerTurn || !item.IsConsumable || !target.IsAlly || !target.IsAlive) return false;
        if (!_session.RemoveItem(item.Id)) return false;
        var hp = Math.Min(item.HealHp, target.Stats.MaxHp - target.Hp);
        var mp = Math.Min(item.HealMana, target.Stats.MaxMana - target.Mana);
        target.Hp += hp;
        target.Mana += mp;
        var gains = new List<string>();
        if (hp > 0) gains.Add($"+{hp} PV");
        if (mp > 0) gains.Add($"+{mp} PM");
        Log.Add($"{CurrentActor!.Name} utilise {item.Name} sur {target.Name}{(gains.Count > 0 ? " : " + string.Join(" ", gains) : "")}.");
        EndAction();
        return true;
    }

    public double FleeChance()
    {
        if (!CanFlee) return 0;
        if (_session.Config.Flee == FleeRule.AlwaysSucceed) return 1;
        var allySpeed = Allies.Where(a => a.IsAlive).Average(a => a.Stats.Speed);
        var enemySpeed = Enemies.Where(e => e.IsAlive).Average(e => e.Stats.Speed);
        return Math.Clamp(0.5 + (allySpeed - enemySpeed) * 0.03, 0.1, 0.95);
    }

    /// <summary>Tente de fuir. Renvoie false si la fuite est impossible (le tour n'est pas consommé).</summary>
    public bool TryFlee()
    {
        if (!IsPlayerTurn || !CanFlee) return false;
        if (_session.Rng.NextDouble() < FleeChance())
        {
            Log.Add("L'équipe prend la fuite !");
            Finish(BattleOutcome.Fled);
        }
        else
        {
            Log.Add("La fuite a échoué !");
            EndAction();
        }
        return true;
    }

    // ------------------------------------------------------------------ Déroulement

    private void EndAction()
    {
        CheckEnd();
        NextTurn();
    }

    private void NextTurn()
    {
        while (Outcome == BattleOutcome.Ongoing)
        {
            if (_queue.Count == 0)
            {
                Round++;
                foreach (var c in All.Where(c => c.IsAlive).OrderByDescending(c => c.Stats.Speed).ThenBy(c => c.IsAlly ? 0 : 1))
                    _queue.Enqueue(c);
            }
            var next = _queue.Dequeue();
            if (!next.IsAlive) continue;
            CurrentActor = next;
            if (next.IsAlly) return;
            EnemyAct(next);
            CheckEnd();
        }
    }

    private void EnemyAct(Combatant enemy)
    {
        var usable = enemy.Skills.Where(s => enemy.Mana >= s.ManaCost).ToList();
        if (usable.Count == 0)
        {
            Log.Add($"{enemy.Name} hésite.");
            return;
        }
        var skill = usable[_session.Rng.Next(usable.Count)];
        var candidates = TargetsFor(skill);
        var target = candidates.Count > 0 ? candidates[_session.Rng.Next(candidates.Count)] : null;
        var targets = ResolveTargets(enemy, skill, target);
        if (targets.Count > 0) Perform(enemy, skill, targets);
    }

    private List<Combatant> ResolveTargets(Combatant actor, SkillDef skill, Combatant? target)
    {
        var foes = actor.IsAlly ? Enemies : Allies;
        var friends = actor.IsAlly ? Allies : Enemies;
        IEnumerable<Combatant> pool = skill.Target switch
        {
            SkillTarget.SingleEnemy or SkillTarget.AllEnemies => foes,
            SkillTarget.SingleAlly or SkillTarget.AllAllies => friends,
            _ => new[] { actor },
        };
        var alive = pool.Where(c => c.IsAlive).ToList();
        if (!NeedsTarget(skill)) return alive;
        return target is not null && alive.Contains(target) ? new List<Combatant> { target } : new List<Combatant>();
    }

    private void Perform(Combatant actor, SkillDef skill, List<Combatant> targets)
    {
        actor.Mana -= skill.ManaCost;
        var parts = new List<string>();
        foreach (var t in targets)
        {
            var variance = 0.9 + _session.Rng.NextDouble() * 0.2;
            if (skill.Kind == SkillKind.Heal)
            {
                var amount = Math.Min((int)(actor.Stats.Magic * skill.Power * 2 * variance) + 5, t.Stats.MaxHp - t.Hp);
                t.Hp += amount;
                parts.Add($"{t.Name} +{amount} PV");
            }
            else
            {
                var raw = skill.Kind == SkillKind.Physical
                    ? actor.Stats.Attack * skill.Power - t.Stats.Defense / 2.0
                    : actor.Stats.Magic * skill.Power * 1.2 - t.Stats.Defense / 4.0;
                var damage = Math.Max(1, (int)Math.Round(raw * variance));
                t.Hp = Math.Max(0, t.Hp - damage);
                parts.Add(t.IsAlive ? $"{t.Name} -{damage} PV" : $"{t.Name} -{damage} PV, vaincu !");
            }
        }
        Log.Add($"{actor.Name} : {skill.Name} → {string.Join(", ", parts)}");
    }

    private void CheckEnd()
    {
        if (Outcome != BattleOutcome.Ongoing) return;
        if (Enemies.All(e => !e.IsAlive))
        {
            Log.Add("Victoire !");
            Finish(BattleOutcome.Victory);
        }
        else if (Allies.All(a => !a.IsAlive))
        {
            Log.Add("Défaite...");
            Finish(BattleOutcome.Defeat);
        }
    }

    private void Finish(BattleOutcome outcome)
    {
        Outcome = outcome;
        CurrentActor = null;
        // Reporte PV et PM sur les personnages sauvegardés.
        foreach (var a in Allies)
        {
            a.Character!.CurrentHp = Math.Max(0, a.Hp);
            a.Character.CurrentMana = Math.Max(0, a.Mana);
        }
    }
}
