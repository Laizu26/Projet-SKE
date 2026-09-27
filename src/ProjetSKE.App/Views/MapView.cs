using Microsoft.Maui.Layouts;
using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Carte : on est d'abord dans le lieu actuel (ville, nature, donjon) ;
/// le bouton « ◂ Pays » ouvre la vue du pays pour voyager.
/// </summary>
public sealed class MapView : ContentView
{
    public MapView(GamePage page)
    {
        Content = page.MapShowCountry ? BuildCountry(page) : BuildLocal(page);
    }

    // ------------------------------------------------------------------ Le lieu actuel

    private static View BuildLocal(GamePage page)
    {
        var s = page.Session;
        var loc = s.CurrentLocation;
        var style = Theme.LocationStyle(loc.Type);
        var stack = new VerticalStackLayout { Spacing = 14 };

        stack.Add(Pill("◂  Pays", () => { page.MapShowCountry = true; page.Render(); }));

        // Bannière du lieu : carte en pierre, filet d'or, grande icône en filigrane.
        var banner = DarkCard(new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(0, 10),
            Children =
            {
                Emblem(style.Icon, 84, style.Accent),
                new Label
                {
                    Text = loc.Name.ToUpperInvariant(), FontFamily = "serif", FontSize = 26, FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Stone100, CharacterSpacing = 3, HorizontalTextAlignment = TextAlignment.Center,
                },
                new Label
                {
                    Text = GameSession.LocationTypeName(loc.Type).ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold,
                    TextColor = style.Accent, CharacterSpacing = 4, HorizontalTextAlignment = TextAlignment.Center,
                },
                new Label
                {
                    Text = loc.Description, FontSize = 14, FontAttributes = FontAttributes.Italic, TextColor = Theme.Stone400,
                    HorizontalTextAlignment = TextAlignment.Center,
                },
            },
        }, style.Icon, style.Accent, goldLine: true);
        banner.MinimumHeightRequest = 250;
        stack.Add(banner);

        var tiles = new List<View>();
        if (s.InCity)
        {
            tiles.Add(Tile(Ico.Bed, "Auberge", $"Repos complet · {loc.InnPrice} or", () =>
            {
                page.Notify(s.Rest() ? "L'équipe est reposée." : "Pas assez d'or pour l'auberge.");
                page.AutoSave();
                page.Render();
            }, Theme.Blue600));
            tiles.Add(Tile(Ico.Store, "Boutique", "Acheter et vendre", () => page.SwitchTab(GameTab.Shop), Theme.Gold600));
        }

        if (s.PendingFixedBattle is { } fb)
        {
            var names = string.Join(", ", fb.MonsterIds.Distinct().Where(s.Db.Monsters.ContainsKey).Select(id => s.Db.Monsters[id].Name));
            tiles.Add(Tile(Ico.Skull, "Affronter", names, () => page.StartFixedBattle(fb), Theme.Red600));
        }
        if (loc.RandomEncounters.Count > 0)
        {
            tiles.Add(Tile(Ico.Swords, "Explorer", "Chercher le combat", () =>
            {
                if (s.Explore() is { } monsters) page.StartBattle(monsters);
            }, Theme.Stone800));
        }

        foreach (var npc in s.VisibleNpcs)
        {
            var npcId = npc.Id;
            var subtitle = npc.Description.Length > 0 ? npc.Description : "Parler";
            tiles.Add(Tile(Ico.MessageCircle, npc.Name, subtitle, () => page.TalkTo(npcId), Theme.Gold700));
        }

        if (tiles.Count > 0)
        {
            stack.Add(Section(s.InCity ? "En ville" : "Sur place"));
            stack.Add(TileGrid(tiles));
        }
        else
        {
            stack.Add(Card(Muted("Rien à faire ici. Touche « Pays » pour partir.", 14)));
        }
        return stack;
    }

    // ------------------------------------------------------------------ Le pays

    private static View BuildCountry(GamePage page)
    {
        var s = page.Session;
        var loc = s.CurrentLocation;
        var stack = new VerticalStackLayout { Spacing = 14 };

        stack.Add(Pill($"◂  Retour à {loc.Name}", () => { page.MapShowCountry = false; page.Render(); }));
        stack.Add(PageHeader(Ico.Map, s.Db.Content.Title, $"Vous êtes à {loc.Name}"));

        stack.Add(Section("Destinations"));
        if (s.Destinations.Count == 0) stack.Add(Card(Muted("Aucune route ne part d'ici.", 14)));
        foreach (var dest in s.Destinations)
        {
            var style = Theme.LocationStyle(dest.Type);
            var open = s.CanEnter(dest);
            var visited = s.State.SeenLocations.Contains(dest.Id);
            var id = dest.Id;

            var info = new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    Txt(dest.Name, 17, open ? Theme.Stone900 : Theme.Stone400, bold: true),
                    Caps(GameSession.LocationTypeName(dest.Type) + (visited ? "" : " · inconnu") + (open ? "" : " · bloqué"), 9, Theme.Stone500),
                },
            };
            var icon = new Border
            {
                WidthRequest = 56,
                HeightRequest = 56,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                Stroke = Theme.Stone700,
                StrokeThickness = 3,
                BackgroundColor = Theme.Stone800,
                Content = Icon(open ? style.Icon : Ico.Lock, 24, open ? style.Accent : Theme.Stone500),
            };
            var go = Icon(open ? Ico.ChevronRight : "", 24, Theme.Gold600);
            var card = Card(IconRow(icon, info, go));
            card.MinimumHeightRequest = 88;
            stack.Add(OnTap(card, () => page.Travel(id)));
        }

        stack.Add(Section("Lieux connus"));
        var chips = new FlexLayout { Wrap = FlexWrap.Wrap, JustifyContent = FlexJustify.Start };
        foreach (var known in s.State.SeenLocations.Where(s.Db.Locations.ContainsKey))
        {
            var l = s.Db.Locations[known];
            var chip = Badge(l.Name, known == loc.Id ? Theme.Gold700 : Theme.Stone500);
            chip.Margin = new Thickness(0, 0, 6, 6);
            chips.Add(chip);
        }
        stack.Add(chips);
        return stack;
    }
}
