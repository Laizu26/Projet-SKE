using System.Net.Http.Headers;
using System.Reflection;
using ProjetSKE.Core.Cloud;

namespace ProjetSKE.App;

/// <summary>
/// Mises à jour dans l'application : chaque version compilée et testée par GitHub Actions est publiée en « Release »
/// sur le dépôt, pour le téléphone (ProjetSKE.apk) et pour le PC (ProjetSKE-Windows.zip), avec le même numéro.
/// L'appli compare son numéro de compilation à la dernière Release, télécharge son fichier et l'installe :
/// sur téléphone, l'installateur d'Android (confirmation) ; sur PC, l'appli se ferme, se remplace et se relance.
/// Les sauvegardes, le contenu et le brouillon du mode développeur sont gardés.
/// </summary>
public static class Updates
{
    public const string Repository = "Laizu26/Projet-SKE";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ProjetSKE", "1.0"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    /// <summary>Fichier de la Release pour cette plateforme.</summary>
#if WINDOWS
    private const string AssetExtension = ".zip";
    public const string PlatformName = "PC";
#else
    private const string AssetExtension = ".apk";
    public const string PlatformName = "téléphone";
#endif

    /// <summary>Numéro de compilation de l'appli installée (le même sur téléphone et sur PC).</summary>
    public static int CurrentBuild
    {
        get
        {
            var embedded = typeof(Updates).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "SkeBuild")?.Value;
            if (int.TryParse(embedded, out var b) && b > 1) return b;
            return int.TryParse(AppInfo.Current.BuildString, out var build) ? build : 0;
        }
    }

    public static string CurrentVersion => $"1.0.{CurrentBuild} (compilation {CurrentBuild}, {PlatformName})";

    /// <summary>Dernière version trouvée (null tant que rien n'est vérifié ou si rien n'est publié).</summary>
    public static ReleaseInfo? Latest { get; private set; }

    public static string Status { get; private set; } = "";

    public static bool IsAvailable => Latest is { } r && r.Build > CurrentBuild;

    public static event Action? Changed;

    /// <summary>Vérifie la dernière version publiée. Ne lève jamais d'exception.</summary>
    public static async Task CheckAsync()
    {
        try
        {
            using var response = await Http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Latest = null;
                Status = "Aucune version publiée (ou dépôt encore privé).";
            }
            else
            {
                response.EnsureSuccessStatusCode();
                Latest = ReleaseInfo.Parse(await response.Content.ReadAsStringAsync(), AssetExtension);
                Status = Latest is null ? $"Dernière version sans fichier pour le {PlatformName}."
                    : IsAvailable ? $"Nouvelle version disponible : {Latest.Name}."
                    : "Le jeu est à jour.";
            }
        }
        catch (Exception e)
        {
            Status = "Vérification impossible : " + e.Message;
        }
        MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());
    }

    /// <summary>Télécharge la mise à jour (progression de 0 à 1) puis l'installe.</summary>
    public static async Task<string> DownloadAndInstallAsync(ReleaseInfo release, IProgress<double> progress)
    {
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, $"ProjetSKE-{release.Build}{AssetExtension}");
            using (var response = await Http.GetAsync(release.ApkUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? release.Size;
                await using var input = await response.Content.ReadAsStreamAsync();
                await using var output = File.Create(path);
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await input.ReadAsync(buffer)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read));
                    done += read;
                    if (total > 0) progress.Report((double)done / total);
                }
            }
            CrashReporter.Log("SKE_UPDATE téléchargée : " + path);
            return await InstallAsync(path, release);
        }
        catch (Exception e)
        {
            return "Mise à jour impossible : " + e.Message;
        }
    }

#if WINDOWS
    /// <summary>
    /// PC : le .zip est décompressé à côté, puis un petit script attend la fermeture de l'appli, remplace ses fichiers
    /// par les nouveaux et la relance. Les données (parties, contenu) sont ailleurs (dossier de l'utilisateur) : gardées.
    /// </summary>
    private static Task<string> InstallAsync(string zip, ReleaseInfo release)
    {
        var staging = Path.Combine(Path.GetTempPath(), $"ProjetSKE-{release.Build}");
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        System.IO.Compression.ZipFile.ExtractToDirectory(zip, staging);
        var appDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var exe = Environment.ProcessPath ?? Path.Combine(appDir, "ProjetSKE.App.exe");
        var pid = Environment.ProcessId;
        var script = Path.Combine(Path.GetTempPath(), "ProjetSKE-mise-a-jour.cmd");
        File.WriteAllText(script, string.Join("\r\n",
            "@echo off",
            ":attente",
            $"tasklist /FI \"PID eq {pid}\" | find \"{pid}\" >nul && (timeout /t 1 /nobreak >nul & goto attente)",
            $"robocopy \"{staging}\" \"{appDir}\" /MIR /NFL /NDL /NJH /NJS /NP >nul",
            $"start \"\" \"{exe}\"",
            ""));
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
        MainThread.BeginInvokeOnMainThread(() => Application.Current?.Quit());
        return Task.FromResult("Mise à jour : le jeu va se fermer puis se relancer.");
    }
#else
    /// <summary>Téléphone : ouvre l'installateur d'Android avec l'APK.</summary>
    private static async Task<string> InstallAsync(string apk, ReleaseInfo release)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
            await Launcher.Default.OpenAsync(new OpenFileRequest("Mise à jour de Projet SKE",
                new ReadOnlyFile(apk, "application/vnd.android.package-archive"))));
        return "Installation lancée : confirme dans la fenêtre d'Android.";
    }
#endif
}
