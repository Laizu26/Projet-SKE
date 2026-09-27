using ProjetSKE.Core.Cloud;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;

namespace ProjetSKE.App.Dev;

/// <summary>
/// Synchronisation automatique du contenu avec la base en ligne (Firestore).
/// À chaque synchronisation : lecture de la version en ligne, fusion élément par élément avec le contenu
/// local (voir <see cref="ContentMerger"/>), publication du résultat si besoin. Rien n'est jamais écrasé
/// sans trace : en cas de conflit la version en ligne est gardée et la locale est mise de côté (restaurable),
/// une copie locale est faite avant chaque changement et chaque révision publiée est archivée en ligne.
/// </summary>
public static class CloudSync
{
    private const string Prefix = "cloud.";

    // Projet Firebase du jeu, pré-rempli pour que tous les téléphones soient connectés sans rien saisir.
    // La clé API Firebase n'est pas un secret : l'accès est protégé par les règles Firestore et la connexion anonyme.
    public const string DefaultProjectId = "projet-ske-597e2";
    public const string DefaultApiKey = "AIzaSyAmrQR9V2OXqmaWLXP8gUCRz15snv6f-hY";

    /// <summary>Intervalle de la synchronisation automatique.</summary>
    public static readonly TimeSpan AutoInterval = TimeSpan.FromSeconds(45);

    private static readonly SemaphoreSlim Lock = new(1, 1);

    /// <summary>Déclenché après chaque synchronisation (pour rafraîchir l'écran).</summary>
    public static event Action? Changed;

    // ------------------------------------------------------------------ Réglages (sur l'appareil)

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

    public static string LastStatus { get; private set; } = "Pas encore synchronisé.";
    public static DateTime? LastSync { get; private set; }
    public static bool Busy { get; private set; }

    public static bool IsReady => Enabled && Settings.IsComplete;

    private static IContentRepository? _repository;
    private static IContentRepository Repository => _repository ??= new FirestoreContentRepository(Settings);

    // ------------------------------------------------------------------ Fichiers locaux

    private static string BasePath => Path.Combine(FileSystem.AppDataDirectory, "cloud-base.json");
    private static string ConflictsPath => Path.Combine(FileSystem.AppDataDirectory, "cloud-conflits.json");
    private static string BackupDir => Path.Combine(FileSystem.AppDataDirectory, "copies-contenu");

    /// <summary>Dernière version commune (point de départ de la fusion). Par défaut : le contenu officiel.</summary>
    private static GameContent LoadBase()
    {
        try
        {
            if (File.Exists(BasePath)) return ContentSerializer.FromJson(File.ReadAllText(BasePath));
        }
        catch (Exception) { }
        return ContentSerializer.Clone(GameDatabase.Default.Content);
    }

    private static void SaveBase(GameContent content) => File.WriteAllText(BasePath, ContentSerializer.ToJson(content));

