using ProjetSKE.App.Ui;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

public sealed class TitlePage : ContentPage
{
    public TitlePage()
    {
        Background = Theme.Vertical(Color.FromArgb("#24305A"), Theme.Bg);

        var emblem = new Border
        {
            WidthRequest = 130,
            HeightRequest = 130,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
            Stroke = Theme.Accent,
            StrokeThickness = 3,
            Background = Theme.Diagonal(Color.FromArgb("#3A2A10"), Theme.Bg),
            Shadow = new Shadow { Brush = Theme.Accent, Offset = new Point(0, 0), Radius = 30, Opacity = 0.5f },
            Content = Icon("⚔️", 60),
        };

        var title = new Label
        {
            Text = "PROJET SKE",
            FontSize = 40,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Accent,
            CharacterSpacing = 6,
            HorizontalTextAlignment = TextAlignment.Center,
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 4), Radius = 8, Opacity = 0.8f },
        };
        var subtitle = new Label
        {
            Text = SkeApp.Db.Content.Title.ToUpperInvariant(),
            FontSize = 14,
            TextColor = Theme.AccentLight,
            CharacterSpacing = 4,
            HorizontalTextAlignment = TextAlignment.Center,
        };

        var dev = Btn("🛠️  Développeur", () => SkeApp.GoTo(SkeApp.DevUnlocked ? new Dev.DevHomePage() : (Page)new Dev.DevCodePage()));
        dev.FontSize = 12;
        dev.Opacity = 0.7;
        dev.HorizontalOptions = LayoutOptions.Center;

        var grid = new Grid
        {
            Padding = new Thickness(28, 40),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
            RowSpacing = 16,
        };
        grid.Add(new VerticalStackLayout
        {
            Spacing = 18,
            VerticalOptions = LayoutOptions.End,
            Children = { emblem, title, subtitle },
        }, 0, 0);
        grid.Add(new BoxView { HeightRequest = 1, Color = Theme.Accent.WithAlpha(0.4f), Margin = new Thickness(40, 10) }, 0, 1);
        grid.Add(new VerticalStackLayout
        {
            Spacing = 14,
            VerticalOptions = LayoutOptions.Start,
            Children =
            {
                Primary("Nouvelle partie", () => SkeApp.GoTo(new SlotPage(newGame: true))),
                Btn("Charger une partie", () => SkeApp.GoTo(new SlotPage(newGame: false))),
            },
        }, 0, 2);
        grid.Add(dev, 0, 3);
        grid.Add(new Label { Text = "v0.2", FontSize = 11, TextColor = Theme.Muted, HorizontalTextAlignment = TextAlignment.Center }, 0, 4);
        Content = grid;
    }
}
