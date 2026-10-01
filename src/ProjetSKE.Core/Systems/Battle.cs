using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;

namespace ProjetSKE.Core.Systems;

public enum BattleOutcome { Ongoing, Victory, Defeat, Fled }

/// <summary>Effet durable actif sur un combattant (poison, bonus, étourdissement...).</summary>
public sealed class ActiveEffect
{
    public required SkillEffect Def { get; init; }
    public required string Source { get; init; }
    public int TurnsLeft { get; set; }

    public bool IsNegative => Def.Type is EffectType.Poison or EffectType.Stun or EffectType.StatDown
            or EffectType.Burn or EffectType.Paralysis or EffectType.Suffocation
        || (Def.Type == EffectType.Element && Def.Amount > 100); // une faiblesse est un effet négatif (purifiable)

    /// <summary>Élément d'un effet élémentaire (embrasé = feu, paralysé = foudre, suffoqué = air), sinon null.</summary>
    public static string? ElementOf(EffectType type) => type switch
    {
        EffectType.Burn => "feu",
        EffectType.Paralysis => "foudre",
        EffectType.Suffocation => "air",
        _ => null,
    };
}

/// <summary>Un participant au combat (personnage de l'équipe ou monstre).</summary>
public sealed class Combatant
{
    public required string Name { get; init; }
    public bool IsAlly { get; init; }
    public CharacterState? Character { get; init; }
    public MonsterDef? Monster { get; init; }
    public required StatBlock Stats { get; set; }
    /// <summary>Stats sans les passifs (équipement compris) : base du recalcul quand un passif s'active ou s'arrête.</summary>
    public StatBlock? RawStats { get; init; }
    public required IReadOnlyList<SkillDef> Skills { get; init; }
    public int Hp { get; set; }
    public int Mana { get; set; }
    /// <summary>En garde : dégâts reçus réduits jusqu'à son prochain tour.</summary>
    public bool Defending { get; set; }
    /// <summary>Répliques de combat de ce participant.</summary>
    public IReadOnlyList<BattleLine> Lines { get; init; } = [];
    /// <summary>Faiblesses et résistances aux éléments.</summary>
    public IReadOnlyList<ElementModifier> Resistances { get; set; } = [];
    /// <summary>Résistances de sa fiche (sans les passifs).</summary>
    public IReadOnlyList<ElementModifier> BaseResistances { get; init; } = [];
    /// <summary>Passifs qui agissent pendant ce combat (personnages de l'équipe).</summary>
    public IReadOnlyList<PassiveDef> Passives { get; set; } = [];
    /// <summary>Tous ses passifs, même ceux dont les conditions ne passaient pas au début (bonus de dégâts, vérifiés à chaque coup).</summary>
    public IReadOnlyList<PassiveDef> AllPassives { get; init; } = [];
    /// <summary>Identifiant du PJ, du monstre ou du PNJ.</summary>
    public string Id => Character?.DefId ?? Monster?.Id ?? Name;
    public int Level => Character?.Level ?? Monster?.Level ?? 1;
    public List<ActiveEffect> Effects { get; } = [];
    /// <summary>Tours restants avant de pouvoir réutiliser une compétence (id → tours).</summary>
    public Dictionary<string, int> Cooldowns { get; } = [];
    /// <summary>Points de dégâts encore absorbés par un bouclier.</summary>
    public int Shield { get; set; }

    public bool IsAlive => Hp > 0;
    public bool IsBoss => Monster?.IsBoss == true;
    public bool IsStunned => Effects.Any(e => e.Def.Type == EffectType.Stun);
    /// <summary>Paralysé : plus d'actions physiques.</summary>
    public bool IsParalyzed => Effects.Any(e => e.Def.Type == EffectType.Paralysis);

    /// <summary>Statistique avec bonus et malus en cours (en %).</summary>
    public int Stat(StatKind kind)
    {
        var baseValue = kind switch
        {
            StatKind.Attack => Stats.Attack,
            StatKind.Defense => Stats.Defense,
            StatKind.Magic => Stats.Magic,
            _ => Stats.Speed,
        };
        var percent = 100
            + Effects.Where(e => e.Def.Type == EffectType.StatUp && e.Def.Stat == kind).Sum(e => e.Def.Amount)
            - Effects.Where(e => e.Def.Type == EffectType.StatDown && e.Def.Stat == kind).Sum(e => e.Def.Amount);
        return Math.Max(kind == StatKind.Defense ? 0 : 1, baseValue * Math.Max(0, percent) / 100);
    }

