using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;

namespace ProjetSKE.Core.Systems;

/// <summary>Stats d'un personnage testées par la condition « Stat » (clé courte, aussi utilisée en mode texte).</summary>
public static class CharacterStats
{
    public static readonly (string Key, string Name)[] All =
    [
        ("pv", "PV actuels"), ("pv%", "PV actuels (%)"), ("pvmax", "PV max"),
        ("pm", "PM actuels"), ("pm%", "PM actuels (%)"), ("pmmax", "PM max"),
        ("atq", "Attaque"), ("def", "Défense"), ("mag", "Magie"), ("vit", "Vitesse"), ("niveau", "Niveau"),
    ];

    public static bool IsKnown(string key) => All.Any(s => s.Key == key);

    public static string NameOf(string key) => All.FirstOrDefault(s => s.Key == key).Name ?? key;

    /// <summary>Valeur d'une stat pour un personnage (bonus d'équipement et passifs compris).</summary>
    public static int Value(GameSession s, CharacterState c, string key)
    {
        var stats = s.GetStats(c);
        return key switch
        {
            "pv" => c.CurrentHp,
            "pv%" => stats.MaxHp > 0 ? c.CurrentHp * 100 / stats.MaxHp : 0,
            "pvmax" => stats.MaxHp,
            "pm" => c.CurrentMana,
            "pm%" => stats.MaxMana > 0 ? c.CurrentMana * 100 / stats.MaxMana : 0,
            "pmmax" => stats.MaxMana,
            "atq" => stats.Attack,
            "def" => stats.Defense,
            "mag" => stats.Magic,
            "vit" => stats.Speed,
            "niveau" => c.Level,
            _ => 0,
        };
    }
}
