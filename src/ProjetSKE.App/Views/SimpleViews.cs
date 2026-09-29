using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
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
                        TextColor = selected ? Night.Stone100 : Theme.Stone900, HorizontalTextAlignment = TextAlignment.Center,
                    },
                },
            }, selected ? Night.Stone900 : Theme.Surface, selected ? (Theme.Dark ? Theme.Gold700 : Night.Stone900) : Theme.Stone200, 10);
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

/// <summary>
/// Journal : un carnet de pages de parchemin où le joueur écrit librement. Une page a une taille fixe : quand elle est
/// pleine, la suite passe sur la page suivante et la page se tourne. Flèches ◂ ▸ pour feuilleter (toucher et souris).
/// </summary>
public sealed class JournalView : ContentView
{
    private const double PageHeight = 430;
    private const double FontSize = 16;

    private readonly GamePage _page;
    private readonly List<string> _pages;
    private readonly Editor _editor;
    private readonly Border _sheet;
    private readonly Label _number;
    private readonly Label _counter;
    private readonly Button _prev;
    private readonly Button _next;
    private bool _updating;
    private bool _turning;

    public JournalView(GamePage page)
    {
        _page = page;
        _pages = Notebook.Pages(page.Session.State.Journal);
        page.JournalPage = Math.Clamp(page.JournalPage, 0, _pages.Count - 1);

        _editor = new Editor
        {
            Placeholder = "Page blanche… Note ici tes découvertes, tes pistes, tes plans.",
            PlaceholderColor = Theme.Stone400,
            TextColor = Theme.Stone900,
            FontFamily = "serif",
            BackgroundColor = Colors.Transparent,
            FontSize = FontSize,
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = PageHeight,
        };
        _editor.TextChanged += (_, e) => OnTextChanged(e.NewTextValue ?? "");
        _editor.Unfocused += (_, _) => page.AutoSave();

        _number = new Label { FontFamily = "serif", FontSize = 12, FontAttributes = FontAttributes.Italic, TextColor = Theme.Stone400, HorizontalTextAlignment = TextAlignment.Center };
        _sheet = Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                IconCaps(Ico.NotebookPen, "Carnet de voyage", Theme.Stone500),
                new BoxView { HeightRequest = 1, Color = Theme.Stone200 },
                _editor,
                _number,
            },
        }, Theme.ParchmentLight, Theme.Stone300);
        _sheet.Padding = new Thickness(18, 14);

        _prev = Btn("◂", () => Turn(-1));
        _next = Btn("▸", () => Turn(+1));
        _counter = Caps("", 10, Theme.Stone500);
        _counter.HorizontalTextAlignment = TextAlignment.Center;
        var nav = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(64)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(64)) },
            ColumnSpacing = 8,
        };
        nav.Add(_prev, 0, 0);
        _counter.VerticalOptions = LayoutOptions.Center;
        nav.Add(_counter, 1, 0);
        nav.Add(_next, 2, 0);

        Content = new VerticalStackLayout
        {
            Spacing = 14,
            Children = { PageHeader(Ico.Feather, "Journal", "Tes notes personnelles"), _sheet, nav },
        };
        Show();
    }

    private int Index { get => _page.JournalPage; set => _page.JournalPage = value; }

    /// <summary>Caractères par ligne et lignes par page, d'après la largeur réelle de la page (estimation prudente).</summary>
    private (int Chars, int Lines) Capacity()
    {
        var width = _editor.Width > 0 ? _editor.Width : 300;
        var chars = (int)((width - 12) / (FontSize * 0.55));
        var lines = (int)((PageHeight - 16) / (FontSize * 1.4));
        return (Math.Max(8, chars), Math.Max(4, lines));
    }

    private void Show()
    {
        _updating = true;
        _editor.Text = _pages[Index];
        _updating = false;
        _number.Text = $"— {Index + 1} —";
        _counter.Text = $"Page {Index + 1} / {_pages.Count}";
        _prev.IsEnabled = Index > 0;
        _prev.Opacity = Index > 0 ? 1 : 0.45;
        // Au-delà de la dernière page : une page blanche (seulement si la dernière n'est pas vide).
        var canNext = Index < _pages.Count - 1 || _pages[Index].Length > 0;
        _next.IsEnabled = canNext;
        _next.Opacity = canNext ? 1 : 0.45;
    }

    private void Save() => _page.Session.State.Journal = Notebook.Join(_pages);

    private void OnTextChanged(string text)
    {
        if (_updating) return;
        _pages[Index] = text;
        var (chars, lines) = Capacity();
        var (fit, overflow) = Notebook.Split(text, chars, lines);
        if (overflow.Length == 0)
        {
            Save();
            _next.IsEnabled = true;
            _next.Opacity = 1;
            return;
        }
        // Page pleine : la suite passe en tête de la page suivante (et ainsi de suite si elle déborde à son tour).
        _pages[Index] = fit;
        var carry = overflow;
        for (var i = Index + 1; carry.Length > 0; i++)
        {
            if (i >= _pages.Count) _pages.Add("");
            var joined = _pages[i].Length == 0 ? carry : carry + (carry.EndsWith('\n') ? "" : " ") + _pages[i];
            (_pages[i], carry) = Notebook.Split(joined, chars, lines);
        }
        Save();
        var cursor = overflow.Length;
        _ = TurnAsync(+1, cursor);
    }

    private void Turn(int direction)
    {
        if (Index + direction < 0) return;
        if (Index + direction >= _pages.Count)
        {
            if (_pages[Index].Length == 0) return;
            _pages.Add("");
        }
        _ = TurnAsync(direction, null);
    }

    /// <summary>La page se tourne (rotation), puis la suivante (ou la précédente) s'affiche.</summary>
    private async Task TurnAsync(int direction, int? cursor)
    {
        if (_turning) return;
        _turning = true;
        try
        {
            _sheet.AnchorX = direction > 0 ? 0 : 1;
            await _sheet.RotateYToAsync(direction > 0 ? -90 : 90, 140, Easing.CubicIn);
            Index = Math.Clamp(Index + direction, 0, _pages.Count - 1);
            Show();
            _sheet.RotationY = direction > 0 ? 90 : -90;
            await _sheet.RotateYToAsync(0, 160, Easing.CubicOut);
        }
        finally
        {
            _sheet.RotationY = 0;
            _turning = false;
        }
        if (cursor is { } position)
        {
            // On continue d'écrire là où la phrase s'est arrêtée.
            _editor.Focus();
            _editor.CursorPosition = Math.Min(position, _editor.Text?.Length ?? 0);
        }
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
        settings.Add(Setting(Ico.Moon, "Mode sombre", Theme.Dark ? "Activé" : "Désactivé", () => Theme.SetDark(!Theme.Dark)));
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
