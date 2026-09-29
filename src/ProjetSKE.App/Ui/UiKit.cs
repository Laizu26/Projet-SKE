using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using ProjetSKE.Core.Models;

namespace ProjetSKE.App.Ui;

/// <summary>
/// Direction artistique reprise de « Service Impérial » : parchemin, pierre et or.
/// Fond parchemin, bandeaux en pierre sombre soulignés d'un filet d'or, cartes blanches,
/// petits libellés en capitales espacées, titres à empattements, boutons pierre / texte or.
/// </summary>
public static class Theme
{
    // ------------------------------------------------------------------ Mode sombre

    private const string DarkKey = "theme.dark";

    /// <summary>Mode sombre (réglage de l'appareil, dans le Menu et sur l'écran titre). Désactivé par défaut.</summary>
    public static bool Dark { get; private set; } = LoadDark();

    private static bool LoadDark()
    {
        try { return Preferences.Default.Get(DarkKey, false); }
        catch (Exception) { return false; } // pas de stockage (projet de vérification)
    }

    /// <summary>Change le mode (enregistré sur l'appareil). Les écrans reprennent les couleurs en se redessinant.</summary>
    public static void SetDark(bool dark)
    {
        Dark = dark;
        try { Preferences.Default.Set(DarkKey, dark); }
        catch (Exception) { }
        if (Application.Current is { } app) app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
    }

    private static Color Pick(Color light, Color dark) => Dark ? dark : light;

    // Pierre (stone) : en mode sombre l'échelle s'inverse (texte clair, fonds sombres).
    // Les éléments toujours sombres (bandeaux, combat, cinématiques) utilisent Night, qui ne change pas.
    public static Color Stone950 => Pick(Night.Stone950, Color.FromArgb("#FAF8F6"));
    public static Color Stone900 => Pick(Night.Stone900, Color.FromArgb("#F0EDEA"));
    public static Color Stone800 => Pick(Night.Stone800, Color.FromArgb("#E2DEDB"));
    public static Color Stone700 => Pick(Night.Stone700, Color.FromArgb("#D0CBC7"));
    public static Color Stone600 => Pick(Night.Stone600, Color.FromArgb("#BDB7B2"));
    public static Color Stone500 => Pick(Night.Stone500, Color.FromArgb("#A39C96"));
    public static Color Stone400 => Pick(Night.Stone400, Color.FromArgb("#8C857F"));
    public static Color Stone300 => Pick(Night.Stone300, Color.FromArgb("#524C47"));
    public static Color Stone200 => Pick(Night.Stone200, Color.FromArgb("#3D3834"));
    public static Color Stone100 => Pick(Night.Stone100, Color.FromArgb("#2F2B27"));
    public static Color Stone50 => Pick(Night.Stone50, Color.FromArgb("#282420"));

    // Parchemin (fonds de page et feuilles)
    public static Color Parchment => Pick(Color.FromArgb("#E6E2D6"), Color.FromArgb("#171412"));
    public static Color ParchmentLight => Pick(Color.FromArgb("#FDF6E3"), Color.FromArgb("#26211B"));
    public static Color ParchmentMid => Pick(Color.FromArgb("#F5F0DC"), Color.FromArgb("#221E19"));
    public static Color ParchmentActive => Pick(Color.FromArgb("#E6DCC3"), Color.FromArgb("#342C22"));

