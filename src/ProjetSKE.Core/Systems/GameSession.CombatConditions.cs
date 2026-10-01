using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Systems;

/// <summary>
/// Conditions vérifiées pendant un combat, à chaque coup : « @soi » = le combattant dont on vérifie le passif,
/// « @cible » = son adversaire à ce coup. Effets en cours (poison, bouclier...), PV du moment, et comparaison de
/// deux stats (« l'adversaire a 5 niveaux de plus que moi »).
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Effets de combat testés par la condition « A un effet » (clé courte, aussi en mode texte).</summary>
    public static readonly (string Key, string Name)[] CombatEffects =
    [
        ("poison", "Empoisonné"), ("regen", "Régénération"), ("etourdi", "Étourdi"), ("bouclier", "Bouclier"),
        ("bonus", "Bonus de stat"), ("malus", "Malus de stat"), ("garde", "En garde"), ("negatif", "Un effet négatif"),
    ];

    private Battle? _combatBattle;
    private Combatant? _combatSelf;
    private Combatant? _combatTarget;

    /// <summary>
    /// Multiplicateur de dégâts d'un coup : passifs « dégâts +% » de l'attaquant (conditions vérifiées avec
    /// @soi = l'attaquant, @cible = la cible) et « dégâts reçus +% » de la cible (@soi = la cible, @cible = l'attaquant).
    /// </summary>
    public double PassiveDamageFactor(Battle battle, Combatant attacker, Combatant target)
    {
        var dealt = attacker.AllPassives.Where(p => p.DamagePercent != 0 && AppliesInCombat(p, battle, attacker, target)).Sum(p => p.DamagePercent);
        var taken = target.AllPassives.Where(p => p.DamageTakenPercent != 0 && AppliesInCombat(p, battle, target, attacker)).Sum(p => p.DamageTakenPercent);
        return Math.Max(0, 100 + dealt) / 100.0 * (Math.Max(0, 100 + taken) / 100.0);
    }

    /// <summary>Le passif agit à cet instant du combat (ses conditions, avec @soi et @cible).</summary>
    public bool AppliesInCombat(PassiveDef p, Battle battle, Combatant self, Combatant? target)
    {
        if (p.Conditions.Count == 0) return true;
        var (b, s, t, owner) = (_combatBattle, _combatSelf, _combatTarget, _passiveOwner);
        _combatBattle = battle;
        _combatSelf = self;
        _combatTarget = target;
        _passiveOwner = self.Id;
        try { return CheckAll(p.Conditions); }
        finally { (_combatBattle, _combatSelf, _combatTarget, _passiveOwner) = (b, s, t, owner); }
    }

    /// <summary>Le combattant désigné (« @soi », « @cible », ou un PJ qui se bat) pendant un combat, sinon null.</summary>
    private Combatant? CombatantFor(string who)
    {
        if (_combatBattle is null) return null;
        if (who is "" or "@soi") return _combatSelf;
        if (who is "@cible" or "@adversaire") return _combatTarget;
        var id = ResolveWho(who);
        return _combatBattle.Allies.Concat(_combatBattle.Enemies).FirstOrDefault(c => c.Id == id);
    }

    /// <summary>Valeur d'une stat pour un combattant (PV du moment, bonus et malus en cours compris).</summary>
    internal static int CombatValue(Combatant c, string key) => key switch
    {
        "pv" => c.Hp,
        "pvperdus" => Math.Max(0, c.Stats.MaxHp - c.Hp),
        "pv%" => c.Stats.MaxHp > 0 ? c.Hp * 100 / c.Stats.MaxHp : 0,
        "pvmax" => c.Stats.MaxHp,
        "pm" => c.Mana,
        "pm%" => c.Stats.MaxMana > 0 ? c.Mana * 100 / c.Stats.MaxMana : 0,
        "pmmax" => c.Stats.MaxMana,
        "atq" => c.Stat(StatKind.Attack),
        "def" => c.Stat(StatKind.Defense),
        "mag" => c.Stat(StatKind.Magic),
        "vit" => c.Stat(StatKind.Speed),
        "niveau" => c.Level,
        _ => 0,
    };

    /// <summary>Stat d'un personnage : en combat, celle du moment ; sinon celle de sa fiche. Null = personne.</summary>
    private int? StatOf(string who, string key)
    {
        if (CombatantFor(who) is { } c) return CombatValue(c, key);
        if (who is "@cible" or "@adversaire") return null; // hors combat : pas d'adversaire
        return ConditionTargets(who).FirstOrDefault() is { } pj ? CharacterStats.Value(this, pj, key) : null;
    }

    private bool StatCondition(Condition c)
    {
        if (CombatantFor(c.Arg) is { } fighter) return Compare(CombatValue(fighter, c.Arg2), c.Op, c.Amount);
        if (c.Arg is "@cible" or "@adversaire") return false;
        // « @equipe » : au moins un membre ; sinon le PJ visé (celui qui parle par défaut).
        return ConditionTargets(c.Arg).Any(p => Compare(CharacterStats.Value(this, p, c.Arg2), c.Op, c.Amount));
    }

    private bool HasEffectCondition(Condition c)
    {
        if (CombatantFor(c.Arg) is not { } f) return false; // hors combat : aucun effet
        return c.Arg2 switch
        {
            "poison" => f.Effects.Any(e => e.Def.Type == EffectType.Poison),
            "regen" => f.Effects.Any(e => e.Def.Type == EffectType.Regen),
            "etourdi" => f.IsStunned,
            "bouclier" => f.Shield > 0,
            "bonus" => f.Effects.Any(e => e.Def.Type == EffectType.StatUp),
            "malus" => f.Effects.Any(e => e.Def.Type == EffectType.StatDown),
            "garde" => f.Defending,
            "negatif" => f.Effects.Any(e => e.IsNegative),
            _ => false,
        };
    }

    /// <summary>Stat de A comparée à celle de B, plus un écart : ex. niveau de @cible ≥ niveau de @soi + 5.</summary>
    private bool CompareStatsCondition(Condition c)
    {
        var left = StatOf(c.Arg, c.Arg2);
        var right = StatOf(c.Other.Length > 0 ? c.Other : "@soi", c.OtherStat.Length > 0 ? c.OtherStat : c.Arg2);
        return left is { } l && right is { } r && Compare(l, c.Op, r + c.Amount);
    }
}
