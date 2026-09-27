namespace ProjetSKE.App;

/// <summary>
/// Garde une trace du dernier plantage (fichier sur le téléphone) pour l'afficher au lancement suivant,
/// et écrit dans le journal Android (logcat, étiquette « SKE »).
/// </summary>
public static class CrashReporter
{
    private static string FilePath => Path.Combine(FileSystem.AppDataDirectory, "dernier-plantage.txt");

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Save(e.ExceptionObject as Exception, "non gérée");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Save(e.Exception, "tâche");
            e.SetObserved();
        };
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => Save(e.Exception, "Android");
    }

    public static void Log(string message) => Android.Util.Log.Info("SKE", message);

    public static void Save(Exception? e, string origin)
    {
        var text = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss} · erreur {origin}\n{e}";
        Android.Util.Log.Error("SKE", "SKE_CRASH " + text);
        try { File.WriteAllText(FilePath, text); }
        catch (Exception) { /* on ne doit jamais planter en signalant un plantage */ }
    }

    public static string? Last
    {
        get
        {
            try { return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null; }
            catch (Exception) { return null; }
        }
    }

    public static void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch (Exception) { }
    }
}
