using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.App.Ui;

/// <summary>
/// Descriptions en clair pour le joueur (fiche d'un personnage) : ce que fait une compétence, un effet, un passif.
/// </summary>
public static class GameInfo
{
    public static string KindName(SkillKind k) => k switch
    {
        SkillKind.Physical => "Physique",
        SkillKind.Magical => "Magique",
        SkillKind.Heal => "Soin",
        SkillKind.Revive => "Résurrection",
        _ => "Effet",
    };

    public static string TargetName(SkillTarget t) => t switch
    {
        SkillTarget.SingleEnemy => "un ennemi",
        SkillTarget.AllEnemies => "tous les ennemis",
        SkillTarget.SingleAlly => "un allié",
        SkillTarget.AllAllies => "tous les alliés",
        _ => "soi-même",
    };

    private static string StatPhrase(string key) => key switch
    {
        "pv" => "des PV actuels", "pvmax" => "des PV max", "pvperdus" => "des PV perdus", "pm" => "des PM actuels",
        "pmmax" => "des PM max", "atq" => "de l'ATQ", "def" => "de la DEF", "mag" => "de la MAG", "vit" => "de la VIT",
        "niveau" => "du niveau", _ => "de " + CharacterStats.NameOf(key),
    };

    /// <summary>« + 30 % des PV max du lanceur + 10 % de la DEF de la cible ».</summary>
    public static string Scalings(IEnumerable<StatScaling> scalings, string caster = "du lanceur") =>
        string.Concat(scalings.Select(s => $" + {s.Percent} % {StatPhrase(s.Stat)} {(s.OfTarget ? "de la cible" : caster)}"));

    /// <summary>Une ligne pour un effet durable (« Bouclier 20 + 30 % des PV max du lanceur, 3 tours »).</summary>
    public static string Effect(SkillEffect e, string caster = "du lanceur")
    {
        var scale = Scalings(e.Scalings, caster);
        var turns = e.Type == EffectType.Cleanse ? "" : $", {e.Turns} tour{(e.Turns > 1 ? "s" : "")}";
        var chance = e.Chance < 100 ? $" ({e.Chance} % de chance)" : "";
        var who = e.OnSelf ? " sur soi" : "";
        var text = e.Type switch
        {
            EffectType.Poison => $"Poison : {e.Amount}{scale} PV par tour",
            EffectType.Regen => $"Régénération : {e.Amount}{scale} PV par tour",
            EffectType.Stun => "Étourdissement (perd ses tours)",
            EffectType.StatUp => $"{Combatant.StatName(e.Stat)} +{e.Amount}{scale} %",
            EffectType.StatDown => $"{Combatant.StatName(e.Stat)} −{e.Amount}{scale} %",
            EffectType.Shield => $"Bouclier de {e.Amount}{scale}",
            EffectType.Cleanse => "Purifie les effets négatifs",
            EffectType.Element => Battle.ElementText(e),
            EffectType.Burn => $"Embrasé (feu) : {e.Amount}{scale} dégâts par tour",
            EffectType.Paralysis => "Paralysé (foudre) : plus d'actions physiques",
            EffectType.Suffocation => $"Suffoqué (air) : {e.Amount}{scale} dégâts par tour",
            _ => e.Type.ToString(),
        };
        return text + who + turns + chance;
    }

    /// <summary>Détail d'une compétence, ligne par ligne.</summary>
    public static List<string> Skill(SkillDef s, GameDatabase db)
    {
        var lines = new List<string> { $"{KindName(s.Kind)} · cible : {TargetName(s.Target)}" };
        var costs = new List<string>();
        if (s.ManaCost > 0) costs.Add($"{s.ManaCost} PM");
        if (s.HpCost > 0) costs.Add($"{s.HpCost} PV");
        if (s.Cooldown > 0) costs.Add($"recharge {s.Cooldown} tour{(s.Cooldown > 1 ? "s" : "")}");
        lines.Add(costs.Count > 0 ? "Coût : " + string.Join(" · ", costs) : "Coût : aucun");
        var scale = Scalings(s.Scalings);
        var power = s.Power != 0 ? $"{s.Power:0.##} × {(s.Kind == SkillKind.Physical ? "ATQ" : "MAG")}" : "";
        var flat = s.FlatAmount != 0 ? $"{s.FlatAmount}" : "";
        var amount = string.Join(" + ", new[] { power, flat }.Where(x => x.Length > 0)) + scale;
        amount = amount.TrimStart(' ', '+');
        switch (s.Kind)
        {
            case SkillKind.Physical or SkillKind.Magical:
                lines.Add($"Dégâts : {(amount.Length > 0 ? amount : "—")}" + (s.Hits > 1 ? $" (× {s.Hits} coups)" : ""));
                var extra = new List<string>();
                if (s.Element.Length > 0) extra.Add($"élément {s.Element}");
                if (s.Accuracy < 100) extra.Add($"précision {s.Accuracy} %");
                if (s.CritChance > 0) extra.Add($"critique {s.CritChance} % (× {s.CritMultiplier:0.##})");
                if (s.DrainPercent > 0) extra.Add($"vol de vie {s.DrainPercent} %");
                if (s.ArmorPenetration > 0) extra.Add($"ignore {s.ArmorPenetration} % de la DEF");
                if (s.ShieldPenetration > 0) extra.Add($"{s.ShieldPenetration} % traverse les boucliers");
                if (extra.Count > 0) lines.Add(string.Join(" · ", extra));
                break;
            case SkillKind.Heal:
                lines.Add($"Soin : {(amount.Length > 0 ? amount : "—")}");
                break;
            case SkillKind.Revive:
                lines.Add($"Relève avec : {(amount.Length > 0 ? amount : "1")} PV");
                break;
        }
        foreach (var e in s.Effects) lines.Add("• " + Effect(e));
        if (s.PowerId is { } pid && db.Powers.TryGetValue(pid, out var pw)) lines.Add($"Pouvoir : {pw.Name} (niveau {s.PowerLevel})");
        return lines;
    }