    public int CooldownOf(SkillDef skill) => Cooldowns.GetValueOrDefault(skill.Id);

    /// <summary>Résumé des effets en cours, pour l'affichage (« Poison 2 · ATQ +20 % (3) »).</summary>
    public string StatusText
    {
        get
        {
            var parts = Effects.Select(Describe).Where(t => t.Length > 0).ToList();
            if (Shield > 0) parts.Add($"Bouclier {Shield}");
            return string.Join(" · ", parts);
        }
    }

    public static string StatName(StatKind s) => s switch
    {
        StatKind.Attack => "ATQ",
        StatKind.Defense => "DEF",
        StatKind.Magic => "MAG",
        _ => "VIT",
    };

    private static string Describe(ActiveEffect e) => e.Def.Type switch
    {
        EffectType.Poison => $"Poison {e.TurnsLeft}",
        EffectType.Regen => $"Régén. {e.TurnsLeft}",
        EffectType.Stun => "Étourdi",
        EffectType.StatUp => $"{StatName(e.Def.Stat)} +{e.Def.Amount} % ({e.TurnsLeft})",
        EffectType.StatDown => $"{StatName(e.Def.Stat)} -{e.Def.Amount} % ({e.TurnsLeft})",
        EffectType.Element => $"{Battle.ElementText(e.Def)} ({e.TurnsLeft})",
        EffectType.Burn => $"Embrasé {e.TurnsLeft}",
        EffectType.Paralysis => $"Paralysé {e.TurnsLeft}",
        EffectType.Suffocation => $"Suffoqué {e.TurnsLeft}",
        _ => "",
    };
}

