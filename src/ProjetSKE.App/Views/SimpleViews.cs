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
        (EncyclopediaCategory.Characters, Ico.Users, "Personnages"),
        (EncyclopediaCategory.Monsters, Ico.Skull, "Monstres"),
        (EncyclopediaCategory.Locations, Ico.Castle, "Lieux"),
        (EncyclopediaCategory.Weapons, Ico.Sword, "Armes"),
        (EncyclopediaCategory.Relics, Ico.Gem, "Reliques"),
    ];

    public EncyclopediaView(GamePage page)
    {
        var s = page.Session;
        var stack = new VerticalStackLayout { Spacing = 14 };
        var total = Categories.Sum(c => s.GetEncyclopedia(c.Category).Count);
        stack.Add(PageHeader(Ico.Library, "Encyclopédie", $"{total} entrée(s) découverte(s)"));

        var tiles = Categories.Select(c =>
        {
            var selected = page.EncyclopediaCategory == c.Category;
            var count = s.GetEncyclopedia(c.Category).Count;
            var tile = Card(new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    Icon(c.Icon, 20, selected ? Theme.Gold500 : Theme.Stone600),
                    new Label
                    {
                        Text = count.ToString(), FontFamily = "serif", FontSize = 15, FontAttributes = FontAttributes.Bold,
                        TextColor = selected ? Theme.Stone100 : Theme.Stone900, HorizontalTextAlignment = TextAlignment.Center,
                    },
                },
            }, selected ? Theme.Stone900 : Colors.White, selected ? Theme.Stone900 : Theme.Stone200, 10);
            tile.Padding = new Thickness(2, 8);
            return (View)OnTap(tile, () => { page.EncyclopediaCategory = c.Category; page.Render(); });
        }).ToList();
        stack.Add(TileGrid(tiles, 5));

        var current = Categories.First(c => c.Category == page.EncyclopediaCategory);
        var entries = s.GetEncyclopedia(page.EncyclopediaCategory);
        var list = new VerticalStackLayout { Spacing = 14 };
        if (entries.Count == 0) list.Add(Muted("Rien de rencontré pour l'instant.", 14));
        foreach (var (name, subtitle, description) in entries)
        {
            list.Add(IconRow(IconBox(current.Icon), new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    Txt(name, 15, Theme.Stone900, bold: true),
                    Caps(subtitle, 9, Theme.Stone500),
                    Txt(description, 13, Theme.Stone600),
                },
            }));
        }
        stack.Add(TitledCard(current.Icon, current.Label, list, Badge(entries.Count.ToString(), Theme.Stone500)));
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
        var stack = new VerticalStackLayout { Spacing = 14 };
        stack.Add(PageHeader(Ico.Store, $"Boutique", page.ShopSelling ? "Le marchand rachète à moitié prix" : s.CurrentLocation.Name));

        stack.Add(ButtonRow(
            Btn("Acheter", () => { page.ShopSelling = false; page.Render(); }, selected: !page.ShopSelling),
            Btn("Vendre", () => { page.ShopSelling = true; page.Render(); }, selected: page.ShopSelling)));

        var list = new VerticalStackLayout { Spacing = 14 };
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
                        Txt(item.Name, 15, Theme.Stone900, bold: true),
                        Caps(ItemSummary(item) + (owned > 0 ? $" · possédé : {owned}" : ""), 9, Theme.Stone500),
                        Txt(item.Description, 12, Theme.Stone500),
                    },
                };
                var buy = Btn($"{item.Price} or", () =>
                {
                    page.Notify(s.Buy(id) ? $"{item.Name} acheté." : "Pas assez d'or.");
                    page.Render();
                }, enabled: !(item.IsUnique && s.OwnsItem(id)), selected: true);
                list.Add(IconRow(IconBox(Theme.ItemIcon(item)), info, buy));
            }
            stack.Add(TitledCard(Ico.ShoppingBag, "Étal du marchand", list));
        }
        else
        {
            var sellable = s.Bag().Where(b => b.Item.IsSellable).ToList();
            if (sellable.Count == 0) list.Add(Muted("Rien à vendre. Les objets équipés doivent d'abord être retirés au camp.", 13));
            foreach (var (item, count) in sellable)
            {
                var id = item.Id;
                var info = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { Txt($"{item.Name}  x{count}", 15, Theme.Stone900, bold: true), Caps(ItemSummary(item), 9, Theme.Stone500) },
                };
                var sell = Btn($"+{s.SellPrice(item)} or", () =>
                {
                    if (s.Sell(id)) page.Notify($"{item.Name} vendu.");
                    page.Render();
                });
                sell.TextColor = Theme.Green600;
                list.Add(IconRow(IconBox(Theme.ItemIcon(item)), info, sell));
            }
            stack.Add(TitledCard(Ico.Coins, "Revendre", list));
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
            Placeholder = "Page blanche… Note ici tes découvertes, tes pistes, tes plans.",
            PlaceholderColor = Theme.Stone400,
            TextColor = Theme.Stone900,
            FontFamily = "serif",
            BackgroundColor = Colors.Transparent,
            FontSize = 16,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 480,
        };
        editor.TextChanged += (_, e) => page.Session.State.Journal = e.NewTextValue ?? "";
        editor.Unfocused += (_, _) => page.AutoSave();

        var sheet = Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                IconCaps(Ico.NotebookPen, "Carnet de voyage", Theme.Stone500),
                new BoxView { HeightRequest = 1, Color = Theme.Stone200 },
                editor,
            },
        }, Theme.ParchmentLight, Theme.Stone300);
        sheet.Padding = new Thickness(18, 14);

        Content = new VerticalStackLayout
        {
            Spacing = 14,
            Children = { PageHeader(Ico.Feather, "Journal", "Tes notes personnelles"), sheet },
        };
    }
}