    /// <summary>Copie locale avant tout changement (les 20 dernières sont gardées).</summary>
    private static void Backup(GameContent content, string label)
    {
        try
        {
            Directory.CreateDirectory(BackupDir);
            File.WriteAllText(Path.Combine(BackupDir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{label}.json"), ContentSerializer.ToJson(content));
            foreach (var old in Directory.GetFiles(BackupDir).OrderByDescending(f => f).Skip(20)) File.Delete(old);
        }
        catch (Exception) { }
    }

    public static int BackupCount => Directory.Exists(BackupDir) ? Directory.GetFiles(BackupDir).Length : 0;

    public static List<MergeConflict> Conflicts
    {
        get
        {
            try
            {
                return File.Exists(ConflictsPath) ? ContentMerger.ConflictsFromJson(File.ReadAllText(ConflictsPath)) : [];
            }
            catch (Exception) { return []; }
        }
    }

    private static void SaveConflicts(List<MergeConflict> list)
    {
        try { File.WriteAllText(ConflictsPath, ContentMerger.ConflictsToJson(list)); }
        catch (Exception) { }
    }

    public static void AddConflicts(IEnumerable<MergeConflict> conflicts)
    {
        var added = conflicts.ToList();
        if (added.Count == 0) return;
        var list = Conflicts;
        list.AddRange(added);
        SaveConflicts(list.TakeLast(50).ToList());
    }

    public static void Dismiss(MergeConflict conflict)
    {
        var list = Conflicts;
        list.RemoveAll(c => c.Kind == conflict.Kind && c.Id == conflict.Id && c.When == conflict.When);
        SaveConflicts(list);
    }

    /// <summary>Remet ta version d'un élément en conflit dans le brouillon (à enregistrer ensuite).</summary>
    public static bool RestoreMine(MergeConflict conflict)
    {
        if (!ContentMerger.Restore(DevState.Draft, conflict)) return false;
        DevState.Touch();
        Dismiss(conflict);
        return true;
    }

    // ------------------------------------------------------------------ Synchronisation

    /// <summary>Lance la synchronisation automatique : au démarrage, puis à intervalle régulier.</summary>
    public static void StartAuto(IDispatcher dispatcher)
    {
        _ = SyncAsync();
        dispatcher.StartTimer(AutoInterval, () =>
        {
            _ = SyncAsync();
            return true;
        });
    }

    /// <summary>Récupère, fusionne et publie. Sans danger si appelée souvent (une seule à la fois).</summary>
    public static async Task<string> SyncAsync()
    {
        if (!IsReady) return LastStatus = "Synchronisation désactivée.";
        if (!await Lock.WaitAsync(0)) return LastStatus;
        Busy = true;
        try
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var local = SkeApp.Db.Content;
                var remote = await Repository.PullAsync();

                if (remote is null)
                {
                    // Première publication.
                    var first = await Repository.PushAsync(local, null, 0, Author, firstPublish: true);
                    if (first.Status == PushStatus.Conflict) continue;
                    if (first.Status == PushStatus.Error) return LastStatus = "Publication impossible : " + first.Error;
                    SaveBase(local);
                    Remember(first.UpdateTime, first.Revision);
                    return Done($"Contenu publié en ligne (révision {first.Revision}).");
                }

                var @base = LoadBase();
                var localUnchanged = ContentSerializer.ToJson(local) == ContentSerializer.ToJson(@base);
                if (localUnchanged && remote.UpdateTime == BaseUpdateTime)
                    return Done($"À jour · révision {remote.Revision} ({remote.UpdatedBy}).");

                var merge = ContentMerger.Merge(@base, local, remote.Content);
                AddConflicts(merge.Conflicts);

                string? updateTime = remote.UpdateTime;
                var revision = remote.Revision;
                if (merge.HasLocalChanges)
                {
                    var push = await Repository.PushAsync(merge.Merged, remote.UpdateTime, remote.Revision, Author);
                    if (push.Status == PushStatus.Conflict) continue; // quelqu'un vient de publier : on refusionne
                    if (push.Status == PushStatus.Error) return LastStatus = "Publication impossible : " + push.Error;
                    updateTime = push.UpdateTime;
                    revision = push.Revision;
                }

                if (ContentSerializer.ToJson(merge.Merged) != ContentSerializer.ToJson(local))
                {
                    Backup(local, "avant-synchro");
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        SkeApp.ApplyContent(merge.Merged);
                        if (DevState.CanRefresh) DevState.Revert();
                    });
                }
                SaveBase(merge.Merged);
                Remember(updateTime, revision);

                var what = merge.HasLocalChanges ? "Vos modifications sont publiées" : $"Révision {revision} de {remote.UpdatedBy} récupérée";
                var warn = merge.Conflicts.Count > 0 ? $" · {merge.Conflicts.Count} conflit(s) à vérifier" : "";
                return Done($"{what}{warn}.");
            }
            return LastStatus = "Beaucoup de modifications en même temps : nouvel essai dans un instant.";
        }
        catch (Exception e)
        {
            return LastStatus = "Hors ligne : " + e.Message;
        }
        finally
        {
            Busy = false;
            Lock.Release();
            MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());
        }
    }

    private static void Remember(string? updateTime, int revision)
    {
        BaseUpdateTime = updateTime;
        BaseRevision = revision;
    }

    private static string Done(string status)
    {
        LastSync = DateTime.Now;
        return LastStatus = status;
    }

    /// <summary>Vérifie les réglages en lisant le document.</summary>
    public static async Task<string> TestAsync(CloudSettings settings)
    {
        try
        {
            var snap = await new FirestoreContentRepository(settings).PullAsync();
            return snap is null
                ? "Connexion réussie. La base est vide : la première synchronisation y publiera le contenu."
                : $"Connexion réussie. Révision {snap.Revision} publiée par {snap.UpdatedBy}.";
        }
        catch (Exception e)
        {
            return "Échec : " + e.Message;
        }
    }
}
