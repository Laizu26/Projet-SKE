using Microsoft.Maui.Controls.Shapes;
using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Carte en hexagones, adaptée de Service Impérial : fil d'Ariane Royaume › Lieu,
/// tuiles colorées par type, position actuelle qui pulse, routes entre les lieux,
/// et dans le lieu une grille où sont posés les bâtiments, les habitants et le pion de l'équipe.
/// Un panneau contextuel sous la carte réagit à la tuile touchée.
/// </summary>
public sealed class MapView : ContentView
{
    // Teintes « terre » de la carte de Service Impérial.
    private static readonly Color CityFill = Color.FromArgb("#E9C46A");
    private static readonly Color WildFill = Color.FromArgb("#A3B18A");
    private static readonly Color DungeonFill = Color.FromArgb("#B5838D");
    // Brouillard, cases vides et bâtiments : parchemin clair, ou parchemin sombre en mode sombre.
    private static Color FogFill => Color.FromArgb(Theme.Dark ? "#3A3328" : "#E9DFC4");
    private static Color FogStroke => Color.FromArgb(Theme.Dark ? "#5A4F3C" : "#C9B98F");
    private static Color EmptyFill => Color.FromArgb(Theme.Dark ? "#2E2920" : "#E9DFC4");
    private static Color EmptyStroke => Color.FromArgb(Theme.Dark ? "#4A4234" : "#D8CBA3");
    private static Color BuildingFill => Color.FromArgb(Theme.Dark ? "#4A4030" : "#FFFBEB");
    private static readonly Color TileStroke = Color.FromArgb("#3F3A34");
    private static readonly Color Emerald = Color.FromArgb("#059669");

    private readonly GamePage _page;
    private GameSession S => _page.Session;

    public MapView(GamePage page)
    {
        _page = page;
        var stack = new VerticalStackLayout { Spacing = 14 };
        stack.Add(Header());
        // Carte du royaume verrouillée (prologue) : seulement la carte du lieu.
        if (S.IsLocked(UiFeature.WorldMap)) page.MapShowCountry = false;
        stack.Add(page.MapShowCountry ? BuildWorld() : BuildLocal());
        Content = stack;
    }

    private static Color FillOf(LocationType type) => type switch
    {
        LocationType.City => CityFill,
        LocationType.Dungeon => DungeonFill,
        _ => WildFill,
    };

    // ------------------------------------------------------------------ En-tête « Où je suis »

