using ProjetSKE.App.Ui;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

/// <summary>Écran titre, dans l'esprit de l'écran d'accès de Service Impérial.</summary>
public sealed class TitlePage : ContentPage
{
    public TitlePage()
    {
        Background = Theme.Diagonal(Theme.Stone900, Theme.Stone950);

        var band = new VerticalStackLayout
        {
            BackgroundColor = Theme.Stone900,
            Spacing = 0,
            Children =
            {
                GoldLine(4),
                new VerticalStackLayout
                {
                    Padding = new Thickness(24, 30, 24, 26),
                    Spacing = 12,
                    Children =
                    {
                        Emblem(Ico.Shield, 92),
                        new Label
                        {
                            Text = "PROJET SKE", FontFamily = "serif", FontSize = 30, FontAttributes = FontAttributes.Bold,
                            TextColor = Theme.Stone100, CharacterSpacing = 6, HorizontalTextAlignment = TextAlignment.Center,
                        },
                        new Label
                        {
                            Text = SkeApp.Db.Content.Title.ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold,
                            TextColor = Theme.Stone500, CharacterSpacing = 5, HorizontalTextAlignment = TextAlignment.Center,
                        },
                    },
                },
            },
        };

        var body = new VerticalStackLayout
        {
            Padding = new Thickness(24, 26),
            Spacing = 14,
            BackgroundColor = Theme.Parchment,
            Children =
            {
                IconCaps(Ico.Swords, "Nouvelle aventure", Theme.Stone400),
                Primary("Nouvelle partie  ▸", () => SkeApp.GoTo(new SlotPage(newGame: true))),
                IconCaps(Ico.Save, "Reprendre", Theme.Stone400),
                Btn("Charger une partie", () => SkeApp.GoTo(new SlotPage(newGame: false))),
            },
        };

        var footer = new ContentView
        {
            BackgroundColor = Theme.Stone200,
            Padding = new Thickness(12),
            Content = new Label
            {
                Text = "CHRONIQUES · VERSION 0.3", FontSize = 9, FontAttributes = FontAttributes.Bold, CharacterSpacing = 3,
                TextColor = Theme.Stone400, HorizontalTextAlignment = TextAlignment.Center,
            },
        };

        var card = new Border
        {
            Content = new VerticalStackLayout { Spacing = 0, Children = { band, body, footer } },
            BackgroundColor = Theme.Parchment,
            Stroke = Theme.Stone800,
            StrokeThickness = 4,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = 0,
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 14), Radius = 30, Opacity = 0.6f },
        };

        var dev = new Border
        {
            BackgroundColor = Theme.Stone900,
            Stroke = Theme.Gold700,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(14, 12),
            Content = new HorizontalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Center,
                Children = { Icon(Ico.WandSparkles, 16, Theme.Gold500), Caps("Mode développeur", 11, Theme.Gold500) },
            },
        };
        OnTap(dev, () => SkeApp.GoTo(SkeApp.DevUnlocked ? new Dev.DevHomePage() : (Page)new Dev.DevCodePage()));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(22, 40),
                Spacing = 18,
                VerticalOptions = LayoutOptions.Center,
                Children = { card, dev },
            },
        };
    }
}