    // Or et accents (identiques dans les deux modes)
    public static readonly Color Gold400 = Color.FromArgb("#FACC15");
    public static readonly Color Gold500 = Color.FromArgb("#EAB308");
    public static readonly Color Gold600 = Color.FromArgb("#CA8A04");
    public static Color Gold700 => Pick(Color.FromArgb("#A16207"), Color.FromArgb("#C08A1E"));
    public static readonly Color Amber500 = Color.FromArgb("#F59E0B");
    public static Color Green600 => Pick(Color.FromArgb("#16A34A"), Color.FromArgb("#22C55E"));
    public static readonly Color Green500 = Color.FromArgb("#22C55E");
    public static Color Red600 => Pick(Color.FromArgb("#DC2626"), Color.FromArgb("#F05252"));
    public static readonly Color Red500 = Color.FromArgb("#EF4444");
    public static Color Blue600 => Pick(Color.FromArgb("#2563EB"), Color.FromArgb("#4F8BF5"));
    public static readonly Color Blue500 = Color.FromArgb("#3B82F6");
    public static Color Purple600 => Pick(Color.FromArgb("#9333EA"), Color.FromArgb("#A855F7"));

    // Rôles (noms utilisés dans tout le code)
    public static Color Bg => Parchment;
    public static Color Text => Stone900;
    public static Color Muted => Stone500;
    public static Color Accent => Gold600;
    public static Color AccentLight => Gold500;
    /// <summary>Fond des cartes : blanc, ou pierre sombre en mode sombre.</summary>
    public static Color Surface => Pick(Colors.White, Color.FromArgb("#211D1A"));
    public static Color Surface2 => Stone50;
    public static Color Stroke => Stone200;
    public static Color Track => Stone200;
    public static Color Good => Green600;
    public static Color Danger => Red600;
    public static Color Hp => Green600;
    public static Color Mana => Blue600;
    public static Color Xp => Purple600;
    /// <summary>Fond jaune pâle d'un élément mis en avant (tâche en cours, emplacement choisi).</summary>
    public static Color Highlight => Pick(Color.FromArgb("#FEF9C3"), Color.FromArgb("#3A2F12"));
    /// <summary>Fond rosé d'un élément en erreur (éditeur).</summary>
    public static Color DangerBg => Pick(Color.FromArgb("#FEF2F2"), Color.FromArgb("#3B1D1D"));
    public static readonly Color Overlay = Color.FromArgb("#B3000000");
    public static Color Ink => Stone900;

    // Anciens noms conservés pour l'éditeur.
    public static Color Panel => Surface;
    public static Color Header => Night.Stone900;
    public static Color ButtonBg => Surface;
    public static Color ButtonSelected => Night.Stone900;

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

    /// <summary>Filet d'or : or foncé → or clair → or foncé.</summary>
    public static Brush GoldLine => new LinearGradientBrush
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(1, 0),
        GradientStops = { new GradientStop(Gold600, 0f), new GradientStop(Gold400, 0.5f), new GradientStop(Gold600, 1f) },
    };

    public static Brush PageBackground => new SolidColorBrush(Parchment);
    public static Brush DarkBackground => Diagonal(Night.Stone900, Night.Stone950);

    /// <summary>Icône et couleur d'accent de chaque type de lieu.</summary>
    public static (string Icon, Color Accent) LocationStyle(LocationType type) => type switch
    {
        LocationType.City => (Ico.Castle, Gold500),
        LocationType.Dungeon => (Ico.Skull, Red500),
        _ => (Ico.Trees, Green500),
    };

    /// <summary>Fond clair (le texte posé dessus doit être sombre) ?</summary>
    public static bool IsLight(Color background) => background.GetLuminosity() > 0.45f;

    /// <summary>Encre lisible sur un fond donné (tuiles de carte colorées...), dans les deux modes.</summary>
    public static Color InkOn(Color background) => IsLight(background) ? Night.Stone900 : Night.Stone100;

    private static readonly Color[] AvatarColors =
    [
        Gold500, Blue500, Green500, Red500, Color.FromArgb("#A855F7"), Color.FromArgb("#14B8A6"), Amber500, Color.FromArgb("#EC4899"),
    ];

    /// <summary>Couleur stable pour un personnage (d'après son identifiant).</summary>
    public static Color AvatarColor(string id)
    {
        var hash = 0;
        foreach (var ch in id) hash = unchecked(hash * 31 + ch);
        return AvatarColors[Math.Abs(hash % AvatarColors.Length)];
    }

    public static string ItemIcon(ItemDef item) => item.Type switch
    {
        ItemType.Consumable => Ico.FlaskConical,
        ItemType.Weapon => Ico.Sword,
        ItemType.Armor => item.Slot switch
        {
            EquipSlot.Head => Ico.HardHat,
            EquipSlot.Hands => Ico.Hand,
            EquipSlot.Legs => Ico.PersonStanding,
            EquipSlot.Feet => Ico.Footprints,
            EquipSlot.Accessory => Ico.Gem,
            EquipSlot.Shield => Ico.Shield,
            _ => Ico.Shirt,
        },
        ItemType.Relic => Ico.Wine,
        _ => Ico.ScrollText,
    };
}