/// <summary>Menu : sauvegarde, paramètres de jeu, retour au titre, outils de test.</summary>
public sealed class MenuView : ContentView
{
    public MenuView(GamePage page)
    {
        var s = page.Session;
        var config = s.Config;
        var stack = new VerticalStackLayout { Spacing = 14 };
        stack.Add(PageHeader(Ico.Settings, "Menu", page.IsTestGame ? "Partie de test" : $"Emplacement {page.Slot + 1}"));

        stack.Add(TitledCard(Ico.Save, "Sauvegarde", Stack(
            Muted(page.IsTestGame ? "Partie de test : jamais sauvegardée." : "La partie est aussi sauvegardée automatiquement.", 13),
            Primary("Sauvegarder", () =>
            {
                page.AutoSave();
                page.Notify("Partie sauvegardée.");
                page.Render();
            }, enabled: !page.IsTestGame))));

        var settings = new VerticalStackLayout { Spacing = 12 };
        settings.Add(Setting(Ico.Compass, "Combats en voyage", Describe(config.TravelEncounters), () => config.TravelEncounters = Next(config.TravelEncounters)));
        settings.Add(Setting(Ico.Skull, "En cas de défaite", Describe(config.Defeat), () => config.Defeat = Next(config.Defeat)));
        settings.Add(Setting(Ico.Footprints, "Fuite", Describe(config.Flee), () => config.Flee = Next(config.Flee)));
        settings.Add(Setting(Ico.Feather, "Vitesse du texte", TextSpeed.Name, TextSpeed.Next));
        settings.Add(Muted("Touchez une valeur pour la changer. Les boss empêchent toujours la fuite.", 11));
        stack.Add(TitledCard(Ico.SlidersHorizontal, "Paramètres", settings));

        stack.Add(TitledCard(Ico.Sparkles, "Version du jeu", Stack(
            Txt(Updates.CurrentVersion, 13, Theme.Stone700),
            Muted(Updates.Status.Length > 0 ? Updates.Status : "Les nouvelles versions sont publiées sur GitHub.", 12),
            ButtonRow(
                Btn("Vérifier", async () => { await Updates.CheckAsync(); page.Render(); }),
                Btn("Installer", async () =>
                {
                    if (Updates.Latest is not { } release) return;
                    page.Notify("Téléchargement de la mise à jour…");
                    page.Render();
                    page.Notify(await Updates.DownloadAndInstallAsync(release, new Progress<double>()));
                    page.Render();
                }, enabled: Updates.IsAvailable, selected: Updates.IsAvailable)))));

        if (SkeApp.DevUnlocked)
        {
            stack.Add(Btn(page.MenuShowDevTools ? "Masquer les outils de test" : "Outils de test", () =>
            {
                page.MenuShowDevTools = !page.MenuShowDevTools;
                page.Render();
            }));
            if (page.MenuShowDevTools) stack.Add(new Dev.DevToolsView(page));
        }

        stack.Add(Btn(page.IsTestGame ? "Quitter le test" : "Retour au titre", page.QuitToTitle));
        Content = stack;

        View Setting(string glyph, string label, string value, Action change)
        {
            var button = Btn(value, () => { change(); page.Render(); }, selected: true);
            button.FontSize = 11;
            return IconRow(IconBox(glyph), Txt(label, 14, Theme.Stone900, bold: true), button);
        }
    }
}
