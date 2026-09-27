using Microsoft.Maui.Controls.Shapes;
using ProjetSKE.Core.Models;

namespace ProjetSKE.App.Ui;

/// <summary>Palette « fantasy sombre » : bleu nuit, parchemin et or.</summary>
public static class Theme
{
    public static readonly Color Bg = Color.FromArgb("#0D1220");
    public static readonly Color BgTop = Color.FromArgb("#1A2238");
    public static readonly Color Surface = Color.FromArgb("#1C2438");
    public static readonly Color Surface2 = Color.FromArgb("#263150");
    public static readonly Color Stroke = Color.FromArgb("#34406A");
    public static readonly Color Track = Color.FromArgb("#0B0F1A");

    public static readonly Color Accent = Color.FromArgb("#E8B84A");
    public static readonly Color AccentLight = Color.FromArgb("#F6D98E");
    public static readonly Color AccentDark = Color.FromArgb("#A67C22");
    public static readonly Color Text = Color.FromArgb("#EEE8DA");
    public static readonly Color Muted = Color.FromArgb("#98A2BA");
    public static readonly Color Good = Color.FromArgb("#7ED08A");
    public static readonly Color Hp = Color.FromArgb("#5CC46A");
    public static readonly Color Mana = Color.FromArgb("#4F9BEF");
    public static readonly Color Danger = Color.FromArgb("#E8604F");
    public static readonly Color Xp = Color.FromArgb("#B78CF0");

    public static readonly Color Overlay = Color.FromArgb("#CC05080F");
    public static readonly Color Parchment = Color.FromArgb("#EADFC2");
    public static readonly Color Ink = Color.FromArgb("#3B2A17");

    // Anciens noms conservés pour l'éditeur.
    public static readonly Color Panel = Surface;
    public static readonly Color Header = Surface2;
    public static readonly Color ButtonBg = Surface2;
    public static readonly Color ButtonSelected = Color.FromArgb("#3B4C7E");

    public static Brush Vertical(Color top, Color bottom) => new LinearGradientBrush
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(0, 1),
        GradientStops = { new GradientStop(top, 0f), new GradientStop(bottom, 1f) },
    };

    public static Brush Diagonal(Color from, Color to) => new LinearGradientBrush
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(1, 1),
        GradientStops = { new GradientStop(from, 0f), new GradientStop(to, 1f) },
    };

    public static Brush PageBackground => Vertical(BgTop, Bg);
    public static Brush GoldButton => Vertical(AccentLight, Accent);

    /// <summary>Ambiance de chaque type de lieu : icône et dégradé.</summary>
    public static (string Icon, Color From, Color To) LocationStyle(LocationType type) => type switch
    {
        LocationType.City => ("🏰", Color.FromArgb("#6B4A1C"), Color.FromArgb("#241A10")),
        LocationType.Dungeon => ("💀", Color.FromArgb("#4E1E3A"), Color.FromArgb("#170A14")),
        _ => ("🌲", Color.FromArgb("#1F5A3A"), Color.FromArgb("#0C1F16")),
    };

    private static readonly Color[] AvatarColors =
    [
        Color.FromArgb("#C0553F"), Color.FromArgb("#3F7CC0"), Color.FromArgb("#4FA05A"), Color.FromArgb("#9A5CC0"),
        Color.FromArgb("#C09A3F"), Color.FromArgb("#3FA5A0"), Color.FromArgb("#C0457F"), Color.FromArgb("#6E7FA8"),
    ];

    /// <summary>Couleur stable pour un personnage (d'après son identifiant).</summary>
    public static Color AvatarColor(string id)
    {
        var hash = 0;
        foreach (var ch in id) hash = unchecked(hash * 31 + ch);
        return AvatarColors[Math.Abs(hash % AvatarColors.Length)];
    }

    public static Color Darker(Color c, float factor = 0.45f) =>
        new(c.Red * factor, c.Green * factor, c.Blue * factor, c.Alpha);

    public static Color Lighter(Color c, float amount = 0.2f) =>
        new(c.Red + (1 - c.Red) * amount, c.Green + (1 - c.Green) * amount, c.Blue + (1 - c.Blue) * amount, c.Alpha);

    public static string ItemIcon(ItemDef item) => item.Type switch
    {
        ItemType.Consumable => "🧪",
        ItemType.Weapon => "⚔️",
        ItemType.Armor => "🛡️",
        ItemType.Relic => "💎",
        _ => "📜",
    };
}

/// <summary>Briques d'interface réutilisées partout.</summary>
public static class UiKit
{
    // ------------------------------------------------------------------ Texte

