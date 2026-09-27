using ProjetSKE.Core.Cloud;
using ProjetSKE.Core.Models;

namespace ProjetSKE.App.Dev;

/// <summary>
/// Synchronisation du contenu avec la base en ligne (Firestore) : le mode développeur est ainsi
/// partagé et modifiable depuis tous les téléphones. Les réglages restent sur l'appareil
/// (jamais dans le dépôt Git).
/// </summary>
public static class CloudSync
{
    private const string Prefix = "cloud.";

    // Projet Firebase du jeu, pré-rempli pour que tous les téléphones soient connectés sans rien saisir.
    // La clé API Firebase n'est pas un secret : l'accès est protégé par les règles Firestore et la connexion anonyme.
    public const string DefaultProjectId = "projet-ske-597e2";
    public const string DefaultApiKey = "AIzaSyAmrQR9V2OXqmaWLXP8gUCRz15snv6f-hY";

    public static bool Enabled
    {
        get => Preferences.Default.Get(Prefix + "enabled", true);
        set => Preferences.Default.Set(Prefix + "enabled", value);
    }

    public static CloudSettings Settings
    {
        get => new(
            Preferences.Default.Get(Prefix + "project", DefaultProjectId),
            Preferences.Default.Get(Prefix + "key", DefaultApiKey),
            Preferences.Default.Get(Prefix + "collection", "projet-ske"),
            Preferences.Default.Get(Prefix + "document", "contenu"));
        set
        {
            Preferences.Default.Set(Prefix + "project", value.ProjectId.Trim());
            Preferences.Default.Set(Prefix + "key", value.ApiKey.Trim());
            Preferences.Default.Set(Prefix + "collection", value.Collection.Trim());
            Preferences.Default.Set(Prefix + "document", value.Document.Trim());
            _repository = null;
        }
    }

    /// <summary>Nom affiché dans l'historique des publications.</summary>
    public static string Author
    {
        get => Preferences.Default.Get(Prefix + "author", "Développeur");
        set => Preferences.Default.Set(Prefix + "author", value);
    }

    /// <summary>Version en ligne sur laquelle repose le contenu local.</summary>
    public static string? BaseUpdateTime
    {
        get => Preferences.Default.Get<string?>(Prefix + "updateTime", null);
        private set
        {
            if (value is null) Preferences.Default.Remove(Prefix + "updateTime");
            else Preferences.Default.Set(Prefix + "updateTime", value);
        }
    }

    public static int BaseRevision
    {
        get => Preferences.Default.Get(Prefix + "revision", 0);
        private set => Preferences.Default.Set(Prefix + "revision", value);
    }

    public static string LastStatus { get; private set; } = "Non connecté.";

    public static bool IsReady => Enabled && Settings.IsComplete;

    private static IContentRepository? _repository;
    private static IContentRepository Repository => _repository ??= new FirestoreContentRepository(Settings);

    private static void Remember(string? updateTime, int revision)
    {
        BaseUpdateTime = updateTime;
        BaseRevision = revision;
    }

    /// <summary>Au lancement : récupère la dernière version en ligne si elle a changé.</summary>
    public static async Task<bool> PullIfNewerAsync()
    {
        if (!IsReady) return false;
        try
        {
            var snap = await Repository.PullAsync();
            if (snap is null)
            {
                LastStatus = "Base en ligne vide : publiez votre contenu.";
                return false;
            }
            if (snap.UpdateTime == BaseUpdateTime)
            {
                LastStatus = $"À jour · révision {snap.Revision} ({snap.UpdatedBy}).";
                return false;
            }
            SkeApp.ApplyContent(snap.Content);
            DevState.Revert();
            Remember(snap.UpdateTime, snap.Revision);
            LastStatus = $"Nouvelle version récupérée · révision {snap.Revision} par {snap.UpdatedBy}.";
            return true;
        }
        catch (Exception e)
        {
            LastStatus = "Hors ligne : " + e.Message;
            return false;
        }
    }

    /// <summary>Remplace le brouillon par la version en ligne (et l'active).</summary>
    public static async Task<string> PullAsync()
    {
        if (!IsReady) return "Base en ligne non configurée.";
        try
        {
            var snap = await Repository.PullAsync();
            if (snap is null) return LastStatus = "Rien n'a encore été publié en ligne.";
            SkeApp.ApplyContent(snap.Content);
            DevState.Revert();
            Remember(snap.UpdateTime, snap.Revision);
            return LastStatus = $"Récupéré : révision {snap.Revision} par {snap.UpdatedBy} ({snap.UpdatedAt.ToLocalTime():dd/MM HH:mm}).";
        }
        catch (Exception e)
        {
            return LastStatus = "Échec de la récupération : " + e.Message;
        }
    }

    /// <summary>Publie le contenu. En cas de conflit, rien n'est écrasé sauf si <paramref name="force"/>.</summary>
    public static async Task<PushResult> PushAsync(GameContent content, bool force = false)
    {
        if (!IsReady) return new PushResult(PushStatus.Error, Error: "Base en ligne non configurée.");
        var expected = force ? null : BaseUpdateTime;
        var result = await Repository.PushAsync(content, expected, BaseRevision, Author, firstPublish: !force && BaseUpdateTime is null);
        if (result.Status == PushStatus.Ok)
        {
            Remember(result.UpdateTime, result.Revision);
            LastStatus = $"Publié : révision {result.Revision}.";
        }
        else
        {
            LastStatus = result.Error ?? "Erreur inconnue.";
        }
        return result;
    }

    /// <summary>Vérifie les réglages en lisant le document.</summary>
    public static async Task<string> TestAsync(CloudSettings settings)
    {
        try
        {
            var snap = await new FirestoreContentRepository(settings).PullAsync();
            return snap is null
                ? "Connexion réussie. La base est vide : utilisez « Publier » pour y envoyer votre contenu."
                : $"Connexion réussie. Révision {snap.Revision} publiée par {snap.UpdatedBy}.";
        }
        catch (Exception e)
        {
            return "Échec : " + e.Message;
        }
    }
}
