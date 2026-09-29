using System.Globalization;
using System.Text;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using Condition = ProjetSKE.Core.Models.Condition;

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

    // Le brouillon est gardé sur le téléphone à chaque modification : une fermeture de l'application
    // ou une mise à jour ne fait rien perdre, même sans avoir appuyé sur « Enregistrer ».
    private static string DraftPath => Path.Combine(FileSystem.AppDataDirectory, "brouillon.json");
    private static string DraftBasePath => Path.Combine(FileSystem.AppDataDirectory, "brouillon-base.json");
    private static CancellationTokenSource? _pendingWrite;
    private static bool _restoreChecked;

    public static void Touch()
    {
        Dirty = true;
        _pendingWrite?.Cancel();
        var cts = _pendingWrite = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(700, cts.Token);
                MainThread.BeginInvokeOnMainThread(WriteDraft);
            }
            catch (OperationCanceledException) { }
        });
    }

    private static readonly object WriteLock = new();

    private static void WriteDraft()
    {
        lock (WriteLock)
        {
            try
            {
                if (_draft is null || !Dirty) return;
                File.WriteAllText(DraftPath, ContentSerializer.ToJson(_draft));
                if (_draftBase is not null) File.WriteAllText(DraftBasePath, ContentSerializer.ToJson(_draftBase));
            }
            catch (Exception) { }
        }
    }

    private static void DeleteDraftFiles()
    {
        _pendingWrite?.Cancel();
        lock (WriteLock)
        {
            try
            {
                if (File.Exists(DraftPath)) File.Delete(DraftPath);
                if (File.Exists(DraftBasePath)) File.Delete(DraftBasePath);
            }
            catch (Exception) { }
        }
    }

    /// <summary>Un brouillon non enregistré a été retrouvé au démarrage.</summary>
    public static bool Restored { get; private set; }

    private static void Reset()
    {
        if (!_restoreChecked)
        {
            _restoreChecked = true;
            try
            {
                if (File.Exists(DraftPath))
                {
                    _draft = ContentSerializer.FromJson(File.ReadAllText(DraftPath));
                    _draftBase = File.Exists(DraftBasePath)
                        ? ContentSerializer.FromJson(File.ReadAllText(DraftBasePath))
                        : ContentSerializer.Clone(SkeApp.Db.Content);
                    Dirty = true;
                    Restored = true;
                    return;
                }
            }
            catch (Exception) { }
        }
        _draft = ContentSerializer.Clone(SkeApp.Db.Content);
        _draftBase = ContentSerializer.Clone(SkeApp.Db.Content);
    }

    public static void Replace(GameContent content)
    {
        if (_draft is null) Reset();
        _draft = content;
        Touch();
    }

    /// <summary>Abandonne les modifications non enregistrées.</summary>
    public static void Revert()
    {
        _restoreChecked = true;
        DeleteDraftFiles();
        Dirty = false;
        Restored = false;
        Reset();
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
        CloudSync.Backup(SkeApp.Db.Content, "avant-enregistrement");
        SkeApp.ApplyContent(result.Merged);
        CloudSync.AddConflicts(result.Conflicts);
        DeleteDraftFiles();
        Dirty = false;
        Restored = false;
        _restoreChecked = true;
        Reset();
        return result.Conflicts;
    }

    public static void ResetToOfficial()
    {
        CloudSync.Backup(SkeApp.Db.Content, "avant-contenu-origine");
        SkeApp.ResetContent();
        Revert();
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
    /// <summary>Partie de l'interface qu'on peut verrouiller (prologue).</summary>
    public static string Name(UiFeature f) => f switch
    {
        UiFeature.TabCamp => "Onglet Camp",
        UiFeature.TabMap => "Onglet Carte",
        UiFeature.TabQuests => "Onglet Quêtes",
        UiFeature.TabEncyclopedia => "Onglet Encyclopédie",
        UiFeature.TabShop => "Onglet Boutique",
        UiFeature.TabJournal => "Onglet Journal",
        UiFeature.TabMenu => "Onglet Menu",
        UiFeature.WorldMap => "Carte du royaume (voyager)",
        UiFeature.Explore => "Explorer (chercher le combat)",
        UiFeature.BattleSkills => "Combat : compétences (sinon seulement la première)",
        UiFeature.BattleItems => "Combat : objets",
        UiFeature.BattleFlee => "Combat : fuir",
        UiFeature.BattleDefend => "Combat : se défendre",
        _ => f.ToString(),
    };

    /// <summary>Lieu qui contient ce lieu dans le brouillon (null = sur la carte du royaume).</summary>
    public static LocationDef? ParentOf(LocationDef loc) =>
        loc.ParentId is { Length: > 0 } p && p != loc.Id ? Draft.Locations.FirstOrDefault(l => l.Id == p) : null;

    /// <summary>Chemin depuis le lieu du royaume jusqu'à ce lieu (s'arrête en cas de boucle).</summary>
    public static List<LocationDef> PathOf(LocationDef loc)
    {
        var path = new List<LocationDef> { loc };
        for (var cur = ParentOf(loc); cur is not null && path.Count < 32 && !path.Contains(cur); cur = ParentOf(cur)) path.Insert(0, cur);
        return path;
    }

    /// <summary>Adversaires possibles : les monstres, puis les PNJ qui savent se battre.</summary>
    public static IEnumerable<(string Id, string Name)> Monsters => Draft.Monsters.Select(x => (x.Id, x.Name))
        .Concat(Draft.Npcs.Where(n => n.Combat is not null).Select(n => (n.Id, "PNJ · " + n.Name)));
    /// <summary>Lieux, avec leur chemin pour les sous-lieux (ex : « Havrefort › Taverne »).</summary>
    public static IEnumerable<(string Id, string Name)> Locations => Draft.Locations.Select(x => (x.Id, string.Join(" › ", PathOf(x).Select(l => l.Name))));
    public static IEnumerable<(string Id, string Name)> Npcs => Draft.Npcs.Select(x => (x.Id, x.Name));
    public static IEnumerable<(string Id, string Name)> Dialogues => Draft.Dialogues.Select(x => (x.Id, x.Name.Length > 0 ? x.Name : x.Id));
    public static IEnumerable<(string Id, string Name)> Quests => Draft.Quests.Select(x => (x.Id, x.Name));

    /// <summary>Choix d'un dialogue, pour la condition « A choisi » : « réplique:choix ».</summary>
    public static IEnumerable<(string Id, string Name)> ChoicesOf(string dialogueId) =>
        Draft.Dialogues.FirstOrDefault(d => d.Id == dialogueId)?.Nodes.SelectMany(n => n.Choices.Select(c =>
            ($"{n.Id}:{n.ChoiceKey(c)}", $"{n.Id} › « {(c.Text.Length > 40 ? c.Text[..40] + "…" : c.Text)} »"))) ?? [];
    public static IEnumerable<(string Id, string Name)> PartQuests => Draft.Quests.Where(q => q.HasParts).Select(x => (x.Id, x.Name));
    public static IEnumerable<(string Id, string Name)> PartsOf(string questId) =>
        Draft.Quests.FirstOrDefault(q => q.Id == questId)?.Parts.Select(p => (p.Id, p.Name.Length > 0 ? p.Name : p.Id)) ?? [];
    public static IEnumerable<(string Id, string Name)> StagedQuests => Draft.Quests.Where(q => q.IsStaged).Select(x => (x.Id, x.Name));

    public static IEnumerable<(string Id, string Name)> StagesOf(string questId, bool endingsOnly = false) =>
        Draft.Quests.FirstOrDefault(q => q.Id == questId)?.Stages.Where(s => !endingsOnly || s.IsEnding)
            .Select(s => (s.Id, (s.Name.Length > 0 ? s.Name : s.Id) + (s.IsEnding ? (s.Failure ? " (fin, échec)" : " (fin)") : "")))
        ?? [];

    /// <summary>Résumé court d'une condition (carte des quêtes).</summary>
    public static string Describe(Condition c)
    {
        var text = Name(c.Type);
        if (c.Arg.Length > 0) text += " " + c.Arg;
        if (c.Arg2.Length > 0) text += " › " + c.Arg2;
        if (c.Type is ConditionType.Variable or ConditionType.Karma or ConditionType.Friendship or ConditionType.Gold
            or ConditionType.Level or ConditionType.PartySize or ConditionType.Day or ConditionType.CampRank or ConditionType.CampResource)
            text += " " + Name(c.Op).Split(' ').Last().Trim('(', ')') + " " + c.Amount;
        if (c.Type is ConditionType.AnyOf or ConditionType.AllOf) text += $" ({c.Children?.Count ?? 0})";
        return (c.Negate ? "sauf " : "") + text;
    }

    public static IEnumerable<(string Id, string Name)> CampRanks => Draft.Camp.Ranks.OrderByDescending(r => r.Level).Select(x => (x.Id, $"{x.Name} (niveau {x.Level})"));
    public static IEnumerable<(string Id, string Name)> CampTasks => Draft.Camp.Tasks.Select(x => (x.Id, x.Name));
    public static IEnumerable<(string Id, string Name)> CampResources => Draft.Camp.Resources.Select(x => (x.Id, x.Name));
    public static IEnumerable<(string Id, string Name)> CampBuildings => Draft.Camp.Buildings.Select(x => (x.Id, x.Name));

    /// <summary>Membre du camp : celui qui fait la tâche (dans un résultat de tâche), un PNJ ou un PJ.</summary>
    public static IEnumerable<(string Id, string Name)> CampWho =>
        new[] { ("@membre", "Celui qui fait la tâche"), ("@parle", "Celui qui parle") }.Concat(Persons);

    public static IEnumerable<(string Id, string Name)> Variables => Draft.Variables.Select(x => (x.Id, x.Name));
    public static IEnumerable<(string Id, string Name)> Portraits => Draft.Portraits.Select(x => (x.Id, x.Name));

    /// <summary>PNJ et PJ (pour l'amitié : qui ressent).</summary>
    public static IEnumerable<(string Id, string Name)> Persons =>
        Draft.Npcs.Select(x => (x.Id, "PNJ " + x.Name)).Concat(Draft.Characters.Select(x => (x.Id, "PJ " + x.Name)));

    /// <summary>De qui (karma) : celui qui parle par défaut.</summary>
    public static IEnumerable<(string Id, string Name)> KarmaWho =>
        new[] { ("@parle", "Celui qui parle"), ("@heros", "Le héros"), ("@equipe", "Toute l'équipe (moyenne / chacun)"), ("@membre", "Celui qui fait la tâche (camp)") }
            .Concat(Draft.Characters.Select(x => (x.Id, x.Name)));

    /// <summary>Envers qui (amitié) : l'équipe entière par défaut.</summary>
    public static IEnumerable<(string Id, string Name)> Toward =>
        new[] { ("@equipe", "L'équipe entière"), ("@parle", "Celui qui parle"), ("@heros", "Le héros") }
            .Concat(Draft.Characters.Select(x => (x.Id, x.Name)));

    public static IEnumerable<(string Id, string Name)> Items(Func<ItemDef, bool>? filter = null) =>
        Draft.Items.Where(i => filter?.Invoke(i) ?? true).Select(x => (x.Id, x.Name));

    // ------------------------------------------------------------------ Noms lisibles des énumérations

    public static string Name(ActionType t) => t switch
    {
        ActionType.SetFlag => "Flag : poser",
        ActionType.ClearFlag => "Flag : retirer",
        ActionType.Recruit => "Équipe : recruter un PJ",
        ActionType.GiveItem => "Objet : donner",
        ActionType.TakeItem => "Objet : prendre",
        ActionType.GiveGold => "Or : donner",
        ActionType.TakeGold => "Or : prendre (payer)",
        ActionType.GiveXp => "XP : donner",
        ActionType.StartBattle => "Combat : lancer",
        ActionType.StartQuest => "Quête : ajouter (démarrer)",
        ActionType.CompleteQuest => "Quête : terminer",
        ActionType.HealParty => "Équipe : soigner",
        ActionType.Teleport => "Lieu : téléporter l'équipe",
        ActionType.SetVariable => "Variable : fixer",
        ActionType.AddVariable => "Variable : ajouter",
        ActionType.AddKarma => "Karma : ajouter",
        ActionType.SetKarma => "Karma : fixer",
        ActionType.AddFriendship => "Amitié : ajouter",
        ActionType.SetFriendship => "Amitié : fixer",
        ActionType.AdvanceTime => "Temps : faire passer",
        ActionType.WaitUntilHour => "Temps : attendre une heure précise",
        ActionType.ShowMessage => "Message : afficher",
        ActionType.LeaveParty => "Équipe : un PJ s'en va",
        ActionType.MoveNpc => "PNJ : déplacer",
        ActionType.RevealLocation => "Lieu : révéler",
        ActionType.HideLocation => "Lieu : cacher",
        ActionType.JoinCamp => "Camp : rejoindre",
        ActionType.LeaveCamp => "Camp : quitter",
        ActionType.SetCampRank => "Camp : changer de grade",
        ActionType.SetCampTask => "Camp : affecter à une tâche",
        ActionType.AddCampResource => "Camp : ressource (ajouter / retirer)",
        ActionType.BuildCampBuilding => "Camp : construire un lieu (gratuit)",
        ActionType.SetQuestStage => "Quête : aller à l'étape",
        ActionType.StartDialogue => "Dialogue : lancer",
        ActionType.UnlockFeature => "Interface : débloquer (onglet, commande...)",
        ActionType.LockFeature => "Interface : verrouiller",
        ActionType.EndTutorial => "Prologue : terminer (aller au choix du héros)",
        ActionType.StartQuestPart => "Quête : démarrer une partie",
        ActionType.CompleteQuestPart => "Quête : terminer une partie",
        ActionType.FailQuestPart => "Quête : échouer une partie",
        _ => "Quête : échouer",
    };

    public static string Name(CompareOp t) => t switch
    {
        CompareOp.AtLeast => "au moins (≥)",
        CompareOp.AtMost => "au plus (≤)",
        CompareOp.Equal => "égal à (=)",
        CompareOp.NotEqual => "différent de (≠)",
        CompareOp.Greater => "plus de (>)",
        _ => "moins de (<)",
    };

    public static string Name(BattleTrigger t) => t switch
    {
        BattleTrigger.Start => "Début du combat",
        BattleTrigger.Turn => "Début d'un tour",
        BattleTrigger.HpBelow => "PV sous un seuil",
        BattleTrigger.Down => "Quand il tombe K.O.",
        BattleTrigger.Kill => "Quand il met K.O.",
        BattleTrigger.Victory => "Victoire de l'équipe",
        _ => "Défaite de l'équipe",
    };

    public static string Name(ConditionType t) => t switch
    {
        ConditionType.FlagSet => "Flag posé",
        ConditionType.FlagNotSet => "Flag absent",
        ConditionType.QuestNotStarted => "Quête : pas commencée",
        ConditionType.QuestActive => "Quête : en cours",
        ConditionType.QuestCompleted => "Quête : terminée",
        ConditionType.HasItem => "Possède l'objet",
        ConditionType.InParty => "PJ dans l'équipe",
        ConditionType.NotInParty => "PJ pas dans l'équipe",
        ConditionType.GoldAtLeast => "Or minimum",
        ConditionType.LevelAtLeast => "Niveau minimum",
        ConditionType.Variable => "Variable",
        ConditionType.Karma => "Karma",
        ConditionType.Friendship => "Amitié",
        ConditionType.Gold => "Or (comparaison)",
        ConditionType.Level => "Niveau (comparaison)",
        ConditionType.PartySize => "Taille de l'équipe",
        ConditionType.Speaker => "Qui parle",
        ConditionType.IsHero => "Être : (PJ incarné par le joueur)",
        ConditionType.ChoiceMade => "A choisi (un choix de dialogue)",
        ConditionType.HourBetween => "Entre deux heures",
        ConditionType.Day => "Jour n°",
        ConditionType.Period => "Moment de la journée",
        ConditionType.WeekDay => "Jour de la semaine",
        ConditionType.Month => "Mois",
        ConditionType.AtLocation => "Se trouve à un lieu",
        ConditionType.Visited => "A déjà visité un lieu",
        ConditionType.MetNpc => "A déjà parlé à un PNJ",
        ConditionType.Chance => "Hasard (%)",
        ConditionType.AnyOf => "Groupe OU : au moins une de ces conditions",
        ConditionType.AllOf => "Groupe ET : toutes ces conditions",
        ConditionType.CampMember => "Camp : est membre",
        ConditionType.CampRank => "Camp : grade (niveau)",
        ConditionType.CampTask => "Camp : fait la tâche",
        ConditionType.CampResource => "Camp : ressource (stock)",
        ConditionType.CampBuilt => "Camp : lieu construit",
        ConditionType.QuestAtStage => "Quête : à l'étape",
        ConditionType.QuestStageReached => "Quête : étape déjà passée",
        ConditionType.QuestEnding => "Quête : finie par",
        ConditionType.QuestPartNotStarted => "Partie de quête : pas commencée",
        ConditionType.QuestPartActive => "Partie de quête : en cours",
        ConditionType.QuestPartCompleted => "Partie de quête : terminée",
        ConditionType.QuestPartFailed => "Partie de quête : échouée",
        _ => "Quête échouée",
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
        SkillKind.Heal => "Soin (MAG)",
        SkillKind.Status => "Effets seulement (bonus, poison...)",
        _ => "Résurrection (allié K.O.)",
    };

    public static string Name(EffectType t) => t switch
    {
        EffectType.Poison => "Poison (PV perdus par tour)",
        EffectType.Regen => "Régénération (PV rendus par tour)",
        EffectType.Stun => "Étourdissement (perd ses tours)",
        EffectType.StatUp => "Bonus de stat (%)",
        EffectType.StatDown => "Malus de stat (%)",
        EffectType.Shield => "Bouclier (absorbe des dégâts)",
        _ => "Purification (retire les effets négatifs)",
    };

    public static string Name(StatKind t) => t switch
    {
        StatKind.Attack => "Attaque",
        StatKind.Defense => "Défense",
        StatKind.Magic => "Magie",
        _ => "Vitesse",
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
