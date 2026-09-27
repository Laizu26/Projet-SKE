using ProjetSKE.App.Ui;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

/// <summary>Affiche une erreur au lieu de fermer le jeu, avec de quoi la copier pour la signaler.</summary>
public sealed class ErrorPage : ContentPage
{
    public ErrorPage(Exception error, string context)
    {
        Background = Theme.PageBackground;
        var details = $"{context}\n{error}";
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 30),
                Spacing = 14,
                Children =
                {
                    PageHeader(Ico.CircleAlert, "Oups", "Une erreur est survenue"),
                    Txt(error.Message, 15, Theme.Red600, bold: true),
                    Muted("Copie ce rapport et envoie-le pour qu'on corrige le problème.", 13),
                    ButtonRow(
                        Btn("Copier le rapport", async () => await Clipboard.Default.SetTextAsync(details), selected: true),
                        Btn("Écran titre", () => SkeApp.GoTo(new TitlePage()))),
                    Card(Txt(details, 11, Theme.Stone700)),
                },
            },
        };
    }
}
