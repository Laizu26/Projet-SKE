using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Cloud;

/// <summary>Connexion à la base Firestore (même technologie que Service Impérial).</summary>
public sealed record CloudSettings(
    string ProjectId,
    string ApiKey,
    string Collection = "projet-ske",
    string Document = "contenu")
{
    public bool IsComplete => ProjectId.Length > 0 && ApiKey.Length > 0 && Collection.Length > 0 && Document.Length > 0;
}

/// <summary>Contenu lu en ligne, avec sa version.</summary>
public sealed record CloudSnapshot(GameContent Content, string UpdateTime, int Revision, string UpdatedBy, DateTime UpdatedAt);

public enum PushStatus { Ok, Conflict, Error }

public sealed record PushResult(PushStatus Status, string? UpdateTime = null, int Revision = 0, string? Error = null);

/// <summary>Endroit où le contenu du mode développeur est partagé entre tous les appareils.</summary>
public interface IContentRepository
{
    /// <summary>Lit le contenu en ligne (null si rien n'a encore été publié).</summary>
    Task<CloudSnapshot?> PullAsync(CancellationToken ct = default);

    /// <summary>
    /// Publie le contenu. <paramref name="expectedUpdateTime"/> = version sur laquelle on a travaillé :
    /// si quelqu'un a publié entre-temps, renvoie <see cref="PushStatus.Conflict"/> (rien n'est écrasé).
    /// null = écraser sans vérifier (ou premier envoi si <paramref name="firstPublish"/>).
    /// </summary>
    Task<PushResult> PushAsync(GameContent content, string? expectedUpdateTime, int baseRevision, string author,
        bool firstPublish = false, CancellationToken ct = default);

    /// <summary>Toutes les révisions publiées (historique), de la plus récente à la plus ancienne.</summary>
    Task<IReadOnlyList<CloudSnapshot>> HistoryAsync(CancellationToken ct = default);
}

/// <summary>Conversion entre le contenu du jeu et un document Firestore (API REST).</summary>
public static class FirestoreFormat
{
    /// <summary>Le contenu est rangé en JSON dans un seul champ texte (limite Firestore : 1 Mo par document).</summary>
    public static string BuildDocument(GameContent content, int revision, string author, DateTime now)
    {
        var fields = new JsonObject
        {
            ["json"] = new JsonObject { ["stringValue"] = ContentSerializer.ToJson(content) },
            ["revision"] = new JsonObject { ["integerValue"] = revision.ToString() },
            ["updatedBy"] = new JsonObject { ["stringValue"] = author },
            ["updatedAt"] = new JsonObject { ["timestampValue"] = now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
            ["title"] = new JsonObject { ["stringValue"] = content.Title },
        };
        return new JsonObject { ["fields"] = fields }.ToJsonString();
    }

    public static CloudSnapshot ParseDocument(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var fields = root.GetProperty("fields");
        string Str(string name, string kind) =>
            fields.TryGetProperty(name, out var f) && f.TryGetProperty(kind, out var v) ? v.GetString() ?? "" : "";

        var content = ContentSerializer.FromJson(Str("json", "stringValue"));
        var revision = int.TryParse(Str("revision", "integerValue"), out var r) ? r : 0;
        var updatedAt = DateTime.TryParse(Str("updatedAt", "timestampValue"), out var d) ? d : DateTime.MinValue;
        var updateTime = root.TryGetProperty("updateTime", out var ut) ? ut.GetString() ?? "" : "";
        return new CloudSnapshot(content, updateTime, revision, Str("updatedBy", "stringValue"), updatedAt);
    }
}

/// <summary>
/// Accès Firestore par l'API REST (pas de SDK natif nécessaire sur Android).
/// Authentification anonyme Firebase : les règles de sécurité peuvent exiger un utilisateur connecté.
/// </summary>
public sealed class FirestoreContentRepository : IContentRepository
{
    private readonly CloudSettings _settings;
    private readonly HttpClient _http;
    private string? _idToken;
    private string? _authError;
    private DateTime _tokenExpiry;

