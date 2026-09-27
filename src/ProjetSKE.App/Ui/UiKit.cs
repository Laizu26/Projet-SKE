using Microsoft.Maui.Controls.Shapes;
using ProjetSKE.Core.Models;

namespace ProjetSKE.App.Ui;

/// <summary>Couleurs du jeu : style sobre des vieux jeux de gestion.</summary>
public static class Theme
{
    public static readonly Color Bg = Color.FromArgb("#101418");
    public static readonly Color Panel = Color.FromArgb("#1B232C");
    public static readonly Color Header = Color.FromArgb("#243B55");
    public static readonly Color Accent = Color.FromArgb("#E0B040");
    public static readonly Color Text = Color.FromArgb("#E6E6E6");
    public static readonly Color Muted = Color.FromArgb("#8A96A3");
    public static readonly Color Good = Color.FromArgb("#5CBF60");
    public static readonly Color Hp = Color.FromArgb("#4CAF50");
    public static readonly Color Mana = Color.FromArgb("#3F7FD0");
    public static readonly Color Danger = Color.FromArgb("#D05040");
    public static readonly Color ButtonBg = Color.FromArgb("#2A3440");
    public static readonly Color ButtonSelected = Color.FromArgb("#3A5A80");
    public static readonly Color Overlay = Color.FromArgb("#E6000000");
}

/// <summary>Petites briques d'interface réutilisées partout (texte, boutons, barres, panneaux).</summary>
public static class UiKit
{
    public static Label Txt(string text, double size = 14, Color? color = null, bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        TextColor = color ?? Theme.Text,
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        LineBreakMode = LineBreakMode.WordWrap,
    };

    public static Label Muted(string text, double size = 12) => Txt(text, size, Theme.Muted);

    public static Label Heading(string text) => Txt(text, 20, Theme.Accent, bold: true);

    public static Label Section(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        TextColor = Theme.Text,
        BackgroundColor = Theme.Header,
        Padding = new Thickness(8, 4),
        Margin = new Thickness(0, 6, 0, 0),
    };

    public static Button Btn(string text, Action onClick, bool enabled = true, bool selected = false)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 14,
            TextColor = Theme.Text,
            BackgroundColor = selected ? Theme.ButtonSelected : Theme.ButtonBg,
            CornerRadius = 0,
            Padding = new Thickness(10, 6),
            MinimumHeightRequest = 40,
            IsEnabled = enabled,
        };
        button.Clicked += (_, _) => onClick();
        return button;
    }

    /// <summary>Barre de jauge : "PV [██████    ] 45/60".</summary>
    public static View Bar(string label, int value, int max, Color color)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(30)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(72)),
            },
            ColumnSpacing = 6,
        };
        grid.Add(Muted(label), 0, 0);
        grid.Add(new ProgressBar
        {
            Progress = max > 0 ? Math.Clamp((double)value / max, 0, 1) : 0,
            ProgressColor = color,
            VerticalOptions = LayoutOptions.Center,
        }, 1, 0);
        grid.Add(Txt($"{value}/{max}", 12), 2, 0);
        return grid;
    }

    public static Border Panel(View content, Color? background = null) => new()
    {
        Content = content,
        BackgroundColor = background ?? Theme.Panel,
        Stroke = Theme.Header,
        StrokeThickness = 1,
        StrokeShape = new Rectangle(),
        Padding = new Thickness(10, 8),
    };

    public static VerticalStackLayout Stack(params View[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        foreach (var child in children) stack.Add(child);
        return stack;
    }

    /// <summary>Ligne de boutons de même largeur.</summary>
    public static Grid ButtonRow(params View[] children)
    {
        var grid = new Grid { ColumnSpacing = 4 };
        for (var i = 0; i < children.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.Add(children[i], i, 0);
        }
        return grid;
    }

    /// <summary>Texte à gauche, élément (souvent un bouton) à droite.</summary>
    public static Grid Row(View left, View right)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8,
        };
        left.VerticalOptions = LayoutOptions.Center;
        right.VerticalOptions = LayoutOptions.Center;
        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        return grid;
    }

    public static string StatsLine(StatBlock s) =>
        $"PV {s.MaxHp} · PM {s.MaxMana} · ATQ {s.Attack} · DEF {s.Defense} · MAG {s.Magic} · VIT {s.Speed}";

    public static string ItemTypeName(ItemDef item) => item.Type switch
    {
        ItemType.Consumable => "Consommable",
        ItemType.Weapon => "Arme",
        ItemType.Armor => "Armure",
        ItemType.Relic => item.RelicUsage == RelicUsage.Quest ? "Relique (quête)" : "Relique",
        _ => "Objet de quête",
    };

    public static string ItemSummary(ItemDef item)
    {
        var bonus = item.Bonus.ToBonusString();
        var parts = new List<string> { ItemTypeName(item) };
        if (bonus.Length > 0) parts.Add(bonus);
        if (item.HealHp > 0) parts.Add($"+{item.HealHp} PV");
        if (item.HealMana > 0) parts.Add($"+{item.HealMana} PM");
        if (item.IsUnique) parts.Add("unique");
        return string.Join(" · ", parts);
    }

    public static string SlotName(EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => "Arme",
        EquipSlot.Armor => "Armure",
        _ => "Relique",
    };

    public static string Describe(TravelEncounterMode mode) => mode switch
    {
        TravelEncounterMode.None => "Aucun",
        TravelEncounterMode.RandomOnly => "Aléatoires",
        TravelEncounterMode.FixedOnly => "Fixes (histoire)",
        _ => "Aléatoires + fixes",
    };

    public static string Describe(DefeatRule rule) => rule switch
    {
        DefeatRule.GameOver => "Game over",
        _ => "Retour en ville",
    };

    public static string Describe(FleeRule rule) => rule switch
    {
        FleeRule.AlwaysSucceed => "Toujours réussie",
        FleeRule.Never => "Impossible",
        _ => "Selon la vitesse",
    };

    public static T Next<T>(T value) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        var index = Array.IndexOf(values, value);
        return values[(index + 1) % values.Length];
    }
}