    /// <summary>Détail d'un passif, ligne par ligne.</summary>
    public static List<string> Passive(PassiveDef p)
    {
        var lines = new List<string>();
        var bonus = p.Bonus.ToBonusString();
        if (bonus.Length > 0) lines.Add("Stats : " + bonus);
        var percent = Percents(p.Percent);
        if (percent.Length > 0) lines.Add("Stats en % : " + percent);
        foreach (var r in p.Resistances)
            lines.Add("• " + Battle.ElementText(new SkillEffect { Type = EffectType.Element, Element = r.Element, Amount = r.Percent }));
        if (p.DamagePercent != 0) lines.Add($"Dégâts infligés {(p.DamagePercent > 0 ? "+" : "")}{p.DamagePercent} %");
        if (p.DamageTakenPercent != 0) lines.Add($"Dégâts reçus {(p.DamageTakenPercent > 0 ? "+" : "")}{p.DamageTakenPercent} %");
        if (p.ArmorPenetration != 0) lines.Add($"Ignore {p.ArmorPenetration} % de la DEF adverse");
        if (p.ShieldPenetration != 0) lines.Add($"{p.ShieldPenetration} % des dégâts traverse les boucliers");
        if (p.HpPerTurn != 0 || p.HpPerTurnPercent != 0)
            lines.Add($"PV par tour : {Join(p.HpPerTurn, p.HpPerTurnPercent, "PV max")}");
        if (p.ManaPerTurn != 0 || p.ManaPerTurnPercent != 0)
            lines.Add($"PM par tour : {Join(p.ManaPerTurn, p.ManaPerTurnPercent, "PM max")}");
        foreach (var e in p.BattleStart) lines.Add("Début du combat : " + Effect(e, "du porteur"));
        foreach (var t in p.Triggers)
        {
            var when = t.When switch
            {
                PassiveTriggerWhen.AllyHpBelow => $"Quand un allié passe sous {t.Threshold} % de PV",
                PassiveTriggerWhen.SelfHpBelow => $"Quand il passe sous {t.Threshold} % de PV",
                PassiveTriggerWhen.EnemyHpBelow => $"Quand un ennemi passe sous {t.Threshold} % de PV",
                _ => "À chacun de ses tours",
            };
            var on = t.Target switch
            {
                PassiveTriggerTarget.Concerned => t.When == PassiveTriggerWhen.TurnStart ? "lui" : "celui-ci",
                PassiveTriggerTarget.Self => "lui",
                PassiveTriggerTarget.AllAllies => "tous ses alliés",
                _ => "tous ses ennemis",
            };
            foreach (var e in t.Effects) lines.Add($"{when} → {on} : {Effect(e, "du porteur")}");
        }
        if (p.XpPercent != 0) lines.Add($"XP en combat +{p.XpPercent} %");
        if (p.GoldPercent != 0) lines.Add($"Or en combat +{p.GoldPercent} %");
        if (p.Conditions.Count > 0) lines.Add("Agit seulement dans certaines conditions.");
        return lines;
    }

    private static string Join(int flat, int percent, string of) =>
        string.Join(" + ", new[] { flat != 0 ? flat.ToString() : "", percent != 0 ? $"{percent} % des {of}" : "" }.Where(x => x.Length > 0));

    private static string Percents(StatBlock s)
    {
        var parts = new List<string>();
        void Add(int v, string n) { if (v != 0) parts.Add($"{n} {(v > 0 ? "+" : "")}{v} %"); }
        Add(s.MaxHp, "PV"); Add(s.MaxMana, "PM"); Add(s.Attack, "ATQ"); Add(s.Defense, "DEF"); Add(s.Magic, "MAG"); Add(s.Speed, "VIT");
        return string.Join(" · ", parts);
    }
}
