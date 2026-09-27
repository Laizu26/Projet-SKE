using System.Globalization;
using System.Text;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;

namespace ProjetSKE.App.Dev;

/// <summary>Brouillon du contenu en cours d'édition dans le mode développeur.</summary>
public static class DevState
{
    private static GameContent? _draft;
    private static GameContent? _draftBase;

    /// <summary>Copie de travail : les modifications n'affectent le jeu qu'après « Enregistrer ».</summary>
    public static GameContent Draft
    {
        get
        {
            if (_draft is null) Reset();
            return _draft!;
        }
    }

    public static bool Dirty { get; private set; }

    public static void Touch() => Dirty = true;

    private static void Reset()
    {
        _draft = ContentSerializer.Clone(SkeApp.Db.Content);
        _draftBase = ContentSerializer.Clone(SkeApp.Db.Content);
    }

    public static void Replace(GameContent content)
    {
        if (_draft is null) Reset();
        _draft = content;
        Dirty = true;
    }

    /// <summary>Abandonne les modifications non enregistrées.</summary>
    public static void Revert()
    {
        Reset();
        Dirty = false;
    }

    /// <summary>
    /// Le brouillon peut être remplacé par la dernière version synchronisée sans rien perdre :
    /// pas de modification en cours, et aucun écran d'édition ouvert sur ses objets.
    /// </summary>
    public static bool CanRefresh
    {
        get
        {
            if (Dirty) return false;
            var page = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;
            return page is not EditorPage && page?.GetType().Name.StartsWith("EntityListPage") != true;
        }
    }

    /// <summary>
    /// Enregistre le brouillon en le fusionnant avec le contenu actif : ce que d'autres ont publié
    /// pendant l'édition n'est pas annulé. En cas de conflit sur un même élément, la version déjà active
    /// est gardée et la tienne est mise de côté (restaurable).
    /// </summary>
    public static IReadOnlyList<Core.Cloud.MergeConflict> Save()
    {
        var result = Core.Cloud.ContentMerger.Merge(_draftBase ?? SkeApp.Db.Content, Draft, SkeApp.Db.Content);
        SkeApp.ApplyContent(result.Merged);
        CloudSync.AddConflicts(result.Conflicts);
        Reset();
        Dirty = false;
        return result.Conflicts;
    }

    public static void ResetToOfficial()
    {
        SkeApp.ResetContent();
        Reset();
        Dirty = false;
    }

    public static IReadOnlyList<string> Validate() => new GameDatabase(Draft).Validate();

    /// <summary>Base de données construite sur une copie du brouillon (pour tester sans enregistrer).</summary>
    public static GameDatabase DraftDatabase() => new(ContentSerializer.Clone(Draft));

    /// <summary>Identifiant à partir d'un nom : "Épée de feu" → "epee_de_feu" (unique).</summary>
    public static string NewId(string name, IEnumerable<string> existing)
    {
        var sb = new StringBuilder();
        foreach (var ch in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        }
        var baseId = string.Join("_", sb.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries));
        if (baseId.Length == 0) baseId = "element";
        var taken = existing.ToHashSet();
        var id = baseId;
        for (var i = 2; taken.Contains(id); i++) id = $"{baseId}_{i}";
        return id;
    }

    // ------------------------------------------------------------------ Listes de choix pour les menus déroulants

    public static IEnumerable<(string Id, string Name)> Skills => Draft.Skills.Select(x => (x.Id, x.Name));
    public static IEnumerable<(string Id, string Name)> Characters => Draft.Characters.Select(x => (x.Id, x.Name));
    public static IEnumerable<(string Id, string Name)> Monsters => Draft.Monsters.Select(x => (x.Id, x.Name));
    public static IEnumerable<(string Id, string Name)> Locations => Draft.Locations.Select(x => (x.Id, x.Name));
    public static IEnumerable<(string Id, string Name)> Npcs => Draft.Npcs.Select(x => (x.Id, x.Name));
    public static IEnumerable<(string Id, string Name)> Dialogues => Draft.Dialogues.Select(x => (x.Id, x.Name.Length > 0 ? x.Name : x.Id));
    public static IEnumerable<(string Id, string Name)> Quests => Draft.Quests.Select(x => (x.Id, x.Name));

    public static IEnumerable<(string Id, string Name)> Items(Func<ItemDef, bool>? filter = null) =>
        Draft.Items.Where(i => filter?.Invoke(i) ?? true).Select(x => (x.Id, x.Name));

    // ------------------------------------------------------------------ Noms lisibles des énumérations

    public static string Name(ActionType t) => t switch
    {
        ActionType.SetFlag => "Poser un flag",
        ActionType.ClearFlag => "Retirer un flag",
        ActionType.Recruit => "Recruter un PJ",
        ActionType.GiveItem => "Donner un objet",
        ActionType.TakeItem => "Prendre un objet",
        ActionType.GiveGold => "Donner de l'or",
        ActionType.TakeGold => "Prendre de l'or",
        ActionType.GiveXp => "Donner de l'XP",
        ActionType.StartBattle => "Lancer un combat",
        ActionType.StartQuest => "Démarrer une quête",
        ActionType.CompleteQuest => "Terminer une quête",
        ActionType.HealParty => "Soigner l'équipe",
        _ => "Téléporter",
    };

    public static string Name(ConditionType t) => t switch
    {
        ConditionType.FlagSet => "Flag posé",
        ConditionType.FlagNotSet => "Flag absent",
        ConditionType.QuestNotStarted => "Quête pas commencée",
        ConditionType.QuestActive => "Quête en cours",
        ConditionType.QuestCompleted => "Quête terminée",
        ConditionType.HasItem => "Possède l'objet",
        ConditionType.InParty => "PJ dans l'équipe",
        ConditionType.NotInParty => "PJ pas dans l'équipe",
        ConditionType.GoldAtLeast => "Or minimum",
        _ => "Niveau minimum",
    };

    public static string Name(ObjectiveType t) => t switch
    {
        ObjectiveType.TalkTo => "Parler à un PNJ",
        ObjectiveType.Defeat => "Vaincre un monstre",
        ObjectiveType.Reach => "Aller à un lieu",
        _ => "Apporter un objet",
    };

    public static string Name(ItemType t) => t switch
    {
        ItemType.Consumable => "Consommable",
        ItemType.Weapon => "Arme",
        ItemType.Armor => "Armure",
        ItemType.Relic => "Relique",
        _ => "Objet de quête",
    };

    public static string Name(RelicUsage t) => t == RelicUsage.Quest ? "Objet de quête" : "Équipable";

    public static string Name(SkillKind t) => t switch
    {
        SkillKind.Physical => "Physique (ATQ)",
        SkillKind.Magical => "Magique (MAG)",
        _ => "Soin (MAG)",
    };

    public static string Name(SkillTarget t) => t switch
    {
        SkillTarget.SingleEnemy => "Un ennemi",
        SkillTarget.AllEnemies => "Tous les ennemis",
        SkillTarget.SingleAlly => "Un allié",
        SkillTarget.AllAllies => "Tous les alliés",
        _ => "Soi-même",
    };

    public static string Name(LocationType t) => t switch
    {
        LocationType.City => "Ville (boutique, auberge)",
        LocationType.Dungeon => "Donjon",
        _ => "Nature",
    };
}