    public static Label Txt(string text, double size = 14, Color? color = null, bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        TextColor = color ?? Theme.Text,
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        LineBreakMode = LineBreakMode.WordWrap,
    };

    public static Label Muted(string text, double size = 12) => Txt(text, size, Theme.Muted);

    public static Label Heading(string text)
    {
        var label = Txt(text, 22, Theme.Accent, bold: true);
        label.CharacterSpacing = 1;
        return label;
    }

    /// <summary>Titre de section : petites capitales dorées et filet.</summary>
    public static View Section(string text)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 10,
            Margin = new Thickness(0, 10, 0, 2),
        };
        var label = Txt(text.ToUpperInvariant(), 12, Theme.Accent, bold: true);
        label.CharacterSpacing = 2;
        grid.Add(label, 0, 0);
        grid.Add(new BoxView { HeightRequest = 1, Color = Theme.Stroke, VerticalOptions = LayoutOptions.Center }, 1, 0);
        return grid;
    }

    public static Label Icon(string emoji, double size = 28) => new()
    {
        Text = emoji,
        FontSize = size,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
    };

    // ------------------------------------------------------------------ Boutons

    /// <summary>Bouton secondaire (fond sombre, liseré) ; doré quand il est sélectionné.</summary>
    public static Button Btn(string text, Action onClick, bool enabled = true, bool selected = false)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = selected ? Theme.Bg : Theme.Text,
            Background = selected ? Theme.GoldButton : new SolidColorBrush(Theme.Surface2),
            BorderColor = selected ? Theme.Accent : Theme.Stroke,
            BorderWidth = 1,
            CornerRadius = 12,
            Padding = new Thickness(12, 8),
            MinimumHeightRequest = 44,
            IsEnabled = enabled,
            Opacity = enabled ? 1 : 0.45,
        };
        button.Clicked += (_, _) => onClick();
        return button;
    }

    /// <summary>Bouton principal doré.</summary>
    public static Button Primary(string text, Action onClick, bool enabled = true)
    {
        var button = Btn(text, onClick, enabled, selected: true);
        button.FontSize = 16;
        button.MinimumHeightRequest = 52;
        button.CornerRadius = 14;
        return button;
    }

    /// <summary>Petit bouton en forme de pastille (ex : flèche retour).</summary>
    public static Button Pill(string text, Action onClick)
    {
        var button = Btn(text, onClick);
        button.FontSize = 13;
        button.CornerRadius = 18;
        button.MinimumHeightRequest = 36;
        button.Padding = new Thickness(14, 4);
        button.HorizontalOptions = LayoutOptions.Start;
        return button;
    }

    /// <summary>Rend n'importe quel élément cliquable.</summary>
    public static T OnTap<T>(T view, Action action) where T : View
    {
        view.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(action) });
        return view;
    }

    // ------------------------------------------------------------------ Cartes et tuiles

    public static Border Panel(View content, Color? background = null) => Card(content, background);

    /// <summary>Carte arrondie avec ombre.</summary>
    public static Border Card(View content, Color? background = null, Color? stroke = null, double radius = 16) => new()
    {
        Content = content,
        BackgroundColor = background ?? Theme.Surface,
        Stroke = stroke ?? Theme.Stroke,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = radius },
        Padding = new Thickness(14, 12),
        Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 4), Radius = 12, Opacity = 0.45f },
    };

    /// <summary>Carte avec dégradé (bannières de lieu, en-têtes).</summary>
    public static Border GradientCard(View content, Color from, Color to, double radius = 20) => new()
    {
        Content = content,
        Background = Theme.Diagonal(from, to),
        Stroke = Theme.Accent.WithAlpha(0.35f),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = radius },
        Padding = new Thickness(18, 16),
        Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 6), Radius = 16, Opacity = 0.5f },
    };

    /// <summary>Grande tuile cliquable : icône, titre, sous-titre.</summary>
    public static View Tile(string icon, string title, string subtitle, Action? onTap, Color? tint = null, bool enabled = true)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                Icon(icon, 34),
                new Label
                {
                    Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold,
                    TextColor = tint ?? Theme.Text, HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.WordWrap,
                },
                new Label
                {
                    Text = subtitle, FontSize = 12, TextColor = Theme.Muted,
                    HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap,
                },
            },
        };
        var card = Card(stack, stroke: tint is null ? Theme.Stroke : tint.WithAlpha(0.6f));
        card.MinimumHeightRequest = 130;
        card.Opacity = enabled ? 1 : 0.4;
        if (onTap is not null && enabled) OnTap(card, onTap);
        return card;
    }

    /// <summary>Grille de tuiles (deux colonnes par défaut).</summary>
    public static Grid TileGrid(IReadOnlyList<View> tiles, int columns = 2)
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < tiles.Count; i++)
        {
            if (i % columns == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Add(tiles[i], i % columns, i / columns);
        }
        return grid;
    }

    /// <summary>Pastille ronde avec l'initiale d'un personnage.</summary>
    public static View Avatar(string name, Color color, double size = 52)
    {
        var initial = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();
        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            StrokeShape = new Ellipse(),
            Stroke = Theme.Accent,
            StrokeThickness = 2,
            Background = Theme.Diagonal(color, Theme.Darker(color)),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = initial,
                FontSize = size * 0.42,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
            },
        };
    }

    /// <summary>Petite étiquette (ex : « Nv 3 », « Boss »).</summary>
    public static View Badge(string text, Color color) => new Border
    {
        BackgroundColor = color.WithAlpha(0.18f),
        Stroke = color,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 10 },
        Padding = new Thickness(8, 2),
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Center,
        Content = new Label { Text = text, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = color },
    };

    /// <summary>Jauge arrondie : "PV ▓▓▓▓░░ 45/60".</summary>
    public static View Bar(string label, int value, int max, Color color, double height = 10)
    {
        var pct = max > 0 ? Math.Clamp((double)value / max, 0, 1) : 0;
        var fill = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(Math.Max(pct, 0.0001), GridUnitType.Star)),
                new ColumnDefinition(new GridLength(Math.Max(1 - pct, 0.0001), GridUnitType.Star)),
            },
        };
        fill.Add(new BoxView
        {
            Background = Theme.Vertical(Theme.Lighter(color, 0.3f), color),
            CornerRadius = height / 2,
            IsVisible = pct > 0,
        }, 0, 0);
        var track = new Border
        {
            HeightRequest = height,
            BackgroundColor = Theme.Track,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = height / 2 },
            Padding = 0,
            VerticalOptions = LayoutOptions.Center,
            Content = fill,
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(28)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(66)),
            },
            ColumnSpacing = 8,
        };
        var name = Txt(label, 11, color, bold: true);
        name.VerticalOptions = LayoutOptions.Center;
        grid.Add(name, 0, 0);
        grid.Add(track, 1, 0);
        var numbers = Txt($"{value}/{max}", 12);
        numbers.HorizontalTextAlignment = TextAlignment.End;
        numbers.VerticalOptions = LayoutOptions.Center;
        grid.Add(numbers, 2, 0);
        return grid;
    }

    // ------------------------------------------------------------------ Mise en page

    public static VerticalStackLayout Stack(params View[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        foreach (var child in children) stack.Add(child);
        return stack;
    }

    /// <summary>Ligne de boutons de même largeur.</summary>
    public static Grid ButtonRow(params View[] children)
    {
        var grid = new Grid { ColumnSpacing = 8 };
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
            ColumnSpacing = 10,
        };
        left.VerticalOptions = LayoutOptions.Center;
        right.VerticalOptions = LayoutOptions.Center;
        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        return grid;
    }

    /// <summary>Icône / avatar à gauche, contenu au centre, élément optionnel à droite.</summary>
    public static Grid IconRow(View icon, View content, View? right = null)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 12,
        };
        icon.VerticalOptions = LayoutOptions.Center;
        content.VerticalOptions = LayoutOptions.Center;
        grid.Add(icon, 0, 0);
        grid.Add(content, 1, 0);
        if (right is not null)
        {
            right.VerticalOptions = LayoutOptions.Center;
            grid.Add(right, 2, 0);
        }
        return grid;
    }

    /// <summary>Cellule de statistique : icône, valeur, libellé.</summary>
    public static View StatCell(string icon, string label, int value, Color color) => Card(new VerticalStackLayout
    {
        Spacing = 0,
        Children =
        {
            new Label { Text = icon, FontSize = 18, HorizontalTextAlignment = TextAlignment.Center },
            new Label { Text = value.ToString(), FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = color, HorizontalTextAlignment = TextAlignment.Center },
            new Label { Text = label, FontSize = 10, TextColor = Theme.Muted, HorizontalTextAlignment = TextAlignment.Center, CharacterSpacing = 1 },
        },
    }, Theme.Surface2, radius: 12);

    // ------------------------------------------------------------------ Textes métier

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

    public static string SlotIcon(EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => "⚔️",
        EquipSlot.Armor => "🛡️",
        _ => "💎",
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
