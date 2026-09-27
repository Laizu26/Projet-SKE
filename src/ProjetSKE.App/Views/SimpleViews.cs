using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Encyclopédie : seules les entrées rencontrées sont visibles.</summary>
public sealed class EncyclopediaView : ContentView
{
    private static readonly (EncyclopediaCategory Category, string Icon, string Label)[] Categories =
    [
        (EncyclopediaCategory.Characters, "👤", "Personnages"),
        (EncyclopediaCategory.Monsters, "👹", "Monstres"),
        (EncyclopediaCategory.Locations, "🏰", "Lieux"),
        (EncyclopediaCategory.Weapons, "⚔️", "Armes"),
        (EncyclopediaCategory.Relics, "💎", "Reliques"),
    ];

    public EncyclopediaView(GamePage page)
    {
        var s = page.Session;
        var stack = new VerticalStackLayout { Spacing = 12 };

        var tiles = Categories.Select(c =>
        {
            var selected = page.EncyclopediaCategory == c.Category;
            var count = s.GetEncyclopedia(c.Category).Count;
            var tile = Card(new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    Icon(c.Icon, 24),
                    new Label { Text = count.ToString(), FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = selected ? Theme.Bg : Theme.Text, HorizontalTextAlignment = TextAlignment.Center },
                },
            }, selected ? Theme.Accent : Theme.Surface, selected ? Theme.AccentLight : Theme.Stroke, 14);
            tile.Padding = new Thickness(4, 8);
            return (View)OnTap(tile, () => { page.EncyclopediaCategory = c.Category; page.Render(); });
        }).ToList();
        stack.Add(TileGrid(tiles, 5));

        var current = Categories.First(c => c.Category == page.EncyclopediaCategory);
        var entries = s.GetEncyclopedia(page.EncyclopediaCategory);
        stack.Add(Section($"{current.Label}  ·  {entries.Count} découvert(s)"));
        if (entries.Count == 0)
        {
            stack.Add(Card(new VerticalStackLayout
            {
                Spacing = 8,
                Padding = new Thickness(0, 30),
                Children =
                {
                    Icon("❔", 44),
                    new Label { Text = "Rien de rencontré pour l'instant.", FontSize = 15, TextColor = Theme.Muted, HorizontalTextAlignment = TextAlignment.Center },
                },
            }));
        }
        foreach (var (name, subtitle, description) in entries)
        {
            var info = new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    Txt(name, 17, Theme.AccentLight, bold: true),
                    Muted(subtitle, 12),
                    Txt(description, 14, Theme.Text),
                },
            };
            stack.Add(Card(IconRow(Icon(current.Icon, 30), info)));
        }
        Content = stack;
    }
}

/// <summary>Boutique de la ville : achat et revente.</summary>
public sealed class ShopView : ContentView
{
    public ShopView(GamePage page)
    {
        var s = page.Session;
        s.BrowseShop();
        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Add(GradientCard(IconRow(Icon("🛒", 40), new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                Txt($"Boutique de {s.CurrentLocation.Name}", 20, Theme.AccentLight, bold: true),
                Muted(page.ShopSelling ? "Le marchand rachète à moitié prix." : "Bienvenue, voyageur ! Jetez un œil.", 13),
            },
        }), Color.FromArgb("#4A3418"), Color.FromArgb("#1A130A")));

        stack.Add(ButtonRow(
            Btn("Acheter", () => { page.ShopSelling = false; page.Render(); }, selected: !page.ShopSelling),
            Btn("Vendre", () => { page.ShopSelling = true; page.Render(); }, selected: page.ShopSelling)));

        if (!page.ShopSelling)
        {
            foreach (var item in s.ShopStock)
            {
                var id = item.Id;
                var owned = s.OwnsCount(id);
                var info = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        Txt(item.Name, 16, Theme.Text, bold: true),
                        Muted(ItemSummary(item) + (owned > 0 ? $" · possédé : {owned}" : ""), 12),
                        Txt(item.Description, 13, Theme.Muted),
                    },
                };
                var buy = Btn($"{item.Price} or", () =>
                {
                    page.Notify(s.Buy(id) ? $"🛍️ {item.Name} acheté." : "Pas assez d'or.");
                    page.Render();
                }, enabled: !(item.IsUnique && s.OwnsItem(id)));
                buy.TextColor = Theme.AccentLight;
                stack.Add(Card(IconRow(Icon(Theme.ItemIcon(item), 32), info, buy)));
            }
        }
        else
        {
            var sellable = s.Bag().Where(b => b.Item.IsSellable).ToList();
            if (sellable.Count == 0)
                stack.Add(Card(Muted("Rien à vendre. Les objets équipés doivent d'abord être retirés au camp.", 14)));
            foreach (var (item, count) in sellable)
            {
                var id = item.Id;
                var info = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { Txt($"{item.Name}  x{count}", 16, Theme.Text, bold: true), Muted(ItemSummary(item), 12) },
                };
                var sell = Btn($"+{s.SellPrice(item)} or", () =>
                {
                    if (s.Sell(id)) page.Notify($"💰 {item.Name} vendu.");
                    page.Render();
                });
                sell.TextColor = Theme.Good;
                stack.Add(Card(IconRow(Icon(Theme.ItemIcon(item), 32), info, sell)));
            }
        }
        Content = stack;
    }
}