/// <summary>
/// Combat au tour par tour. L'ordre de jeu dépend de la vitesse (bonus compris).
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
    /// <summary>Index des lignes du journal qui sont des répliques (affichées comme des paroles).</summary>
    public HashSet<int> SpeechLines { get; } = [];
    private readonly HashSet<(Combatant?, BattleLine)> _said = [];
    private readonly IReadOnlyList<BattleLine> _fixedLines = [];
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
    public int Round { get; private set; }
    public Combatant? CurrentActor { get; private set; }

    public bool IsPlayerTurn => Outcome == BattleOutcome.Ongoing && CurrentActor is { IsAlly: true };
    public bool HasBoss => Enemies.Any(e => e.IsBoss);
    public bool CanFlee => IsTraining || (_session.Config.Flee != FleeRule.Never && !HasBoss);

    /// <summary>
    /// Combat d'entraînement : sans risque. PV et PM reviennent à leur état d'avant, pas de défaite (ni game over),
    /// pas d'or ni de butin, XP réduite (réglage d'équilibrage), et on peut toujours arrêter.
    /// </summary>
    public bool IsTraining { get; init; }

    /// <summary>PV et PM de l'équipe avant un entraînement (rendus à la fin).</summary>
    internal List<(CharacterState Character, int Hp, int Mana)> Before { get; } = [];

    internal Battle(GameSession session, IReadOnlyList<string> monsterIds, string? fixedBattleId)
    {
        _session = session;
        FixedBattleId = fixedBattleId;

        Allies = session.ActiveParty.Select(c =>
        {
            var stats = session.GetStats(c);
            var def = session.DefOf(c);
            var passives = session.ActivePassives(c);
            return new Combatant
            {
                Name = def.Name,
                IsAlly = true,
                Character = c,
                Stats = stats,
                Skills = session.GetSkills(c),
                Hp = Math.Min(c.CurrentHp, stats.MaxHp),
                Mana = Math.Min(c.CurrentMana, stats.MaxMana),
                Lines = def.BattleLines,
                // Les résistances des passifs passent avant celles de la fiche (la première trouvée compte).
                Resistances = [.. passives.SelectMany(p => p.Resistances), .. def.Resistances],
                BaseResistances = def.Resistances,
                RawStats = session.StatsWithoutPassives(c),
                Passives = passives,
                AllPassives = session.PassivesOf(c),
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
            // Passifs du monstre ou du PNJ : stats, résistances, effets en combat (comme pour les PJ).
            var passives = session.ActivePassives(def.Id, def.PassiveIds);
            var stats = passives.Count > 0 ? GameSession.WithPassives(def.Stats, passives) : def.Stats;
            enemies.Add(new Combatant
            {
                Name = name,
                Monster = def,
                Stats = stats,
                Skills = def.SkillIds.Where(session.Db.Skills.ContainsKey).Select(id => session.Db.Skills[id]).ToList(),
                Hp = stats.MaxHp,
                Mana = stats.MaxMana,
                Lines = def.BattleLines,
                Resistances = [.. passives.SelectMany(p => p.Resistances), .. def.Resistances],
                BaseResistances = def.Resistances,
                RawStats = def.Stats,
                Passives = passives,
                AllPassives = def.PassiveIds.Where(session.Db.Passives.ContainsKey).Select(id => session.Db.Passives[id]).ToList(),
            });
        }
        Enemies = enemies;

        if (fixedBattleId is not null)
            _fixedLines = session.Db.Content.Locations.Select(l => l.FixedBattle).FirstOrDefault(f => f?.Id == fixedBattleId)?.BattleLines ?? [];

        Log.Add(Enemies.Count > 0 ? $"Combat ! {string.Join(", ", Enemies.Select(e => e.Name))}" : "Aucun ennemi.");
        // Passifs : effets posés sur le porteur au début du combat.
        foreach (var fighter in All)
            foreach (var p in fighter.Passives)
                foreach (var effect in p.BattleStart)
                    Log.Add($"{p.Name} : {ApplyEffect(fighter, Resolve(effect, fighter, fighter), "passif:" + p.Id)}");
        SayAll(BattleTrigger.Start);
        CheckEnd();
        NextTurn();
    }

    private IEnumerable<Combatant> All => Allies.Concat(Enemies);

    // ------------------------------------------------------------------ Actions du joueur

    public static bool NeedsTarget(SkillDef skill) => skill.Target is SkillTarget.SingleEnemy or SkillTarget.SingleAlly;

    /// <summary>Le combattant peut payer la compétence (PM, PV) et elle n'est pas en recharge.</summary>
    public static bool CanAfford(Combatant c, SkillDef skill) =>
        c.Mana >= skill.ManaCost && (skill.HpCost <= 0 || c.Hp > skill.HpCost) && c.CooldownOf(skill) == 0
        && !(c.IsParalyzed && skill.Kind == SkillKind.Physical); // paralysé : plus d'actions physiques

    public bool CanUse(SkillDef skill) => IsPlayerTurn && CanAfford(CurrentActor!, skill);

    /// <summary>Cibles possibles pour une compétence à cible unique (résurrection : les alliés K.O.).</summary>
    public IReadOnlyList<Combatant> TargetsFor(SkillDef skill) => CurrentActor is null ? [] : TargetsFor(CurrentActor, skill);

    private IReadOnlyList<Combatant> TargetsFor(Combatant actor, SkillDef skill)
    {
        var foes = actor.IsAlly ? Enemies : Allies;
        var friends = actor.IsAlly ? Allies : Enemies;
        return skill.Target switch
        {
            SkillTarget.SingleEnemy => foes.Where(c => c.IsAlive).ToList(),
            SkillTarget.SingleAlly => friends.Where(c => skill.Kind == SkillKind.Revive ? !c.IsAlive : c.IsAlive).ToList(),
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

    /// <summary>Se mettre en garde : les dégâts reçus sont divisés par deux jusqu'au prochain tour.</summary>
    public void Defend()
    {
        if (!IsPlayerTurn) return;
        CurrentActor!.Defending = true;
        Log.Add($"{CurrentActor.Name} se met en garde.");
        EndAction();
    }

    /// <summary>Passer son tour.</summary>
    public void Wait()
    {
        if (!IsPlayerTurn) return;
        Log.Add($"{CurrentActor!.Name} attend.");
        EndAction();
    }

    public double FleeChance()
    {
        if (!CanFlee) return 0;
        if (_session.Config.Flee == FleeRule.AlwaysSucceed) return 1;
        var allySpeed = Allies.Where(a => a.IsAlive).Average(a => a.Stat(StatKind.Speed));
        var enemySpeed = Enemies.Where(e => e.IsAlive).Average(e => e.Stat(StatKind.Speed));
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
                SayAll(BattleTrigger.Turn, Round);
                foreach (var c in All.Where(c => c.IsAlive).OrderByDescending(c => c.Stat(StatKind.Speed)).ThenBy(c => c.IsAlly ? 0 : 1))
                    _queue.Enqueue(c);
            }
            var next = _queue.Dequeue();
            if (!next.IsAlive) continue;
            CurrentActor = next;
            next.Defending = false;
            if (!StartOfTurn(next))
            {
                CheckEnd();
                continue;
            }
            if (next.IsAlly) return;
            EnemyAct(next);
            CheckEnd();
        }
    }

    /// <summary>Début du tour d'un combattant : recharges, poison, régénération, étourdissement. Renvoie false s'il ne joue pas.</summary>
    private bool StartOfTurn(Combatant c)
    {
        foreach (var key in c.Cooldowns.Keys.ToList())
            if (--c.Cooldowns[key] <= 0) c.Cooldowns.Remove(key);
        RefreshPassives();
        TurnStartTriggers(c);

        // Passifs : PV / PM rendus (ou perdus) à chaque tour du porteur.
        foreach (var p in c.Passives.Where(p => p.HpPerTurn != 0 || p.ManaPerTurn != 0))
        {
            var hp = c.Hp;
            c.Hp = Math.Clamp(c.Hp + p.HpPerTurn, 0, c.Stats.MaxHp);
            c.Mana = Math.Clamp(c.Mana + p.ManaPerTurn, 0, c.Stats.MaxMana);
            if (c.Hp != hp) Log.Add($"{p.Name} : {c.Name} {(c.Hp > hp ? "+" : "")}{c.Hp - hp} PV");
        }

        var stunned = c.IsStunned;
        foreach (var e in c.Effects.ToList())
        {
            switch (e.Def.Type)
            {
                case EffectType.Poison:
                    var before = c.Hp;
                    c.Hp = Math.Max(0, c.Hp - Math.Max(1, e.Def.Amount));
                    Log.Add($"{c.Name} souffre du poison : -{before - c.Hp} PV{(c.IsAlive ? "" : ", vaincu !")}");
                    break;
                case EffectType.Burn or EffectType.Suffocation:
                {
                    // Dégâts élémentaires par tour : faiblesse ×, résistance ÷, absorption = soin.
                    var factor = ElementFactor(ActiveEffect.ElementOf(e.Def.Type)!, c);
                    var amount = (int)Math.Round(Math.Max(1, e.Def.Amount) * factor);
                    var what = e.Def.Type == EffectType.Burn ? "brûle" : "suffoque";
                    if (amount >= 0)
                    {
                        var hpBefore = c.Hp;
                        c.Hp = Math.Max(0, c.Hp - amount);
                        Log.Add($"{c.Name} {what} : -{hpBefore - c.Hp} PV{(c.IsAlive ? "" : ", vaincu !")}");
                    }
                    else
                    {
                        var gain = Math.Min(-amount, c.Stats.MaxHp - c.Hp);
                        c.Hp += gain;
                        if (gain > 0) Log.Add($"{c.Name} absorbe : +{gain} PV");
                    }
                    break;
                }
                case EffectType.Regen:
                    var healed = Math.Min(e.Def.Amount, c.Stats.MaxHp - c.Hp);
                    c.Hp += healed;
                    if (healed > 0) Log.Add($"{c.Name} se régénère : +{healed} PV");
                    break;
            }
            if (--e.TurnsLeft <= 0)
            {
                c.Effects.Remove(e);
                if (e.Def.Type == EffectType.Shield) c.Shield = 0;
            }
        }
        if (!c.IsAlive)
        {
            Say(c, BattleTrigger.Down);
            return false;
        }
        if (stunned)
        {
            Log.Add($"{c.Name} est étourdi et perd son tour.");
            return false;
        }
        return true;
    }

    /// <summary>L'ennemi choisit une compétence utile au hasard (pas de soin si personne n'est blessé...).</summary>
    private void EnemyAct(Combatant enemy)
    {
        var friends = Enemies;
        var usable = enemy.Skills.Where(s => CanAfford(enemy, s)).Where(s => s.Kind switch
        {
            SkillKind.Heal => friends.Any(f => f.IsAlive && f.Hp < f.Stats.MaxHp),
            SkillKind.Revive => friends.Any(f => !f.IsAlive),
            _ => true,
        }).ToList();
        if (usable.Count == 0)
        {
            Log.Add($"{enemy.Name} hésite.");
            return;
        }
        var skill = usable[_session.Rng.Next(usable.Count)];
        var candidates = TargetsFor(enemy, skill);
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
        var valid = pool.Where(c => skill.Kind == SkillKind.Revive ? !c.IsAlive : c.IsAlive).ToList();
        if (!NeedsTarget(skill)) return valid;
        return target is not null && valid.Contains(target) ? [target] : [];
    }

    /// <summary>Multiplicateur d'élément de la cible (1 = normal).</summary>
    private static double ElementFactor(SkillDef skill, Combatant target) => ElementFactor(skill.Element, target);

    /// <summary>Multiplicateur d'un élément sur une cible (1 = normal, 2 = faiblesse, 0 = immunité, négatif = absorbe).</summary>
    public static double ElementFactor(string element, Combatant target)
    {
        if (element.Trim().Length == 0) return 1;
        // Un effet en cours (faiblesse / résistance temporaire) passe avant la fiche ; le plus récent compte.
        var temporary = target.Effects.LastOrDefault(e => e.Def.Type == EffectType.Element
            && string.Equals(e.Def.Element.Trim(), element.Trim(), StringComparison.OrdinalIgnoreCase));
        if (temporary is not null) return temporary.Def.Amount / 100.0;
        var mod = target.Resistances.FirstOrDefault(r => string.Equals(r.Element.Trim(), element.Trim(), StringComparison.OrdinalIgnoreCase));
        return mod is null ? 1 : mod.Percent / 100.0;
    }

    private void Perform(Combatant actor, SkillDef skill, List<Combatant> targets)
    {
        var balance = _session.Balance;
        actor.Mana -= skill.ManaCost;
        if (skill.HpCost > 0) actor.Hp = Math.Max(1, actor.Hp - skill.HpCost);
        if (skill.Cooldown > 0) actor.Cooldowns[skill.Id] = skill.Cooldown + 1;

        var parts = new List<string>();
        var hits = new List<(Combatant Target, int Before)>();
        var spread = Math.Clamp(balance.DamageVariancePercent, 0, 100) / 100.0;
        double Variance() => 1 - spread + _session.Rng.NextDouble() * spread * 2;
        var offensive = skill.Kind is SkillKind.Physical or SkillKind.Magical;

        foreach (var t in targets)
        {
            var before = t.Hp;
            switch (skill.Kind)
            {
                case SkillKind.Heal:
                {
                    var heal = (int)(actor.Stat(StatKind.Magic) * skill.Power * balance.HealMultiplier * Variance()) + balance.HealFlat + skill.FlatAmount
                        + Scaled(skill.Scalings, actor, t);
                    var amount = Math.Max(0, Math.Min(heal, t.Stats.MaxHp - t.Hp));
                    t.Hp += amount;
                    parts.Add($"{t.Name} +{amount} PV");
                    break;
                }
                case SkillKind.Revive:
                {
                    var heal = (int)(actor.Stat(StatKind.Magic) * skill.Power * balance.HealMultiplier) + skill.FlatAmount + Scaled(skill.Scalings, actor, t);
                    t.Hp = Math.Clamp(heal, 1, t.Stats.MaxHp);
                    t.Effects.Clear();
                    parts.Add($"{t.Name} se relève avec {t.Hp} PV");
                    break;
                }
                case SkillKind.Status:
                    break;
                default:
                {
                    var total = 0;
                    var notes = new List<string>();
                    var factor = ElementFactor(skill, t);
                    if (factor > 1) notes.Add("faiblesse !");
                    else if (factor is > 0 and < 1) notes.Add("résiste");
                    else if (factor == 0) notes.Add("immunisé");
                    for (var h = 0; h < Math.Max(1, skill.Hits) && t.IsAlive; h++)
                    {
                        if (skill.Accuracy < 100 && _session.Rng.Next(100) >= skill.Accuracy)
                        {
                            notes.Add("raté");
                            continue;
                        }
                        // Pénétration : % de la DEF ignoré, % des dégâts qui passe à travers le bouclier (compétence + passifs).
                        var (passiveArmor, passiveShield) = _session.PassivePenetration(this, actor, t);
                        var armorPen = Math.Clamp(skill.ArmorPenetration + passiveArmor, 0, 100);
                        var shieldPen = Math.Clamp(skill.ShieldPenetration + passiveShield, 0, 100);
                        var defense = t.Stat(StatKind.Defense) * (100 - armorPen) / 100.0;
                        var raw = skill.Kind == SkillKind.Physical
                            ? actor.Stat(StatKind.Attack) * skill.Power - defense * balance.PhysicalDefenseFactor
                            : actor.Stat(StatKind.Magic) * skill.Power * balance.MagicMultiplier - defense * balance.MagicDefenseFactor;
                        raw = Math.Max(1, raw) + skill.FlatAmount + Scaled(skill.Scalings, actor, t);
                        if (skill.CritChance > 0 && _session.Rng.Next(100) < skill.CritChance)
                        {
                            raw *= skill.CritMultiplier;
                            notes.Add("critique");
                        }
                        // Passifs « dégâts +% » de l'attaquant et « dégâts reçus +% » de la cible, vérifiés à ce coup-ci.
                        raw *= _session.PassiveDamageFactor(this, actor, t);
                        var damage = (int)Math.Round(raw * Variance() * factor * (t.Defending ? 0.5 : 1));
                        if (factor < 0)
                        {
                            // L'élément soigne la cible.
                            var absorbed = Math.Min(-damage, t.Stats.MaxHp - t.Hp);
                            t.Hp += absorbed;
                            notes.Add($"absorbe +{absorbed} PV");
                            continue;
                        }
                        if (factor > 0) damage = Math.Max(1, damage);
                        if (t.Shield > 0)
                        {
                            // La part qui pénètre le bouclier va directement sur les PV.
                            var blocked = Math.Min(t.Shield, damage - damage * shieldPen / 100);
                            t.Shield -= blocked;
                            damage -= blocked;
                            if (blocked > 0) notes.Add($"bouclier -{blocked}");
                        }
                        t.Hp = Math.Max(0, t.Hp - damage);
                        total += damage;
                    }
                    var extra = notes.Count > 0 ? $" ({string.Join(", ", notes.Distinct())})" : "";
                    parts.Add(t.IsAlive ? $"{t.Name} -{total} PV{extra}" : $"{t.Name} -{total} PV{extra}, vaincu !");
                    if (skill.DrainPercent > 0 && total > 0)
                    {
                        var drained = Math.Min(total * skill.DrainPercent / 100, actor.Stats.MaxHp - actor.Hp);
                        actor.Hp += drained;
                        if (drained > 0) parts.Add($"{actor.Name} +{drained} PV");
                    }
                    break;
                }
            }
            hits.Add((t, before));
        }

        // Effets durables.
        foreach (var effect in skill.Effects)
        {
            var receivers = effect.OnSelf ? new List<Combatant> { actor } : targets.Where(t => t.IsAlive).ToList();
            foreach (var r in receivers)
            {
                if (effect.Chance < 100 && _session.Rng.Next(100) >= effect.Chance) continue;
                parts.Add(ApplyEffect(r, Resolve(effect, actor, r), skill.Name));
            }
        }

        Log.Add(skill.UseText.Length > 0
            ? skill.UseText.Replace("%lanceur%", actor.Name).Replace("%sort%", skill.Name)
                .Replace("%cible%", string.Join(", ", targets.Select(t => t.Name))) + (parts.Count > 0 ? " → " + string.Join(", ", parts) : "")
            : $"{actor.Name} : {skill.Name}" + (parts.Count > 0 ? " → " + string.Join(", ", parts) : ""));

        foreach (var (t, before) in hits)
        {
            var max = Math.Max(1, t.Stats.MaxHp);
            foreach (var line in t.Lines.Where(l => l.Trigger == BattleTrigger.HpBelow))
                if (t.IsAlive && t.Hp * 100 < line.Amount * max && before * 100 >= line.Amount * max) Say(t, line);
            if (!t.IsAlive && before > 0)
            {
                Say(t, BattleTrigger.Down);
                Say(actor, BattleTrigger.Kill);
            }
        }
    }

    /// <summary>« faiblesse au feu (200 %) », « résiste à la glace (50 %) », « immunisé à la foudre »...</summary>
    public static string ElementText(SkillEffect e)
    {
        var element = e.Element.Trim().ToLowerInvariant();
        return e.Amount switch
        {
            < 0 => $"absorbe {element} ({-e.Amount} %)",
            0 => $"immunisé : {element}",
            < 100 => $"résiste : {element} ({e.Amount} %)",
            100 => $"{element} normal",
            _ => $"faiblesse : {element} ({e.Amount} %)",
        };
    }

    /// <summary>Part d'un montant qui dépend des stats (« 30 % des PV max du lanceur »).</summary>
    private static int Scaled(IEnumerable<StatScaling> scalings, Combatant caster, Combatant target) =>
        scalings.Sum(s => GameSession.CombatValue(s.OfTarget ? target : caster, s.Stat) * s.Percent / 100);

    /// <summary>L'effet avec son montant final (montant fixe + parts selon les stats).</summary>
    private static SkillEffect Resolve(SkillEffect e, Combatant caster, Combatant target)
    {
        if (e.Scalings.Count == 0) return e;
        return new SkillEffect
        {
            Type = e.Type, Stat = e.Stat, Turns = e.Turns, Chance = e.Chance, OnSelf = e.OnSelf, Element = e.Element,
            Amount = e.Amount + Scaled(e.Scalings, caster, target),
        };
    }

    /// <summary>Pose un effet durable ; un même effet venant de la même compétence est rafraîchi, pas cumulé.</summary>
    private string ApplyEffect(Combatant r, SkillEffect effect, string source)
    {
        if (effect.Type == EffectType.Cleanse)
        {
            var removed = r.Effects.RemoveAll(e => e.IsNegative);
            return removed > 0 ? $"{r.Name} est purifié" : $"{r.Name} n'a rien à purifier";
        }
        // Effets élémentaires : immunisé (ou absorbe) à l'élément = rien ; paralysie moins (ou plus) probable selon la résistance.
        if (ActiveEffect.ElementOf(effect.Type) is { } element)
        {
            var factor = ElementFactor(element, r);
            if (factor <= 0) return $"{r.Name} est immunisé ({element})";
            if (effect.Type == EffectType.Paralysis && factor < 1 && _session.Rng.NextDouble() >= factor)
                return $"{r.Name} résiste à la paralysie";
        }
        var existing = r.Effects.FirstOrDefault(e => e.Source == source && e.Def.Type == effect.Type && e.Def.Stat == effect.Stat
            && string.Equals(e.Def.Element, effect.Element, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) r.Effects.Remove(existing);
        // Le tour en cours compte : un effet de 3 tours dure les 3 prochains tours de la cible.
        r.Effects.Add(new ActiveEffect { Def = effect, Source = source, TurnsLeft = Math.Max(1, effect.Turns) });
        if (effect.Type == EffectType.Shield) r.Shield = Math.Max(r.Shield, effect.Amount);
        return effect.Type switch
        {
            EffectType.Poison => $"{r.Name} est empoisonné",
            EffectType.Regen => $"{r.Name} se régénère",
            EffectType.Stun => $"{r.Name} est étourdi",
            EffectType.StatUp => $"{r.Name} {Combatant.StatName(effect.Stat)} +{effect.Amount} %",
            EffectType.StatDown => $"{r.Name} {Combatant.StatName(effect.Stat)} -{effect.Amount} %",
            EffectType.Element => $"{r.Name} : {ElementText(effect)}",
            EffectType.Burn => $"{r.Name} est embrasé",
            EffectType.Paralysis => $"{r.Name} est paralysé",
            EffectType.Suffocation => $"{r.Name} suffoque",
            _ => $"{r.Name} est protégé ({effect.Amount})",
        };
    }

    // ------------------------------------------------------------------ Passifs revérifiés pendant le combat

    /// <summary>
    /// Après chaque action et à chaque début de tour : les passifs dont les conditions changent (PV sous 30 %,
    /// empoisonné...) s'activent ou s'arrêtent ; leurs bonus de stats et résistances suivent (PV et PM max restent fixes).
    /// </summary>
    private void RefreshPassives()
    {
        foreach (var c in All.Where(c => c.IsAlive && c.AllPassives.Count > 0 && c.RawStats is not null))
        {
            var active = c.AllPassives.Where(p => _session.AppliesInCombat(p, this, c, null)).ToList();
            if (active.SequenceEqual(c.Passives)) continue;
            foreach (var p in active.Except(c.Passives)) Log.Add($"{p.Name} ({c.Name}) s'active");
            var stats = GameSession.WithPassives(c.RawStats!, active);
            stats.MaxHp = c.Stats.MaxHp;
            stats.MaxMana = c.Stats.MaxMana;
            c.Stats = stats;
            c.Passives = active;
            c.Resistances = [.. active.SelectMany(p => p.Resistances), .. c.BaseResistances];
        }
    }

    // Déclencheurs déjà joués : (porteur, passif, n°, concerné). « Sous le seuil » = en attente de repasser au-dessus.
    private readonly HashSet<(Combatant, PassiveDef, int, Combatant)> _triggerBelow = [];
    private readonly HashSet<(Combatant, PassiveDef, int, Combatant)> _triggerDone = [];

    private static int HpPercent(Combatant c) => c.Stats.MaxHp > 0 ? c.Hp * 100 / c.Stats.MaxHp : 0;

    /// <summary>Déclencheurs de seuil de PV : l'allié (ou le porteur, ou un ennemi) vient de passer sous le seuil.</summary>
    private void CheckPassiveTriggers()
    {
        foreach (var bearer in All.Where(c => c.IsAlive))
        {
            foreach (var p in bearer.AllPassives.Where(p => p.Triggers.Count > 0))
            {
                for (var i = 0; i < p.Triggers.Count; i++)
                {
                    var t = p.Triggers[i];
                    var watched = t.When switch
                    {
                        PassiveTriggerWhen.SelfHpBelow => [bearer],
                        PassiveTriggerWhen.AllyHpBelow => All.Where(c => c.IsAlly == bearer.IsAlly && c != bearer).ToList(),
                        PassiveTriggerWhen.EnemyHpBelow => All.Where(c => c.IsAlly != bearer.IsAlly).ToList(),
                        _ => [],
                    };
                    foreach (var concerned in watched)
                    {
                        var key = (bearer, p, i, concerned);
                        if (!concerned.IsAlive || HpPercent(concerned) >= t.Threshold)
                        {
                            _triggerBelow.Remove(key); // repassé au-dessus : il pourra se redéclencher
                            continue;
                        }
                        if (!_triggerBelow.Add(key)) continue; // déjà sous le seuil
                        if (t.OncePerBattle && !_triggerDone.Add(key)) continue;
                        if (!_session.AppliesInCombat(p, this, bearer, concerned)) { _triggerBelow.Remove(key); _triggerDone.Remove(key); continue; }
                        Fire(bearer, p, t, concerned);
                    }
                }
            }
        }
    }

    /// <summary>Déclencheurs « au début de chaque tour du porteur ».</summary>
    private void TurnStartTriggers(Combatant bearer)
    {
        foreach (var p in bearer.AllPassives)
            foreach (var t in p.Triggers.Where(t => t.When == PassiveTriggerWhen.TurnStart))
                if (_session.AppliesInCombat(p, this, bearer, null)) Fire(bearer, p, t, bearer);
    }

    private void Fire(Combatant bearer, PassiveDef p, PassiveTrigger t, Combatant concerned)
    {
        var receivers = t.Target switch
        {
            PassiveTriggerTarget.Self => [bearer],
            PassiveTriggerTarget.AllAllies => All.Where(c => c.IsAlly == bearer.IsAlly && c.IsAlive).ToList(),
            PassiveTriggerTarget.AllEnemies => All.Where(c => c.IsAlly != bearer.IsAlly && c.IsAlive).ToList(),
            _ => new List<Combatant> { concerned },
        };
        foreach (var effect in t.Effects)
            foreach (var r in receivers.Where(r => r.IsAlive))
                if (effect.Chance >= 100 || _session.Rng.Next(100) < effect.Chance)
                    Log.Add($"{p.Name} ({bearer.Name}) : {ApplyEffect(r, Resolve(effect, bearer, r), $"passif:{p.Id}:{bearer.Name}")}");
    }

    private void CheckEnd()
    {
        if (Outcome != BattleOutcome.Ongoing) return;
        RefreshPassives();
        CheckPassiveTriggers();
        if (Enemies.All(e => !e.IsAlive))
        {
            SayAll(BattleTrigger.Victory);
            Log.Add("Victoire !");
            Finish(BattleOutcome.Victory);
        }
        else if (Allies.All(a => !a.IsAlive))
        {
            SayAll(BattleTrigger.Defeat);
            Log.Add("Défaite...");
            Finish(BattleOutcome.Defeat);
        }
    }

    // ------------------------------------------------------------------ Répliques de combat

    /// <summary>Déclenche un moment pour tous (participants puis répliques du combat fixe).</summary>
    private void SayAll(BattleTrigger trigger, int turn = 0)
    {
        foreach (var c in All)
            foreach (var line in c.Lines.Where(l => l.Trigger == trigger && (trigger != BattleTrigger.Turn || l.Amount == turn)))
                Say(c, line);
        foreach (var line in _fixedLines.Where(l => l.Trigger == trigger && (trigger != BattleTrigger.Turn || l.Amount == turn)))
            Say(null, line);
    }

    private void Say(Combatant owner, BattleTrigger trigger)
    {
        foreach (var line in owner.Lines.Where(l => l.Trigger == trigger)) Say(owner, line);
    }

    /// <summary>Dit une réplique (une seule fois par combat), si ses conditions et sa chance passent.</summary>
    private void Say(Combatant? owner, BattleLine line)
    {
        if (line.Text.Length == 0 || _said.Contains((owner, line))) return;
        if (!_session.CheckAll(line.Conditions)) return;
        if (line.Chance < 100 && _session.Rng.Next(100) >= line.Chance) return;
        _said.Add((owner, line));
        var speaker = line.Speaker.Length > 0 ? _session.FormatText(line.Speaker) : owner?.Name ?? "";
        var text = _session.FormatText(line.Text);
        SpeechLines.Add(Log.Count);
        Log.Add(speaker.Length > 0 ? $"{speaker} : « {text} »" : text);
        foreach (var action in line.Actions.Where(a => a.Type != ActionType.StartBattle)) _session.Execute(action);
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
