namespace ProjetSKE.App.Ui;

/// <summary>
/// Adapte chaque écran à la largeur disponible : sur un grand écran (PC, tablette), le contenu reste dans une
/// colonne centrée aux proportions du jeu au lieu de s'étirer sur toute la largeur. Sur téléphone, l'écran est
/// plus étroit que la colonne : rien ne change. Appliqué à toutes les pages (voir SkeApp.GoTo), donc à tout
/// nouvel écran, sur les deux plateformes.
/// </summary>
public static class Responsive
{
    /// <summary>Largeur maximale du jeu (écrans de jeu, menus).</summary>
    public const double GameWidth = 640;

    /// <summary>Largeur maximale des écrans du mode développeur (plus de place pour écrire).</summary>
    public const double EditorWidth = 980;

    public static void Apply(Page page)
    {
        if (page is not ContentPage content) return;
        Constrain(content);
        // La fenêtre change de taille, ou l'écran remplace son contenu en se rafraîchissant : on réapplique.
        content.SizeChanged -= OnSizeChanged;
        content.SizeChanged += OnSizeChanged;
        content.PropertyChanged -= OnChanged;
        content.PropertyChanged += OnChanged;
    }

    private static void OnSizeChanged(object? sender, EventArgs e)
    {
        if (sender is ContentPage page) Constrain(page);
    }

    private static void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is ContentPage page && e.PropertyName == nameof(ContentPage.Content)) Constrain(page);
    }

    /// <summary>Colonne de largeur min(fenêtre, largeur max), centrée.</summary>
    private static void Constrain(ContentPage page)
    {
        if (page.Content is not { } view) return;
        var max = IsEditor(page) ? EditorWidth : GameWidth;
        if (page.Width > max)
        {
            view.WidthRequest = max;
            view.HorizontalOptions = LayoutOptions.Center;
        }
        else if (view.WidthRequest >= 0)
        {
            // Écran étroit (téléphone) : toute la largeur, comme avant.
            view.WidthRequest = -1;
            view.HorizontalOptions = LayoutOptions.Fill;
        }
    }

    /// <summary>Écrans du mode développeur (espace de noms Dev).</summary>
    private static bool IsEditor(Page page) => page.GetType().Namespace?.EndsWith(".Dev", StringComparison.Ordinal) == true;
}
