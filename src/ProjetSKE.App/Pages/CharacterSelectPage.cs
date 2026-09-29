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

        // Une carte par héros, qu'on fait glisser de gauche à droite (ou flèches, sur PC).
        // Chaque carte est construite une seule fois : glisser vite ne reconstruit rien (l'ancienne liste défilante
        // recréait la carte à chaque passage et figeait l'écran).
        var heroes = db.Starters.ToList();
        var cards = heroes.Select(h => (View)new ScrollView { Content = HeroCard(slot, db, h), Padding = new Thickness(4, 4, 4, 12) }).ToList();
        var stage = new Grid { Padding = new Thickness(14, 0), IsClippedToBounds = true };
        var index = 0;
        if (cards.Count > 0) stage.Add(cards[0]);

        var dots = new IndicatorView
        {
            IndicatorColor = Theme.Stone300,
            SelectedIndicatorColor = Theme.Gold500,
            IndicatorSize = 9,
            HorizontalOptions = LayoutOptions.Center,
            ItemsSource = heroes,
            IsVisible = heroes.Count > 1,
        };

        void Show(int next)
        {
            next = Math.Clamp(next, 0, cards.Count - 1);
            if (cards.Count == 0 || next == index) return;
            var direction = next > index ? 1 : -1;
            var from = cards[index];
            var to = cards[next];
            index = next;
            dots.Position = next;
            // Glissement rapide ; si on enchaîne, l'animation précédente s'arrête net (rien ne s'accumule).
            foreach (var view in stage.Children.OfType<View>().ToList())
            {
                view.AbortAnimation("TranslateTo");
                if (view != from) stage.Remove(view);
            }
            from.TranslationX = 0;
            var width = Math.Max(stage.Width, 300);
            to.TranslationX = direction * width;
            stage.Add(to);
            _ = from.TranslateTo(-direction * width, 0, 220, Easing.CubicOut).ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (cards[index] != from) stage.Remove(from);
                    from.TranslationX = 0;
                }));
            _ = to.TranslateTo(0, 0, 220, Easing.CubicOut);
        }

        // Glisser au doigt : la carte défile en hauteur, le geste horizontal change de héros.
        foreach (var card in cards)
        {
            if (card is not ScrollView { Content: View inner }) continue;
            var left = new SwipeGestureRecognizer { Direction = SwipeDirection.Left, Threshold = 40 };
            left.Swiped += (_, _) => Show(index + 1);
            var right = new SwipeGestureRecognizer { Direction = SwipeDirection.Right, Threshold = 40 };
            right.Swiped += (_, _) => Show(index - 1);
            inner.GestureRecognizers.Add(left);
            inner.GestureRecognizers.Add(right);
        }

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
        };
        root.Add(header, 0, 0);
        root.Add(stage, 0, 1);
        // Flèches (souris sur PC, ou toucher) en plus du glissement.
        View Arrow(string glyph, int step)
        {
            var arrow = new Border
            {
                WidthRequest = 40, HeightRequest = 40, BackgroundColor = Night.Stone900, Stroke = Theme.Gold600, StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                Content = Icon(glyph, 18, Theme.Gold500),
            };
            return OnTap(arrow, () => Show(index + step));
        }
        var dotsRow = new Grid
        {
            ColumnSpacing = 14,
            HorizontalOptions = LayoutOptions.Center,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
        };
        dots.VerticalOptions = LayoutOptions.Center;
        dotsRow.Add(Arrow(Ico.ArrowLeft, -1), 0, 0);
        dotsRow.Add(dots, 1, 0);
        dotsRow.Add(Arrow(Ico.ArrowRight, 1), 2, 0);
        dotsRow.IsVisible = heroes.Count > 1;
        var footer = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(0, 4, 0, 18), Children = { dotsRow } };
        if (heroes.Count > 1)
        {
            var hint = Caps("Glisse ou utilise les flèches pour voir les autres héros", 9, Theme.Stone500);
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
            if (start.Description.Length > 0) startInfo.Add(Txt(start.Description, 12, Night.Stone400));
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
                new Label { Text = def.Name.ToUpperInvariant(), FontFamily = "serif", FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = Night.Stone100, CharacterSpacing = 4, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = def.ClassAndTitle.ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Theme.Gold500, CharacterSpacing = 4, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = def.Description, FontSize = 14, TextColor = Night.Stone400, FontAttributes = FontAttributes.Italic, HorizontalTextAlignment = TextAlignment.Center },
                TileGrid(
                [
                    StatCell(Ico.Heart, "PV", st.MaxHp, Theme.Hp),
                    StatCell(Ico.Droplet, "PM", st.MaxMana, Theme.Mana),
                    StatCell(Ico.Sword, "ATQ", st.Attack, Theme.Danger),
                    StatCell(Ico.Shield, "DEF", st.Defense, Theme.Muted),
                    StatCell(Ico.Sparkles, "MAG", st.Magic, Theme.Xp),
                    StatCell(Ico.Wind, "VIT", st.Speed, Theme.Good),
                ], 3),
                Txt("Compétences : " + string.Join(", ", skills), 13, Night.Stone400),
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
        b.TextColor = Night.Stone900;
        return b;
    }

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(_slot < 0 ? new Dev.DevHomePage() : (Page)new SlotPage(newGame: true));
        return true;
    }
}
