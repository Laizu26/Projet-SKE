using ProjetSKE.App.Pages;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.App;

public class SkeApp : Application
{
    public static GameDatabase Db => GameDatabase.Default;

    private static SaveService? _saves;
    public static SaveService Saves => _saves ??= new SaveService(Path.Combine(FileSystem.AppDataDirectory, "saves"));

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new TitlePage());

    /// <summary>Remplace l'écran affiché (pas de pile de navigation : style menus de jeu).</summary>
    public static void GoTo(Page page)
    {
        if (Current?.Windows.Count > 0) Current.Windows[0].Page = page;
    }
}
