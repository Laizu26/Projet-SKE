using ProjetSKE.App.Ui;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

public sealed class TitlePage : ContentPage
{
    public TitlePage()
    {
        BackgroundColor = Theme.Bg;
        Content = new VerticalStackLayout
        {
            Padding = new Thickness(24),
            Spacing = 12,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                Txt("PROJET SKE", 32, Theme.Accent, bold: true),
                Muted(SkeApp.Db.Content.Title, 14),
                new BoxView { HeightRequest = 24, Color = Colors.Transparent },
                Btn("Nouvelle partie", () => SkeApp.GoTo(new SlotPage(newGame: true))),
                Btn("Charger une partie", () => SkeApp.GoTo(new SlotPage(newGame: false))),
                new BoxView { HeightRequest = 40, Color = Colors.Transparent },
                Btn("Développeur", () => SkeApp.GoTo(SkeApp.DevUnlocked ? new Dev.DevHomePage() : (Page)new Dev.DevCodePage())),
            },
        };
    }
}