/// <summary>
/// Pierre sombre fixe : bandeaux, barre d'onglets, combat, cinématiques, cartes sombres.
/// Ces éléments sont sombres dans les deux modes (le mode sombre ne les inverse pas).
/// </summary>
public static class Night
{
    public static readonly Color Stone950 = Color.FromArgb("#0C0A09");
    public static readonly Color Stone900 = Color.FromArgb("#1C1917");
    public static readonly Color Stone800 = Color.FromArgb("#292524");
    public static readonly Color Stone700 = Color.FromArgb("#44403C");
    public static readonly Color Stone600 = Color.FromArgb("#57534E");
    public static readonly Color Stone500 = Color.FromArgb("#78716C");
    public static readonly Color Stone400 = Color.FromArgb("#A8A29E");
    public static readonly Color Stone300 = Color.FromArgb("#D6D3D1");
    public static readonly Color Stone200 = Color.FromArgb("#E7E5E4");
    public static readonly Color Stone100 = Color.FromArgb("#F5F5F4");
    public static readonly Color Stone50 = Color.FromArgb("#FAFAF9");
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

    /// <summary>Petit libellé en capitales très espacées (signature visuelle de Service Impérial).</summary>
    public static Label Caps(string text, double size = 10, Color? color = null) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = size,
        FontAttributes = FontAttributes.Bold,
        TextColor = color ?? Theme.Stone400,
        CharacterSpacing = 2.5,
        LineBreakMode = LineBreakMode.WordWrap,
    };

    /// <summary>Titre à empattements, en capitales.</summary>
    public static Label Serif(string text, double size = 22, Color? color = null) => new()
    {
        Text = text.ToUpperInvariant(),
        FontFamily = "serif",
        FontSize = size,
        FontAttributes = FontAttributes.Bold,
        TextColor = color ?? Theme.Stone900,
        CharacterSpacing = 1.5,
        LineBreakMode = LineBreakMode.WordWrap,
    };

    public static Label Heading(string text) => Serif(text, 24);

    /// <summary>Icône Lucide.</summary>
    public static Label Icon(string glyph, double size = 18, Color? color = null) => new()
    {
        Text = glyph,
        FontFamily = Ico.Font,
        FontSize = size,
        TextColor = color ?? Theme.Stone900,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
    };

    /// <summary>Icône + libellé en capitales sur une ligne.</summary>
    public static View IconCaps(string glyph, string text, Color? color = null, double size = 10) => new HorizontalStackLayout
    {
        Spacing = 6,
        Children = { Icon(glyph, size + 3, color ?? Theme.Stone400), Caps(text, size, color ?? Theme.Stone400) },
    };

    /// <summary>En-tête de page : icône dorée, grand titre serif, sous-titre espacé, trait épais.</summary>
    public static View PageHeader(string glyph, string title, string subtitle)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12,
        };
        grid.Add(Icon(glyph, 34, Theme.Gold600), 0, 0);
        grid.Add(new VerticalStackLayout { Spacing = 2, Children = { Serif(title, 24), Caps(subtitle, 10, Theme.Stone500) } }, 1, 0);
        return new VerticalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(0, 0, 0, 4),
            Children = { grid, new BoxView { HeightRequest = 4, Color = Theme.Stone800 } },
        };
    }

    /// <summary>Titre de section : libellé en capitales et filet.</summary>
    public static View Section(string text)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 10,
            Margin = new Thickness(0, 10, 0, 0),
        };
        grid.Add(Caps(text, 10, Theme.Stone500), 0, 0);
        grid.Add(new BoxView { HeightRequest = 1, Color = Theme.Stone300, VerticalOptions = LayoutOptions.Center }, 1, 0);
        return grid;
    }

    public static BoxView GoldLine(double height = 3) => new() { HeightRequest = height, Background = Theme.GoldLine };

    // ------------------------------------------------------------------ Boutons

    /// <summary>Bouton secondaire (blanc, liseré pierre) ; pierre / or quand il est sélectionné.</summary>
    public static Button Btn(string text, Action onClick, bool enabled = true, bool selected = false)
    {
        var button = new Button
        {
            Text = text.ToUpperInvariant(),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.5,
            TextColor = selected ? Theme.Gold500 : Theme.Stone800,
            BackgroundColor = selected ? Night.Stone900 : Theme.Surface,
            // En mode sombre, le bouton pierre se détache du fond par un liseré or.
            BorderColor = selected ? (Theme.Dark ? Theme.Gold700 : Night.Stone900) : Theme.Stone300,
            BorderWidth = 1,
            CornerRadius = 12,
            Padding = new Thickness(12, 8),
            MinimumHeightRequest = 44,
            IsEnabled = enabled,
            Opacity = enabled ? 1 : 0.45,
        };
        button.Clicked += (_, _) => onClick();
        Interactive.Attach(button);
        return button;
    }

    /// <summary>Bouton principal : pierre sombre, texte or, capitales espacées.</summary>
    public static Button Primary(string text, Action onClick, bool enabled = true)
    {
        var button = Btn(text, onClick, enabled, selected: true);
        button.FontSize = 13;
        button.CharacterSpacing = 3;
        button.MinimumHeightRequest = 54;
        button.Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 4), Radius = 10, Opacity = 0.3f };
        return button;
    }

    /// <summary>Petit bouton de navigation (ex : « ◂ PAYS »).</summary>
    public static Button Pill(string text, Action onClick)
    {
        var button = Btn(text, onClick);
        button.FontSize = 11;
        button.CornerRadius = 20;
        button.MinimumHeightRequest = 36;
        button.Padding = new Thickness(16, 4);
        button.HorizontalOptions = LayoutOptions.Start;
        return button;
    }

    /// <summary>Rend n'importe quel élément cliquable.</summary>
    public static T OnTap<T>(T view, Action action) where T : View
    {
        view.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => { Interactive.Flash(view); action(); }) });
        Interactive.Attach(view);
        return view;
    }

    // ------------------------------------------------------------------ Cartes

    public static Border Panel(View content, Color? background = null) => Card(content, background);

    /// <summary>Carte blanche, liseré pierre clair, coins arrondis, ombre légère.</summary>
    public static Border Card(View content, Color? background = null, Color? stroke = null, double radius = 12) => new()
    {
        Content = content,
        BackgroundColor = background ?? Theme.Surface,
        Stroke = stroke ?? Theme.Stone200,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = radius },
        Padding = new Thickness(14, 12),
        Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 2), Radius = 6, Opacity = 0.08f },
    };

    /// <summary>Carte avec bandeau de titre (comme les « Card » de Service Impérial).</summary>
    public static Border TitledCard(string glyph, string title, View content, View? headerRight = null)
    {
        var header = new Grid
        {
            BackgroundColor = Theme.Stone50,
            Padding = new Thickness(12, 9),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        header.Add(IconCaps(glyph, title, Theme.Stone500), 0, 0);
        if (headerRight is not null) header.Add(headerRight, 1, 0);

        var body = new ContentView { Content = content, Padding = new Thickness(14, 12) };
        var card = Card(new VerticalStackLayout
        {
            Spacing = 0,
            Children = { header, new BoxView { HeightRequest = 1, Color = Theme.Stone200 }, body },
        });
        card.Padding = 0;
        return card;
    }

    /// <summary>Carte sombre en pierre, avec grande icône en filigrane (bannières, trésor).</summary>
    public static Border DarkCard(View content, string? watermark = null, Color? watermarkColor = null, bool goldLine = false)
    {
        var grid = new Grid();
        if (watermark is not null)
        {
            var mark = Icon(watermark, 120, watermarkColor ?? Colors.White);
            mark.Opacity = 0.08;
            mark.HorizontalOptions = LayoutOptions.End;
            mark.VerticalOptions = LayoutOptions.End;
            mark.Margin = new Thickness(0, 0, -18, -26);
            grid.Add(mark);
        }
        var inner = new VerticalStackLayout { Spacing = 0 };
        if (goldLine) inner.Add(GoldLine());
        inner.Add(new ContentView { Content = content, Padding = new Thickness(18, 16) });
        grid.Add(inner);

        return new Border
        {
            Content = grid,
            Background = Theme.DarkBackground,
            Stroke = Theme.Dark ? Night.Stone700 : Night.Stone800,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 0,
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 6), Radius = 14, Opacity = 0.35f },
        };
    }

    /// <summary>Ancien nom : carte sombre (les couleurs passées sont ignorées pour garder la DA).</summary>
    public static Border GradientCard(View content, Color from, Color to, double radius = 20) => DarkCard(content);

    /// <summary>Emblème rond : pierre, anneau épais, icône dorée.</summary>
    public static View Emblem(string glyph, double size = 80, Color? color = null) => new Border
    {
        WidthRequest = size,
        HeightRequest = size,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        StrokeShape = new Ellipse(),
        Stroke = Night.Stone700,
        StrokeThickness = 4,
        BackgroundColor = Night.Stone800,
        Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 4), Radius = 12, Opacity = 0.5f },
        Content = Icon(glyph, size * 0.45, color ?? Theme.Gold500),
    };

    /// <summary>Tuile cliquable : pastille d'icône, titre en capitales, sous-titre.</summary>
    public static View Tile(string glyph, string title, string subtitle, Action? onTap, Color? tint = null, bool enabled = true)
    {
        var accent = tint ?? Theme.Stone800;
        var badge = new Border
        {
            WidthRequest = 54,
            HeightRequest = 54,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new Ellipse(),
            StrokeThickness = 1,
            Stroke = accent.WithAlpha(0.3f),
            BackgroundColor = accent.WithAlpha(0.1f),
            Content = Icon(glyph, 24, accent),
        };
        var stack = new VerticalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                badge,
                new Label
                {
                    Text = title.ToUpperInvariant(), FontSize = 12, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1.5,
                    TextColor = Theme.Stone900, HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap,
                },
                new Label
                {
                    Text = subtitle, FontSize = 11, TextColor = Theme.Stone500,
                    HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap,
                },
            },
        };
        var card = Card(stack, stroke: tint is null ? Theme.Stone200 : tint.WithAlpha(0.35f));
        card.MinimumHeightRequest = 140;
        card.Opacity = enabled ? 1 : 0.4;
        if (onTap is not null && enabled) OnTap(card, onTap);
        return card;
    }

    /// <summary>Grille de tuiles (deux colonnes par défaut).</summary>
    public static Grid TileGrid(IReadOnlyList<View> tiles, int columns = 2)
    {
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < tiles.Count; i++)
        {
            if (i % columns == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Add(tiles[i], i % columns, i / columns);
        }
        return grid;
    }

    /// <summary>Avatar rond : pierre, initiale, anneau de couleur.</summary>
    public static View Avatar(string name, Color color, double size = 52)
    {
        var initial = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();
        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            StrokeShape = new Ellipse(),
            Stroke = color,
            StrokeThickness = 2,
            BackgroundColor = Night.Stone800,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = initial,
                FontFamily = "serif",
                FontSize = size * 0.42,
                FontAttributes = FontAttributes.Bold,
                TextColor = Night.Stone300,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
            },
        };
    }

    /// <summary>Petite étiquette (ex : « NV 3 », « BOSS »).</summary>
    public static View Badge(string text, Color color) => new Border
    {
        BackgroundColor = color.WithAlpha(0.12f),
        Stroke = color.WithAlpha(0.45f),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 6 },
        Padding = new Thickness(7, 2),
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Center,
        Content = new Label
        {
            Text = text.ToUpperInvariant(), FontSize = 9, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1.5, TextColor = color,
        },
    };

    /// <summary>Jauge : "PV ▓▓▓▓░░ 45/60". <paramref name="dark"/> pour les fonds en pierre.</summary>
    public static View Bar(string label, int value, int max, Color color, double height = 8, bool dark = false)
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
        fill.Add(new BoxView { Color = color, CornerRadius = height / 2, IsVisible = pct > 0 }, 0, 0);
        var track = new Border
        {
            HeightRequest = height,
            BackgroundColor = dark ? Night.Stone700 : Theme.Stone200,
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
                new ColumnDefinition(new GridLength(26)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(62)),
            },
            ColumnSpacing = 8,
        };
        var name = Caps(label, 9, color);
        name.VerticalOptions = LayoutOptions.Center;
        grid.Add(name, 0, 0);
        grid.Add(track, 1, 0);
        var numbers = Txt($"{value}/{max}", 11, dark ? Night.Stone300 : Theme.Stone600, bold: true);
        numbers.HorizontalTextAlignment = TextAlignment.End;
        numbers.VerticalOptions = LayoutOptions.Center;
        grid.Add(numbers, 2, 0);
        return grid;
    }

    /// <summary>
    /// Jauge animée : la barre glisse de l'ancienne valeur vers la nouvelle.
    /// Sur une perte, une traînée claire reste un instant avant de rattraper la barre (effet « dégâts »).
    /// </summary>
    public static View AnimatedBar(string label, int from, int to, int max, Color color, double height = 8, bool dark = false)
    {
        double Pct(int v) => max > 0 ? Math.Clamp((double)v / max, 0, 1) : 0;
        var pFrom = Pct(from);
        var pTo = Pct(to);
        var damage = to < from;

        var track = new AbsoluteLayout
        {
            HeightRequest = height,
            BackgroundColor = dark ? Night.Stone700 : Theme.Stone200,
            VerticalOptions = LayoutOptions.Center,
        };
        var ghost = new BoxView { Color = damage ? Color.FromArgb("#FDE68A") : color.WithAlpha(0.45f), CornerRadius = height / 2 };
        var fill = new BoxView { Color = color, CornerRadius = height / 2 };
        foreach (var box in new[] { ghost, fill })
        {
            AbsoluteLayout.SetLayoutFlags(box, AbsoluteLayoutFlags.All);
            track.Add(box);
        }
        AbsoluteLayout.SetLayoutBounds(ghost, new Rect(0, 0, damage ? pFrom : pTo, 1));
        AbsoluteLayout.SetLayoutBounds(fill, new Rect(0, 0, pFrom, 1));
        var clip = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = height / 2 },
            Padding = 0,
            HeightRequest = height,
            VerticalOptions = LayoutOptions.Center,
            Content = track,
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(26)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(62)),
            },
            ColumnSpacing = 8,
        };
        var name = Caps(label, 9, color);
        name.VerticalOptions = LayoutOptions.Center;
        grid.Add(name, 0, 0);
        grid.Add(clip, 1, 0);
        var numbers = Txt($"{from}/{max}", 11, dark ? Night.Stone300 : Theme.Stone600, bold: true);
        numbers.HorizontalTextAlignment = TextAlignment.End;
        numbers.VerticalOptions = LayoutOptions.Center;
        grid.Add(numbers, 2, 0);

        if (from != to)
        {
            grid.Loaded += (_, _) =>
            {
                new Animation(v => AbsoluteLayout.SetLayoutBounds(fill, new Rect(0, 0, v, 1)), pFrom, pTo)
                    .Commit(fill, "bar", 16, damage ? 380u : 650u, Easing.CubicOut);
                new Animation(v => numbers.Text = $"{(int)Math.Round(v)}/{max}", from, to)
                    .Commit(numbers, "count", 16, 500, Easing.CubicOut, (_, _) => numbers.Text = $"{to}/{max}");
                if (damage)
                {
                    // La traînée claire attend puis rattrape la barre.
                    var trail = new Animation
                    {
                        { 0.45, 1, new Animation(v => AbsoluteLayout.SetLayoutBounds(ghost, new Rect(0, 0, v, 1)), pFrom, pTo, Easing.CubicIn) },
                    };
                    trail.Commit(ghost, "trail", 16, 1100);
                }
            };
        }
        else
        {
            numbers.Text = $"{to}/{max}";
        }
        return grid;
    }

    /// <summary>Petite secousse (quand un combattant encaisse un coup).</summary>
    public static void Shake(VisualElement view)
    {
        view.Loaded += async (_, _) =>
        {
            for (var i = 0; i < 3; i++)
            {
                await view.TranslateTo(-6, 0, 45);
                await view.TranslateTo(6, 0, 45);
            }
            await view.TranslateTo(0, 0, 45);
        };
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

    /// <summary>Pastille d'icône carrée (fond pierre clair) pour les listes.</summary>
    public static View IconBox(string glyph, Color? color = null, double size = 44) => new Border
    {
        WidthRequest = size,
        HeightRequest = size,
        BackgroundColor = Theme.Stone100,
        Stroke = Theme.Stone200,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 10 },
        Content = Icon(glyph, size * 0.45, color ?? Theme.Stone700),
    };

    /// <summary>Cellule de statistique : icône, valeur (serif), libellé.</summary>
    public static View StatCell(string glyph, string label, int value, Color color)
    {
        var card = Card(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                Icon(glyph, 16, color),
                new Label
                {
                    Text = value.ToString(), FontFamily = "serif", FontSize = 22, FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Stone900, HorizontalTextAlignment = TextAlignment.Center,
                },
                new Label
                {
                    Text = label.ToUpperInvariant(), FontSize = 9, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1.5,
                    TextColor = Theme.Stone400, HorizontalTextAlignment = TextAlignment.Center,
                },
            },
        });
        card.Padding = new Thickness(6, 10);
        return card;
    }

    /// <summary>Petite pastille « chargée » sombre pour les chiffres importants (ex : or, niveau).</summary>
    public static View DarkStat(string glyph, string label, string value)
    {
        return DarkCard(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                Caps(label, 10, Night.Stone400),
                new Label { Text = value, FontFamily = "serif", FontSize = 34, FontAttributes = FontAttributes.Bold, TextColor = Theme.Gold500 },
            },
        }, glyph, Theme.Gold500);
    }

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
        EquipSlot.Armor => "Corps",
        EquipSlot.Head => "Tête",
        EquipSlot.Hands => "Mains",
        EquipSlot.Legs => "Jambes",
        EquipSlot.Feet => "Pieds",
        EquipSlot.Accessory => "Accessoire",
        EquipSlot.Shield => "Bouclier",
        _ => "Relique",
    };

    public static string SlotIcon(EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => Ico.Sword,
        EquipSlot.Armor => Ico.Shirt,
        EquipSlot.Head => Ico.HardHat,
        EquipSlot.Hands => Ico.Hand,
        EquipSlot.Legs => Ico.PersonStanding,
        EquipSlot.Feet => Ico.Footprints,
        EquipSlot.Accessory => Ico.Gem,
        EquipSlot.Shield => Ico.Shield,
        _ => Ico.Wine,
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
