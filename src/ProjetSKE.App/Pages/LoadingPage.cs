using ProjetSKE.App.Ui;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

/// <summary>
/// Écran de chargement, affiché tout de suite quand on ouvre un écran lourd (partie, sauvegarde, choix du héros) :
/// l'écran lourd se construit derrière, au lieu de laisser l'écran précédent figé sans réaction.
/// </summary>
public sealed class LoadingPage : ContentPage
{
    public LoadingPage(string message)
    {
        BackgroundColor = Night.Stone900;
        var spinner = new ActivityIndicator
        {
            IsRunning = true,
            Color = Theme.Gold500,
            WidthRequest = 42,
            HeightRequest = 42,
            HorizontalOptions = LayoutOptions.Center,
        };
        var label = new Label
        {
            Text = message.ToUpperInvariant(),
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 4,
            TextColor = Theme.Gold500,
            HorizontalTextAlignment = TextAlignment.Center,
        };
        Content = new VerticalStackLayout
        {
            Spacing = 18,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children = { Icon(Ico.Swords, 34, Theme.Gold600), spinner, label },
        };
    }

    // Pas de retour arrière pendant le chargement.
    protected override bool OnBackButtonPressed() => true;
}
