using System.Net.Http.Headers;
using ProjetSKE.Core.Cloud;

namespace ProjetSKE.App;

/// <summary>
/// Mises à jour dans l'application : chaque APK compilé et testé par GitHub Actions est publié en « Release »
/// sur le dépôt. L'appli compare son numéro de compilation à la dernière Release, télécharge l'APK et lance
/// l'installation (Android demande une confirmation ; la première fois, il faut autoriser l'appli à installer).
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

    /// <summary>Numéro de compilation de l'appli installée (versionCode Android).</summary>
    public static int CurrentBuild => int.TryParse(AppInfo.Current.BuildString, out var b) ? b : 0;

    public static string CurrentVersion => $"{AppInfo.Current.VersionString} (compilation {AppInfo.Current.BuildString})";

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
                Latest = ReleaseInfo.Parse(await response.Content.ReadAsStringAsync());
                Status = Latest is null ? "Dernière version sans APK."
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

    /// <summary>Télécharge l'APK (progression de 0 à 1) puis ouvre l'installateur d'Android.</summary>
    public static async Task<string> DownloadAndInstallAsync(ReleaseInfo release, IProgress<double> progress)
    {
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, $"ProjetSKE-{release.Build}.apk");
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
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Launcher.Default.OpenAsync(new OpenFileRequest("Mise à jour de Projet SKE",
                    new ReadOnlyFile(path, "application/vnd.android.package-archive"))));
            return "Installation lancée : confirme dans la fenêtre d'Android.";
        }
        catch (Exception e)
        {
            return "Mise à jour impossible : " + e.Message;
        }
    }
}
