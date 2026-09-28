namespace ProjetSKE.App;

/// <summary>
/// Garde une trace du dernier plantage (fichier de l'appli) pour l'afficher au lancement suivant,
/// et écrit dans le journal : logcat (étiquette « SKE ») sur Android ; sur PC, la console et le fichier
/// indiqué par la variable d'environnement SKE_LOG_FILE (test automatique de GitHub Actions).
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
#if ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => Save(e.Exception, "Android");
#endif
    }

    public static void Log(string message)
    {
#if ANDROID
        Android.Util.Log.Info("SKE", message);
#else
        Write(" SKE : " + message);
#endif
    }

#if !ANDROID
    private static readonly object LogLock = new();

    private static void Write(string line)
    {
        try
        {
            Console.WriteLine(line);
            if (Environment.GetEnvironmentVariable("SKE_LOG_FILE") is { Length: > 0 } file)
                lock (LogLock) File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff}{line}{Environment.NewLine}");
        }
        catch (Exception) { }
    }
#endif

    public static void Save(Exception? e, string origin)
    {
        var text = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss} · erreur {origin}\n{e}";
#if ANDROID
        Android.Util.Log.Error("SKE", "SKE_CRASH " + text);
#else
        Write(" SKE : SKE_CRASH " + text);
#endif
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
