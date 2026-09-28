using ProjetSKE.App.Ui;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

/// <summary>Sélection du personnage de départ (pas de création).</summary>
public sealed class CharacterSelectPage : ContentPage
{
    private readonly int _slot;

    /// <param name="slot">Emplacement de sauvegarde, ou -1 pour une partie de test (jamais sauvegardée).</param>
    /// <param name="db">Contenu à utiliser (par défaut le contenu actif).</param>
    public CharacterSelectPage(int slot, GameDatabase? db = null)
    {
        _slot = slot;
        Background = Theme.PageBackground;
        db ??= SkeApp.Db;
        var header = new VerticalStackLayout { Padding = new Thickness(18, 28, 18, 8), Spacing = 16 };
        header.Add(Pill("◂  Retour", () => SkeApp.GoTo(slot < 0 ? new Dev.DevHomePage() : (Page)new SlotPage(newGame: true))));
        header.Add(PageHeader(Ico.User, slot < 0 ? "Partie de test" : "Choisis ton héros", "D'autres te rejoindront en chemin"));

        // Une carte par héros, qu'on fait glisser de gauche à droite (les voisines dépassent sur les bords).
        var heroes = db.Starters.ToList();
        var carousel = new CarouselView
        {
            ItemsSource = heroes,
            Loop = false,
            PeekAreaInsets = new Thickness(heroes.Count > 1 ? 26 : 0, 0),
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Horizontal)
            {
                SnapPointsType = SnapPointsType.MandatorySingle,
                SnapPointsAlignment = SnapPointsAlignment.Center,
                ItemSpacing = 8,
            },
            ItemTemplate = new DataTemplate(() =>
            {
                var host = new ContentView { Padding = new Thickness(4, 4, 4, 12) };
                host.BindingContextChanged += (_, _) =>
                {
                    if (host.BindingContext is CharacterDef def) host.Content = new ScrollView { Content = HeroCard(slot, db, def) };
                };
                return host;
            }),
        };
        var dots = new IndicatorView
        {
            IndicatorColor = Theme.Stone300,
            SelectedIndicatorColor = Theme.Gold500,
            IndicatorSize = 9,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 4, 0, 18),
            IsVisible = heroes.Count > 1,
        };
        carousel.IndicatorView = dots;

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
        };
        root.Add(header, 0, 0);
        root.Add(carousel, 0, 1);
        var footer = new VerticalStackLayout { Spacing = 2, Children = { dots } };
        if (heroes.Count > 1)
        {
            var hint = Caps("Glisse pour voir les autres héros", 9, Theme.Stone500);
            hint.HorizontalTextAlignment = TextAlignment.Center;
            footer.Children.Insert(0, hint);
        }
        root.Add(footer, 0, 2);
        Content = root;
    }

    /// <summary>Carte d'un héros : portrait, classe, stats, compétences, départ, et le bouton pour commencer.</summary>
    private static View HeroCard(int slot, GameDatabase db, CharacterDef def)
    {
        var skills = def.Skills.Where(s => s.Level <= 1 && db.Skills.ContainsKey(s.SkillId)).Select(s => db.Skills[s.SkillId].Name);
        var id = def.Id;
        var st = def.BaseStats;
        var color = Theme.AvatarColor(def.Id);
        // Le départ dépend du héros : on l'annonce, sans le faire choisir.
        var start = db.StartFor(id);
        var startInfo = new VerticalStackLayout { Spacing = 2 };
        if (db.Starts.Count > 1)
        {
            startInfo.Add(IconCaps(Ico.Compass, start.Name, Theme.Gold500, 9));
            if (start.Description.Length > 0) startInfo.Add(Txt(start.Description, 12, Theme.Stone400));
        }
        View portrait = def.PortraitId is { } pid && db.Portraits.TryGetValue(pid, out var image)
            ? new Border
            {
                WidthRequest = 120, HeightRequest = 144, HorizontalOptions = LayoutOptions.Center,
                Stroke = Theme.Gold500, StrokeThickness = 2,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                Content = new FramedImage(image),
            }
            : Avatar(def.Name, color, 96);
        return DarkCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                portrait,
                new Label { Text = def.Name.ToUpperInvariant(), FontFamily = "serif", FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = Theme.Stone100, CharacterSpacing = 4, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = def.ClassAndTitle.ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Theme.Gold500, CharacterSpacing = 4, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = def.Description, FontSize = 14, TextColor = Theme.Stone400, FontAttributes = FontAttributes.Italic, HorizontalTextAlignment = TextAlignment.Center },
                TileGrid(
                [
                    StatCell(Ico.Heart, "PV", st.MaxHp, Theme.Hp),
                    StatCell(Ico.Droplet, "PM", st.MaxMana, Theme.Mana),
                    StatCell(Ico.Sword, "ATQ", st.Attack, Theme.Danger),
                    StatCell(Ico.Shield, "DEF", st.Defense, Theme.Muted),
                    StatCell(Ico.Sparkles, "MAG", st.Magic, Theme.Xp),
                    StatCell(Ico.Wind, "VIT", st.Speed, Theme.Good),
                ], 3),
                Txt("Compétences : " + string.Join(", ", skills), 13, Theme.Stone400),
                startInfo,
                StartButton("Commencer avec " + def.Name, () => Launch(slot, db, id, null)),
            },
        }, Ico.User, goldLine: true);
    }

    /// <summary>Lance la partie avec ce héros (départ null = celui du héros).</summary>
    public static void Launch(int slot, GameDatabase db, string heroId, string? startId)
    {
        SkeApp.Open(() =>
        {
            var session = GameSession.NewGame(db, heroId, startId);
            if (slot >= 0) SkeApp.Saves.Save(slot, session.State);
            return new GamePage(session, slot, playIntro: true);
        }, "Nouvelle partie");
    }

    /// <summary>Bouton or sur fond pierre.</summary>
    internal static Button StartButton(string text, Action onClick)
    {
        var b = Primary(text, onClick);
        b.BackgroundColor = Theme.Gold500;
        b.BorderColor = Theme.Gold400;
        b.TextColor = Theme.Stone900;
        return b;
    }

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(_slot < 0 ? new Dev.DevHomePage() : (Page)new SlotPage(newGame: true));
        return true;
    }
}