/// <summary>Journal : une page de parchemin où le joueur écrit librement.</summary>
public sealed class JournalView : ContentView
{
    public JournalView(GamePage page)
    {
        var editor = new Editor
        {
            Text = page.Session.State.Journal,
            Placeholder = "Page blanche... Note ici tes découvertes, tes pistes, tes plans.",
            PlaceholderColor = Color.FromArgb("#8C7A5A"),
            TextColor = Theme.Ink,
            BackgroundColor = Colors.Transparent,
            FontSize = 16,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 520,
        };
        editor.TextChanged += (_, e) => page.Session.State.Journal = e.NewTextValue ?? "";
        editor.Unfocused += (_, _) => page.AutoSave();

        var parchment = new Border
        {
            Background = Theme.Vertical(Theme.Parchment, Color.FromArgb("#D9C9A0")),
            Stroke = Color.FromArgb("#8C6A3A"),
            StrokeThickness = 2,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(18, 16),
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 6), Radius = 14, Opacity = 0.5f },
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text = "Carnet de voyage", FontSize = 20, FontAttributes = FontAttributes.Bold | FontAttributes.Italic,
                        TextColor = Theme.Ink, HorizontalTextAlignment = TextAlignment.Center,
                    },
                    new BoxView { HeightRequest = 1, Color = Color.FromArgb("#8C6A3A"), Margin = new Thickness(30, 0) },
                    editor,
                },
            },
        };
        Content = parchment;
    }
}

/// <summary>Menu : sauvegarde, paramètres de jeu, retour au titre, outils de test.</summary>
public sealed class MenuView : ContentView
{
    public MenuView(GamePage page)
    {
        var s = page.Session;
        var config = s.Config;
        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Add(Section("Partie"));
        stack.Add(Card(Stack(
            IconRow(Icon("💾", 30), Stack(
                Txt(page.IsTestGame ? "Partie de test" : $"Emplacement {page.Slot + 1}", 16, Theme.Text, bold: true),
                Muted(page.IsTestGame ? "Jamais sauvegardée." : "Sauvegarde automatique activée.", 12))),
            Primary("Sauvegarder", () =>
            {
                page.AutoSave();
                page.Notify(page.IsTestGame ? "Partie de test : rien n'est sauvegardé." : "💾 Partie sauvegardée.");
                page.Render();
            }, enabled: !page.IsTestGame))));

        stack.Add(Section("Paramètres"));
        stack.Add(Setting("🧭", "Combats en voyage", Describe(config.TravelEncounters), () => config.TravelEncounters = Next(config.TravelEncounters)));
        stack.Add(Setting("☠️", "En cas de défaite", Describe(config.Defeat), () => config.Defeat = Next(config.Defeat)));
        stack.Add(Setting("🏃", "Fuite", Describe(config.Flee), () => config.Flee = Next(config.Flee)));
        stack.Add(Muted("Touchez une valeur pour la changer. Les boss empêchent toujours la fuite.", 12));

        if (SkeApp.DevUnlocked)
        {
            stack.Add(Section("Mode développeur"));
            stack.Add(Btn(page.MenuShowDevTools ? "🛠️  Masquer les outils de test" : "🛠️  Outils de test", () =>
            {
                page.MenuShowDevTools = !page.MenuShowDevTools;
                page.Render();
            }));
            if (page.MenuShowDevTools) stack.Add(new Dev.DevToolsView(page));
        }

        stack.Add(new BoxView { HeightRequest = 10, Color = Colors.Transparent });
        stack.Add(Btn(page.IsTestGame ? "Quitter le test" : "🚪  Retour au titre", page.QuitToTitle));

        Content = stack;

        View Setting(string icon, string label, string value, Action change)
        {
            var button = Btn(value, () => { change(); page.Render(); });
            button.TextColor = Theme.AccentLight;
            return Card(IconRow(Icon(icon, 26), Txt(label, 15, Theme.Text, bold: true), button));
        }
    }
}
