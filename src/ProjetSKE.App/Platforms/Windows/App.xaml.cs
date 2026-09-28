namespace ProjetSKE.App.WinUI;

/// <summary>Point d'entrée sur PC (Windows). Tout le reste est le même code que sur téléphone.</summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        // Test automatique (GitHub Actions) : ProjetSKE.App.exe --autotest
        if (Environment.GetCommandLineArgs().Contains("--autotest")) Dev.AutoTest.Requested = true;
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