    public FirestoreContentRepository(CloudSettings settings, HttpClient? http = null)
    {
        _settings = settings;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    private string DocumentUrl =>
        $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(_settings.ProjectId)}/databases/(default)/documents/" +
        $"{Uri.EscapeDataString(_settings.Collection)}/{Uri.EscapeDataString(_settings.Document)}?key={Uri.EscapeDataString(_settings.ApiKey)}";

    /// <summary>Connexion anonyme (Firebase Auth). Si elle n'est pas activée, on continue sans jeton.</summary>
    private async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_idToken is null || DateTime.UtcNow >= _tokenExpiry)
        {
            _idToken = null;
            try
            {
                var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={Uri.EscapeDataString(_settings.ApiKey)}";
                using var response = await _http.PostAsync(url,
                    new StringContent("{\"returnSecureToken\":true}", Encoding.UTF8, "application/json"), ct);
                var authBody = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    // ADMIN_ONLY_OPERATION = connexion anonyme désactivée ; CONFIGURATION_NOT_FOUND = Authentication jamais ouvert.
                    _authError = authBody.Contains("ADMIN_ONLY_OPERATION") ? "la connexion « Anonyme » est désactivée"
                        : authBody.Contains("CONFIGURATION_NOT_FOUND") ? "Firebase Authentication n'est pas encore activé (bouton « Commencer »)"
                        : $"refus de Firebase Authentication ({(int)response.StatusCode})";
                }
                if (response.IsSuccessStatusCode)
                {
                    _authError = null;
                    using var doc = JsonDocument.Parse(authBody);
                    _idToken = doc.RootElement.GetProperty("idToken").GetString();
                    var seconds = int.TryParse(doc.RootElement.GetProperty("expiresIn").GetString(), out var s) ? s : 3600;
                    _tokenExpiry = DateTime.UtcNow.AddSeconds(seconds - 120);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Pas de connexion anonyme : les règles doivent alors autoriser l'accès public.
            }
        }
        if (_idToken is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _idToken);
    }

    public async Task<CloudSnapshot?> PullAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, DocumentUrl);
        await AuthorizeAsync(request, ct);
        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        // 404 = document pas encore publié… sauf si c'est la base elle-même qui n'existe pas.
        if (response.StatusCode == HttpStatusCode.NotFound && !IsMissingDatabase(body)) return null;
        if (!response.IsSuccessStatusCode) throw new HttpRequestException(Explain(response.StatusCode, body));
        return FirestoreFormat.ParseDocument(body);
    }

    public async Task<PushResult> PushAsync(GameContent content, string? expectedUpdateTime, int baseRevision, string author,
        bool firstPublish = false, CancellationToken ct = default)
    {
        var url = DocumentUrl;
        if (expectedUpdateTime is { Length: > 0 }) url += "&currentDocument.updateTime=" + Uri.EscapeDataString(expectedUpdateTime);
        else if (firstPublish) url += "&currentDocument.exists=false";

        var revision = baseRevision + 1;
        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(FirestoreFormat.BuildDocument(content, revision, author, DateTime.UtcNow), Encoding.UTF8, "application/json"),
        };
        try
        {
            await AuthorizeAsync(request, ct);
            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                var updateTime = doc.RootElement.TryGetProperty("updateTime", out var ut) ? ut.GetString() : null;
                await SaveHistoryAsync(content, revision, author, ct);
                return new PushResult(PushStatus.Ok, updateTime, revision);
            }
            // Précondition refusée = quelqu'un a publié une autre version entre-temps.
            if (body.Contains("FAILED_PRECONDITION") || body.Contains("ALREADY_EXISTS") || response.StatusCode == HttpStatusCode.Conflict)
                return new PushResult(PushStatus.Conflict, Error: "Le contenu en ligne a été modifié par quelqu'un d'autre.");
            var message = Explain(response.StatusCode, body);
            if (_authError is not null && (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized))
                message = $"Écriture refusée car {_authError}. Dans la console Firebase : Authentication → Méthode de connexion → Anonyme → Activer.";
            return new PushResult(PushStatus.Error, Error: message);
        }
        catch (Exception e)
        {
            return new PushResult(PushStatus.Error, Error: e.Message);
        }
    }

    /// <summary>
    /// Copie de chaque révision publiée dans la sous-collection « historique » : même en cas d'erreur,
    /// aucune version n'est jamais perdue. Échec silencieux (la publication principale a réussi).
    /// </summary>
    private async Task SaveHistoryAsync(GameContent content, int revision, string author, CancellationToken ct)
    {
        try
        {
            var url =
                $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(_settings.ProjectId)}/databases/(default)/documents/" +
                $"{Uri.EscapeDataString(_settings.Collection)}/{Uri.EscapeDataString(_settings.Document)}/historique/r{revision:D6}" +
                $"?key={Uri.EscapeDataString(_settings.ApiKey)}";
            using var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(FirestoreFormat.BuildDocument(content, revision, author, DateTime.UtcNow), Encoding.UTF8, "application/json"),
            };
            await AuthorizeAsync(request, ct);
            using var _ = await _http.SendAsync(request, ct);
        }
        catch (Exception)
        {
            // L'historique est un bonus : on n'échoue pas la publication pour lui.
        }
    }

    public async Task<IReadOnlyList<CloudSnapshot>> HistoryAsync(CancellationToken ct = default)
    {
        var list = new List<CloudSnapshot>();
        string? pageToken = null;
        do
        {
            var url =
                $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(_settings.ProjectId)}/databases/(default)/documents/" +
                $"{Uri.EscapeDataString(_settings.Collection)}/{Uri.EscapeDataString(_settings.Document)}/historique" +
                $"?key={Uri.EscapeDataString(_settings.ApiKey)}&pageSize=100" + (pageToken is null ? "" : "&pageToken=" + Uri.EscapeDataString(pageToken));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            await AuthorizeAsync(request, ct);
            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException(Explain(response.StatusCode, body));
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("documents", out var documents))
                foreach (var d in documents.EnumerateArray())
                {
                    try { list.Add(FirestoreFormat.ParseDocument(d.GetRawText())); }
                    catch (Exception) { /* révision illisible : ignorée */ }
                }
            pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
        } while (pageToken is not null && list.Count < 1000);
        return list.OrderByDescending(r => r.Revision).ToList();
    }

    private static string Short(string text) => text.Length > 200 ? text[..200] + "…" : text;

    /// <summary>« The database (default) does not exist » (base non créée), à distinguer de « Document … not found ».</summary>
    private static bool IsMissingDatabase(string body) =>
        body.Contains("does not exist", StringComparison.OrdinalIgnoreCase) && body.Contains("database", StringComparison.OrdinalIgnoreCase);

    /// <summary>Message clair pour les erreurs Firestore les plus courantes.</summary>
    public static string Explain(HttpStatusCode status, string body)
    {
        if (IsMissingDatabase(body))
            return "La base Firestore n'existe pas encore : crée-la dans la console Firebase (Firestore Database).";
        if (body.Contains("SERVICE_DISABLED") || body.Contains("has not been used", StringComparison.OrdinalIgnoreCase))
            return "L'API Firestore n'est pas activée pour ce projet (créer la base Firestore l'active).";
        if (body.Contains("API_KEY") || body.Contains("API key", StringComparison.OrdinalIgnoreCase))
            return "Clé API refusée : vérifie la clé et qu'elle n'est pas restreinte aux applications Android.";
        if (status == HttpStatusCode.Forbidden || body.Contains("PERMISSION_DENIED"))
            return "Accès refusé par les règles Firestore : ajoute les règles pour « projet-ske » et active la connexion anonyme.";
        if (status == HttpStatusCode.Unauthorized)
            return "Non authentifié : active la connexion « Anonyme » dans Firebase Authentication.";
        return $"Firestore {(int)status} : {Short(body)}";
    }
}
