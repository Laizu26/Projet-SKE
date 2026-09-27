using Microsoft.Maui.Layouts;
using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Carte : on est d'abord dans le lieu actuel (ville, nature, donjon) ;
/// la flèche « Pays » ouvre la vue du pays pour voyager.
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

        var banner = GradientCard(new VerticalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                Icon(style.Icon, 64),
                new Label
                {
                    Text = loc.Name, FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = Theme.AccentLight,
                    HorizontalTextAlignment = TextAlignment.Center, CharacterSpacing = 1,
                },
                new Label
                {
                    Text = GameSession.LocationTypeName(loc.Type).ToUpperInvariant(), FontSize = 12, TextColor = Theme.Text,
                    HorizontalTextAlignment = TextAlignment.Center, CharacterSpacing = 3, Opacity = 0.8,
                },
                new Label
                {
                    Text = loc.Description, FontSize = 14, TextColor = Theme.Text, FontAttributes = FontAttributes.Italic,
                    HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 0),
                },
            },
        }, style.From, style.To);
        banner.MinimumHeightRequest = 240;
        stack.Add(banner);

        var tiles = new List<View>();
        if (s.InCity)
        {
            tiles.Add(Tile("🛏️", "Auberge", $"Repos complet · {loc.InnPrice} or", () =>
            {
                page.Notify(s.Rest() ? "💤 L'équipe est reposée." : "Pas assez d'or pour l'auberge.");
                page.AutoSave();
                page.Render();
            }));
            tiles.Add(Tile("🛒", "Boutique", "Acheter et vendre", () => page.SwitchTab(GameTab.Shop)));
        }

        if (s.PendingFixedBattle is { } fb)
        {
            var names = string.Join(", ", fb.MonsterIds.Distinct().Where(s.Db.Monsters.ContainsKey).Select(id => s.Db.Monsters[id].Name));
            tiles.Add(Tile("💀", "Affronter", names, () => page.StartFixedBattle(fb), Theme.Danger));
        }
        if (loc.RandomEncounters.Count > 0)
        {
            tiles.Add(Tile("⚔️", "Explorer", "Chercher le combat", () =>
            {
                if (s.Explore() is { } monsters) page.StartBattle(monsters);
            }));
        }

        foreach (var npc in s.VisibleNpcs)
        {
            var npcId = npc.Id;
            var subtitle = npc.Description.Length > 0 ? npc.Description : "Parler";
            tiles.Add(Tile("🗣️", npc.Name, subtitle, () => page.TalkTo(npcId), Theme.AccentLight));
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

        stack.Add(GradientCard(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Icon("🗺️", 48),
                new Label
                {
                    Text = s.Db.Content.Title, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Theme.AccentLight,
                    HorizontalTextAlignment = TextAlignment.Center,
                },
                new Label
                {
                    Text = $"Vous êtes à {loc.Name}. Où aller ?", FontSize = 14, TextColor = Theme.Text,
                    HorizontalTextAlignment = TextAlignment.Center,
                },
            },
        }, Color.FromArgb("#2B3A63"), Color.FromArgb("#111827")));

        stack.Add(Section("Destinations"));
        if (s.Destinations.Count == 0) stack.Add(Card(Muted("Aucune route ne part d'ici.", 14)));
        foreach (var dest in s.Destinations)
        {
            var style = Theme.LocationStyle(dest.Type);
            var open = s.CanEnter(dest);
            var visited = s.State.SeenLocations.Contains(dest.Id);
            var id = dest.Id;

            var iconCircle = new Border
            {
                WidthRequest = 60,
                HeightRequest = 60,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                Stroke = Theme.Accent.WithAlpha(0.5f),
                StrokeThickness = 1,
                Background = Theme.Diagonal(style.From, style.To),
                Content = Icon(open ? style.Icon : "🔒", 28),
            };
            var info = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    Txt(dest.Name, 17, open ? Theme.Text : Theme.Muted, bold: true),
                    Muted(GameSession.LocationTypeName(dest.Type) + (visited ? "" : " · inconnu") + (open ? "" : " · bloqué"), 12),
                },
            };
            var go = Txt(open ? "➜" : "", 26, Theme.Accent, bold: true);
            var card = Card(IconRow(iconCircle, info, go));
            card.MinimumHeightRequest = 90;
            stack.Add(OnTap(card, () => page.Travel(id)));
        }

        stack.Add(Section("Lieux connus"));
        var chips = new FlexLayout { Wrap = FlexWrap.Wrap, JustifyContent = FlexJustify.Start };
        foreach (var known in s.State.SeenLocations.Where(s.Db.Locations.ContainsKey))
        {
            var l = s.Db.Locations[known];
            var chip = Badge($"{Theme.LocationStyle(l.Type).Icon} {l.Name}", known == loc.Id ? Theme.Accent : Theme.Muted);
            chip.Margin = new Thickness(0, 0, 6, 6);
            chips.Add(chip);
        }
        stack.Add(chips);
        return stack;
    }
}
