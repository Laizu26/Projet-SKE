using ProjetSKE.App.Pages;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.App;

public class SkeApp : Application
{
    /// <summary>Code d'accès au mode développeur.</summary>
    public const string DevCode = "1234";

    /// <summary>Mode développeur déverrouillé pour cette session de l'application.</summary>
    public static bool DevUnlocked { get; set; }

    private static string ContentPath => Path.Combine(FileSystem.AppDataDirectory, "content.json");

    private static GameDatabase? _db;

    /// <summary>Contenu actif : celui de l'éditeur s'il a été enregistré, sinon le contenu officiel.</summary>
    public static GameDatabase Db => _db ??= LoadActiveContent();

    public static bool HasCustomContent => File.Exists(ContentPath);

    private static SaveService? _saves;
    public static SaveService Saves => _saves ??= new SaveService(Path.Combine(FileSystem.AppDataDirectory, "saves"));

    private static GameDatabase LoadActiveContent()
    {
        try
        {
            if (File.Exists(ContentPath)) return new GameDatabase(ContentSerializer.FromJson(File.ReadAllText(ContentPath)));
        }
        catch (Exception)
        {
            // Fichier illisible : on repart sur le contenu officiel.
        }
        return GameDatabase.Default;
    }

    /// <summary>Enregistre le contenu de l'éditeur et l'active pour les parties.</summary>
    public static void ApplyContent(GameContent content)
    {
        var copy = ContentSerializer.Clone(content);
        File.WriteAllText(ContentPath, ContentSerializer.ToJson(copy));
        _db = new GameDatabase(copy);
    }

    /// <summary>Revient au contenu officiel (supprime le contenu de l'éditeur).</summary>
    public static void ResetContent()
    {
        if (File.Exists(ContentPath)) File.Delete(ContentPath);
        _db = GameDatabase.Default;
    }

    private static bool _servicesStarted;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Base en ligne configurée : on récupère la dernière version du contenu en arrière-plan.
        // CreateWindow peut être appelé plusieurs fois (recréation de l'activité) : on ne démarre qu'une fois.
        if (!_servicesStarted)
        {
            _servicesStarted = true;
            if (Dev.AutoTest.Requested)
                Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(3), () => _ = Dev.AutoTest.RunAsync());
            else
                Dev.CloudSync.StartAuto(Dispatcher);
        }
        UserAppTheme = Ui.Theme.Dark ? AppTheme.Dark : AppTheme.Light; // contrôles natifs (champs, listes) assortis
        var title = new TitlePage();
        Ui.Responsive.Apply(title);
        var window = new Window(title) { Title = "Projet SKE" };
#if WINDOWS
        // Sur PC : une fenêtre aux proportions d'un téléphone (le jeu est pensé en portrait), redimensionnable.
        window.Width = 480;
        window.Height = 920;
        window.MinimumWidth = 360;
        window.MinimumHeight = 560;
#endif
        return window;
    }

    /// <summary>Remplace l'écran affiché (pas de pile de navigation : style menus de jeu).</summary>
    public static void GoTo(Page page)
    {
        Ui.Responsive.Apply(page);
        if (Current?.Windows.Count > 0) Current.Windows[0].Page = page;
    }

    /// <summary>
    /// Ouvre un écran lourd : l'écran de chargement s'affiche tout de suite, puis l'écran est construit juste après
    /// (l'écran précédent ne reste plus figé). Si sa création échoue, affiche l'erreur au lieu de fermer le jeu.
    /// </summary>
    public static void Open(Func<Page> create, string context)
    {
        var dispatcher = Current?.Dispatcher;
        // Test automatique : ouverture directe (ses étapes enchaînent sur l'écran ouvert).
        if (dispatcher is null || Dev.AutoTest.Requested)
        {
            Build(create, context);
            return;
        }
        GoTo(new LoadingPage(context));
        // Laisse le temps à l'écran de chargement de s'afficher avant le gros du travail.
        dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(60), () => Build(create, context));
    }

    private static void Build(Func<Page> create, string context)
    {
        try
        {
            GoTo(create());
        }
        catch (Exception e)
        {
            CrashReporter.Save(e, context);
            GoTo(new ErrorPage(e, context));
        }
    }
}