    private View Header()
    {
        var loc = S.CurrentLocation;
        var style = Theme.LocationStyle(loc.Type);
        return DarkCard(IconRow(
            Emblem(style.Icon, 52, style.Accent),
            new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    Caps("Où je suis", 9, Night.Stone500),
                    new Label { Text = loc.Name.ToUpperInvariant(), FontFamily = "serif", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Night.Stone100, CharacterSpacing = 2 },
                    new Label { Text = loc.Description, FontSize = 12, FontAttributes = FontAttributes.Italic, TextColor = Night.Stone400 },
                },
            }), style.Icon, style.Accent, goldLine: true);
    }

    /// <summary>Fil d'Ariane : [Royaume] › [Lieu] › [Sous-lieu]...</summary>
    private View Breadcrumb()
    {
        View Crumb(string glyph, string text, bool active, Action onTap)
        {
            var chip = new Border
            {
                BackgroundColor = active ? Night.Stone900 : Colors.Transparent,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(10, 6),
                Content = new HorizontalStackLayout
                {
                    Spacing = 5,
                    Children = { Icon(glyph, 12, active ? Theme.Amber500 : Night.Stone400), Caps(text, 9, active ? Theme.Amber500 : Night.Stone400) },
                },
            };
            return OnTap(chip, onTap);
        }

        var crumbs = new HorizontalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(8, 8),
        };
        if (!S.IsLocked(UiFeature.WorldMap))
            crumbs.Add(Crumb(Ico.Globe, S.Db.Content.World.CountryName, _page.MapShowCountry, () => { _page.MapShowCountry = true; _page.Render(); }));
        // Toucher un lieu qui contient le lieu actuel, c'est en sortir.
        var path = S.Db.PathOf(S.CurrentLocation);
        foreach (var loc in path)
        {
            var here = loc.Id == S.CurrentLocation.Id;
            var id = loc.Id;
            if (crumbs.Children.Count > 0) crumbs.Add(Icon(Ico.ChevronRight, 12, Theme.Stone300));
            crumbs.Add(Crumb(here ? Ico.MapPin : Ico.DoorOpen, loc.Name, here && !_page.MapShowCountry, () =>
            {
                if (here) { _page.MapShowCountry = false; _page.Render(); }
                else _page.Travel(id);
            }));
        }
        return new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = crumbs,
        };
    }

    /// <summary>Carte blanche : fil d'Ariane, carte sur fond parchemin, panneau contextuel.</summary>
    private View MapCard(View map, View panel)
    {
        var mapArea = new ContentView
        {
            Background = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.35),
                Radius = 0.9,
                GradientStops = { new GradientStop(Color.FromArgb(Theme.Dark ? "#2C261D" : "#F7F0DC"), 0f), new GradientStop(Color.FromArgb(Theme.Dark ? "#1C1813" : "#E6D6A8"), 1f) },
            },
            Padding = new Thickness(6, 10),
            Content = map,
        };
        var card = Card(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                Breadcrumb(),
                new BoxView { HeightRequest = 1, Color = Theme.Stone100 },
                mapArea,
                new BoxView { HeightRequest = 1, Color = Theme.Stone100 },
                new ContentView { Padding = new Thickness(14, 12), Content = panel },
            },
        }, radius: 16);
        card.Padding = 0;
        return card;
    }

    // ------------------------------------------------------------------ Strate « Royaume »

    private View BuildWorld()
    {
        var db = S.Db;
        var layout = WorldLayout.Compute(db);
        // Dans un sous-lieu (taverne...), la carte du royaume montre le lieu qui le contient.
        var current = S.RootLocation;

        // Lieux visibles : ceux déjà visités + ceux reliés à la position actuelle.
        var visible = S.State.SeenLocations.Where(db.Locations.ContainsKey).ToHashSet();
        foreach (var id in current.ConnectedIds.Where(db.Locations.ContainsKey)) visible.Add(id);
        visible.RemoveWhere(id => !S.IsVisible(db.Locations[id]));
        visible.Add(current.Id);

        visible.RemoveWhere(id => !layout.ContainsKey(id));
        var selectedId = _page.MapSelectedLocation is { } sel && visible.Contains(sel) ? sel : current.Id;

        var tiles = new List<HexTileSpec>();
        foreach (var id in visible)
        {
            if (!layout.TryGetValue(id, out var hex)) continue;
            var loc = db.Locations[id];
            var known = S.State.SeenLocations.Contains(id);
            var open = S.CanEnter(loc);
            var locId = id;
            tiles.Add(new HexTileSpec(
                hex,
                known ? FillOf(loc.Type) : FogFill,
                known ? TileStroke : FogStroke,
                Icon: known ? Theme.LocationStyle(loc.Type).Icon : Ico.Compass,
                Label: known ? loc.Name : "???",
                IsCurrent: id == current.Id,
                IsSelected: id == selectedId,
                Dashed: !known,
                OnTap: () => { _page.MapSelectedLocation = locId; _page.Render(); },
                Badge: open ? null : Ico.Lock,
                Opacity: known ? 1 : 0.85));
        }

        var roads = new List<HexRoad>();
        var drawn = new HashSet<(string, string)>();
        foreach (var id in visible)
        {
            foreach (var next in db.Locations[id].ConnectedIds.Where(visible.Contains))
            {
                var key = string.CompareOrdinal(id, next) < 0 ? (id, next) : (next, id);
                if (!drawn.Add(key) || !layout.ContainsKey(id) || !layout.ContainsKey(next)) continue;
                roads.Add(new HexRoad(layout[id], layout[next], id == current.Id || next == current.Id));
            }
        }

        return MapCard(new HexMapView(tiles, roads), WorldPanel(db.Locations[selectedId]));
    }

    private static string Duration(int minutes) =>
        minutes < 60 ? $"{minutes} min" : minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60:00}";

    private View WorldPanel(LocationDef loc)
    {
        var current = S.RootLocation;
        var known = S.State.SeenLocations.Contains(loc.Id);
        var isHere = loc.Id == current.Id;
        var adjacent = current.ConnectedIds.Contains(loc.Id);
        var open = S.CanEnter(loc);

        var title = new HorizontalStackLayout { Spacing = 8, Children = { Txt(known ? loc.Name : "Lieu inconnu", 16, Theme.Stone900, bold: true) } };
        if (isHere) title.Add(Badge("Vous êtes ici", Theme.Gold700));
        else if (!open) title.Add(Badge("Bloqué", Theme.Red600));

        var info = new VerticalStackLayout
        {
            Spacing = 2,
            Children = { title, Caps(known ? S.LocationTypeLabel(loc.Type) : "Brouillard", 9, Theme.Stone400) },
        };
        var tint = known ? FillOf(loc.Type) : FogFill;
        var box = new Border
        {
            WidthRequest = 40,
            HeightRequest = 40,
            BackgroundColor = tint,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = Icon(known ? Theme.LocationStyle(loc.Type).Icon : Ico.Compass, 18, Theme.InkOn(tint)),
        };

        var panel = Stack(IconRow(box, info));
        if (known && loc.Description.Length > 0) panel.Add(Txt(loc.Description, 13, Theme.Stone600));

        if (isHere)
        {
            panel.Add(Primary(S.CurrentLocation.Id == loc.Id ? $"Entrer dans {loc.Name}" : $"Retour : {S.CurrentLocation.Name}",
                () => { _page.MapShowCountry = false; _page.Render(); }));
        }
        else if (adjacent && open)
        {
            var id = loc.Id;
            var minutes = loc.TravelMinutes ?? S.Db.Content.Time.TravelMinutes;
            var go = Btn(S.Db.Content.Time.Enabled && minutes > 0 ? $"Voyager ici · {Duration(minutes)}" : "Voyager ici", () => _page.Travel(id), selected: true);
            go.BackgroundColor = Emerald;
            go.BorderColor = Emerald;
            go.TextColor = Colors.White;
            panel.Add(go);
        }
        else if (adjacent)
        {
            panel.Add(IconRow(Icon(Ico.Lock, 14, Theme.Stone400),
                Txt(loc.LockedMessage.Length > 0 ? loc.LockedMessage : "Le passage est bloqué.", 12, Theme.Stone500)));
        }
        else
        {
            panel.Add(Muted($"Aucune route directe depuis {current.Name}.", 12));
        }
        panel.Add(Muted("Touchez une tuile pour la sélectionner.", 11));
        return panel;
    }

    // ------------------------------------------------------------------ Strate « Lieu »

    private sealed record Building(string Key, string Icon, string Name, string Subtitle, Color Accent, string ActionLabel, Action Action);

    private List<Building> Buildings()
    {
        var loc = S.CurrentLocation;
        var list = new List<Building>();
        if (S.HasInn)
        {
            list.Add(new Building("inn", Ico.Bed, "Auberge", $"Repos complet pour {loc.InnPrice} or.", Theme.Blue600, "Dormir", () =>
            {
                _page.Notify(S.Rest() ? "L'équipe est reposée." : "Pas assez d'or pour l'auberge.");
                _page.AutoSave();
                _page.Render();
            }));
        }
        if (S.HasShop)
            list.Add(new Building("shop", Ico.Store, "Boutique", "Acheter et revendre de l'équipement.", Theme.Gold600, "Entrer", () => _page.SwitchTab(GameTab.Shop)));
        if (S.PendingFixedBattle is { } fb)
        {
            var names = string.Join(", ", fb.MonsterIds.Distinct().Where(S.Db.Monsters.ContainsKey).Select(id => S.Db.Monsters[id].Name));
            list.Add(new Building("boss", Ico.Skull, "Danger", names, Theme.Red600, "Affronter", () => _page.StartFixedBattle(fb)));
        }
        if (loc.RandomEncounters.Count > 0 && !S.IsLocked(UiFeature.Explore))
        {
            list.Add(new Building("explore", Ico.Swords, "Explorer", "Parcourir les environs à la recherche d'ennemis.", Theme.Stone800, "Chercher le combat", () =>
            {
                if (S.Explore() is { } monsters) _page.StartBattle(monsters);
            }));
        }
        // Sous-lieux (comme des salons dans une catégorie) : on y entre.
        foreach (var sub in S.SubLocations)
        {
            var subId = sub.Id;
            var style = Theme.LocationStyle(sub.Type);
            var open = S.CanEnter(sub);
            var desc = !open && sub.LockedMessage.Length > 0 ? sub.LockedMessage : sub.Description.Length > 0 ? sub.Description : "Un lieu à l'intérieur.";
            list.Add(new Building("loc:" + sub.Id, open ? style.Icon : Ico.Lock, sub.Name, desc, style.Accent, open ? "Entrer" : "Bloqué",
                () => _page.Travel(subId)));
        }
        if (S.Db.ParentOf(loc) is { } parent)
        {
            var parentId = parent.Id;
            list.Insert(0, new Building("exit", Ico.DoorOpen, "Sortir", $"Retourner à {parent.Name}.", Theme.Stone600, $"Sortir vers {parent.Name}",
                () => _page.Travel(parentId)));
        }
        else if (!S.IsLocked(UiFeature.WorldMap))
        {
            // Lieu principal : on en sort par l'entrée, vers la carte du pays (pour voyager ailleurs).
            var country = S.Db.Content.World.CountryName;
            list.Insert(0, new Building("exit", Ico.DoorOpen, loc.IsCity ? "Entrée de la ville" : "Sortie",
                $"Quitter {loc.Name} : retour à la carte ({country}).", Theme.Stone600, $"Sortir de {loc.Name}",
                () => { _page.MapShowCountry = true; _page.Render(); }));
        }
        if (loc.Training && S.TrainingOpponents.Count > 0)
            list.Add(new Building("training", Ico.Swords, "Terrain d'entraînement", "Combats sans risque pour s'exercer.", Theme.Blue600,
                "S'entraîner", _page.ShowTraining));
        // Portes de donjon (dans n'importe quel lieu, sous-lieux compris).
        foreach (var d in S.DungeonsHere)
        {
            var dungeonId = d.Id;
            var open = S.CanEnterDungeon(d);
            var done = S.State.DungeonsDone.Contains(d.Id);
            var desc = !open ? (done && !d.Repeatable ? "Exploré." : d.LockedMessage.Length > 0 ? d.LockedMessage : "La porte est fermée.")
                : d.Description.Length > 0 ? d.Description : $"{d.Steps.Count} épreuves.";
            list.Add(new Building("dungeon:" + d.Id, open ? Ico.Castle : Ico.Lock, d.Name, desc, Theme.Purple600, open ? "Entrer dans le donjon" : "Fermé",
                () => _page.EnterDungeon(dungeonId)));
        }
        foreach (var npc in S.VisibleNpcs)
        {
            var npcId = npc.Id;
            list.Add(new Building("npc:" + npc.Id, Ico.MessageCircle, npc.Name, npc.Description.Length > 0 ? npc.Description : "Un habitant.", Theme.Gold700, "Parler", () => _page.TalkTo(npcId)));
        }
        return list;
    }

    private View BuildLocal()
    {
        var buildings = Buildings();
        var radius = buildings.Count <= 6 ? 2 : 3;

        // Le pion de l'équipe démarre au centre ; les bâtiments occupent les anneaux autour.
        var slots = Hex.Spiral(new Hex(0, 0), radius).Skip(1).ToList();
        var placed = new Dictionary<Hex, Building>();
        for (var i = 0; i < buildings.Count && i < slots.Count; i++) placed[slots[i]] = buildings[i];

        var selected = buildings.FirstOrDefault(b => b.Key == _page.MapSelectedBuilding);
        var tiles = new List<HexTileSpec>();
        foreach (var hex in Hex.Grid(radius))
        {
            var cell = hex;
            if (placed.TryGetValue(hex, out var b))
            {
                tiles.Add(new HexTileSpec(hex, BuildingFill, Theme.Stone500, b.Icon, b.Name,
                    IsSelected: selected == b,
                    OnTap: () =>
                    {
                        _page.MapSelectedBuilding = b.Key;
                        _page.MapPartyHex = cell;
                        _page.Render();
                    }));
            }
            else
            {
                tiles.Add(new HexTileSpec(hex, EmptyFill, EmptyStroke, IsCurrent: hex == _page.MapPartyHex,
                    OnTap: () =>
                    {
                        _page.MapSelectedBuilding = null;
                        _page.MapPartyHex = cell;
                        _page.Render();
                    }));
            }
        }

        var hero = S.State.Party.FirstOrDefault(c => c.DefId == S.State.HeroId) ?? S.State.Party.FirstOrDefault();
        var heroName = hero is null ? "" : S.DefOf(hero).Name;
        var initial = heroName.Length > 0 ? heroName[..1].ToUpperInvariant() : "?";
        var tokens = new List<HexToken> { new(_page.MapPartyHex, Theme.Gold600, initial) };

        return MapCard(new HexMapView(tiles, tokens: tokens), LocalPanel(selected, buildings));
    }

    private View LocalPanel(Building? selected, List<Building> buildings)
    {
        if (selected is null)
        {
            var hint = buildings.Count == 0
                ? (S.IsLocked(UiFeature.WorldMap) ? "Rien à faire ici pour l'instant." : $"Rien à faire ici. Passez par la carte de {S.Db.Content.World.CountryName} pour voyager.")
                : "Touchez un bâtiment ou un habitant pour interagir. Touchez une case vide pour y déplacer l'équipe.";
            var list = Stack(Muted(hint, 12));
            foreach (var b in buildings)
            {
                var bb = b;
                var row = IconRow(IconBox(b.Icon, b.Accent, 36), new VerticalStackLayout
                {
                    Spacing = 0,
                    Children = { Txt(b.Name, 14, Theme.Stone900, bold: true), Muted(b.Subtitle, 11) },
                }, Icon(Ico.ChevronRight, 16, Theme.Stone300));
                list.Add(OnTap(row, () => { _page.MapSelectedBuilding = bb.Key; _page.Render(); }));
            }
            return list;
        }

        // Aperçu du bâtiment, comme la fenêtre « bâtiment » de Service Impérial.
        var head = IconRow(IconBox(selected.Icon, selected.Accent, 44), new VerticalStackLayout
        {
            Spacing = 1,
            Children = { Txt(selected.Name, 16, Theme.Stone900, bold: true), Muted(selected.Subtitle, 12) },
        }, OnTap(Icon(Ico.X, 18, Theme.Stone400), () => { _page.MapSelectedBuilding = null; _page.Render(); }));
        return Stack(head, Primary(selected.ActionLabel, selected.Action));
    }
}
