using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Data;

/// <summary>Contenu du jeu indexé par identifiant, prêt à être utilisé par les règles.</summary>
public sealed class GameDatabase
{
    public GameContent Content { get; }
    public IReadOnlyDictionary<string, SkillDef> Skills { get; }
    public IReadOnlyDictionary<string, ItemDef> Items { get; }
    public IReadOnlyDictionary<string, CharacterDef> Characters { get; }
    public IReadOnlyDictionary<string, MonsterDef> Monsters { get; }
    public IReadOnlyDictionary<string, LocationDef> Locations { get; }
    public IReadOnlyDictionary<string, NpcDef> Npcs { get; }
    public IReadOnlyDictionary<string, DialogueDef> Dialogues { get; }
    public IReadOnlyDictionary<string, QuestDef> Quests { get; }
    public IReadOnlyDictionary<string, VariableDef> Variables { get; }
    public IReadOnlyDictionary<string, PortraitDef> Portraits { get; }

    public StartSettings Start => Content.Start;

    /// <summary>Tous les départs : le principal puis les autres.</summary>
    public IReadOnlyList<StartSettings> Starts => [Content.Start, .. Content.ExtraStarts];

    public StartSettings StartById(string? id) => Starts.FirstOrDefault(s => s.Id == id) ?? Content.Start;

    /// <summary>Départs possibles pour un héros.</summary>
    public IReadOnlyList<StartSettings> StartsFor(string heroId) =>
        Starts.Where(s => s.HeroIds.Count == 0 || s.HeroIds.Contains(heroId)).ToList();
    public BalanceSettings Balance => Content.Balance;

    public GameDatabase(GameContent content)
    {
        Content = content;
        // En cas d'identifiant en double, le dernier gagne (la validation le signale).
        Skills = Index(content.Skills, s => s.Id);
        Items = Index(content.Items, i => i.Id);
        Characters = Index(content.Characters, c => c.Id);
        Monsters = Index(content.Monsters, m => m.Id);
        Locations = Index(content.Locations, l => l.Id);
        Npcs = Index(content.Npcs, n => n.Id);
        Dialogues = Index(content.Dialogues, d => d.Id);
        Quests = Index(content.Quests, q => q.Id);
        Variables = Index(content.Variables, v => v.Id);
        Portraits = Index(content.Portraits, p => p.Id);
    }

    /// <summary>
    /// Portrait de celui qui parle : portrait imposé par la réplique, sinon celui du PJ, PNJ ou monstre
    /// portant ce nom. Null s'il n'y en a pas.
    /// </summary>
    public PortraitDef? PortraitFor(string speakerName, string? portraitId = null)
    {
        if (portraitId is not null && Portraits.TryGetValue(portraitId, out var forced)) return forced;
        if (speakerName.Length == 0) return null;
        var id = Content.Npcs.FirstOrDefault(n => n.Name == speakerName && n.PortraitId is not null)?.PortraitId
            ?? Content.Characters.FirstOrDefault(c => c.Name == speakerName && c.PortraitId is not null)?.PortraitId
            ?? Content.Monsters.FirstOrDefault(m => m.Name == speakerName && m.PortraitId is not null)?.PortraitId;
        return id is not null && Portraits.TryGetValue(id, out var p) ? p : null;
    }

    /// <summary>Texte de l'interface (renommable dans le mode développeur).</summary>
    public string T(string key) => Vocabulary.Get(Content, key);

    private static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> key)
    {
        var dict = new Dictionary<string, T>();
        foreach (var item in items) dict[key(item)] = item;
        return dict;
    }

    private static GameDatabase? _default;

    /// <summary>Contenu officiel fourni avec le jeu.</summary>
    public static GameDatabase Default => _default ??= new GameDatabase(ContentSerializer.LoadDefault());

    public IEnumerable<CharacterDef> Starters => Content.Characters.Where(c => c.IsStarter);

    public IEnumerable<NpcDef> NpcsAt(string locationId) => Content.Npcs.Where(n => n.LocationId == locationId);

    /// <summary>Vérifie que toutes les références entre contenus existent. Renvoie la liste des problèmes.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        void Check(bool ok, string message) { if (!ok) errors.Add(message); }
        void Ref<T>(IReadOnlyDictionary<string, T> dict, string? id, string where, string what)
        {
            if (!string.IsNullOrEmpty(id)) Check(dict.ContainsKey(id), $"{where} : {what} « {id} » introuvable");
        }

        CheckIds(Content.Skills.Select(x => x.Id), "Compétence");
        CheckIds(Content.Items.Select(x => x.Id), "Objet");
        CheckIds(Content.Characters.Select(x => x.Id), "Personnage");
        CheckIds(Content.Monsters.Select(x => x.Id), "Monstre");
        CheckIds(Content.Locations.Select(x => x.Id), "Lieu");
        CheckIds(Content.Npcs.Select(x => x.Id), "PNJ");
        CheckIds(Content.Dialogues.Select(x => x.Id), "Dialogue");
        CheckIds(Content.Quests.Select(x => x.Id), "Quête");
        CheckIds(Content.Variables.Select(x => x.Id), "Variable");
        CheckIds(Content.Portraits.Select(x => x.Id), "Portrait");
        foreach (var c in Content.Characters) Ref(Portraits, c.PortraitId, $"Personnage {c.Id}", "portrait");
        foreach (var n in Content.Npcs) Ref(Portraits, n.PortraitId, $"PNJ {n.Id}", "portrait");
        foreach (var m in Content.Monsters) Ref(Portraits, m.PortraitId, $"Monstre {m.Id}", "portrait");
        var camp = Content.Camp;
        CheckIds(camp.Ranks.Select(r => r.Id), "Grade");
        CheckIds(camp.Tasks.Select(t => t.Id), "Tâche du camp");
        foreach (var n in Content.Npcs.Where(n => n.StartRankId is not null))
            Check(camp.Ranks.Any(r => r.Id == n.StartRankId), $"PNJ {n.Id} : grade « {n.StartRankId} » introuvable");
        foreach (var t in camp.Tasks)
        {
            var w = $"Tâche {t.Id}";
            Check(t.DurationMinutes > 0, $"{w} : durée à 0");
            CheckConditions(t.Conditions, w);
            foreach (var o in t.Outcomes)
            {
                CheckConditions(o.Conditions, w);
                CheckActions(o.Actions, w);
            }
        }
        CheckIds(camp.Resources.Select(r => r.Id), "Ressource du camp");
        CheckIds(camp.Buildings.Select(b => b.Id), "Lieu du camp");
        foreach (var b in camp.Buildings)
        {
            var w = $"Lieu du camp {b.Id}";
            foreach (var cost in b.Costs)
                Check(camp.Resources.Any(r => r.Id == cost.ResourceId), $"{w} : ressource « {cost.ResourceId} » introuvable");
            CheckConditions(b.Conditions, w);
            CheckActions(b.OnBuilt, w);
        }
        foreach (var p in Content.Portraits)
            Check(p.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase), $"Image {p.Id} : le lien doit commencer par https://");

        foreach (var c in Content.Characters)
        {
            var w = $"Personnage {c.Id}";
            foreach (var s in c.Skills) Ref(Skills, s.SkillId, w, "compétence");
            Ref(Items, c.StartingWeaponId, w, "arme");
            Ref(Items, c.StartingArmorId, w, "armure");
            Ref(Items, c.StartingRelicId, w, "relique");
            Check(c.Skills.Any(s => s.Level <= 1), $"{w} : aucune compétence au niveau 1");
            CheckLines(c.BattleLines, w);
        }
        foreach (var m in Content.Monsters)
        {
            var w = $"Monstre {m.Id}";
            foreach (var s in m.SkillIds) Ref(Skills, s, w, "compétence");
            foreach (var d in m.Drops) Ref(Items, d.ItemId, w, "butin");
            Check(m.SkillIds.Count > 0, $"{w} : aucune compétence");
            Check(m.Stats.MaxHp > 0, $"{w} : PV à 0");
            CheckLines(m.BattleLines, w);
        }
        foreach (var l in Content.Locations)
        {
            var w = $"Lieu {l.Id}";
            foreach (var id in l.ConnectedIds) Ref(Locations, id, w, "lieu relié");
            foreach (var id in l.ShopItemIds) Ref(Items, id, w, "article");
            foreach (var g in l.RandomEncounters)
                foreach (var id in g.MonsterIds) Ref(Monsters, id, w, "monstre");
            if (l.FixedBattle is { } fb)
            {
                foreach (var id in fb.MonsterIds) Ref(Monsters, id, w, "monstre");
                Ref(Dialogues, fb.IntroDialogueId, w, "dialogue");
                Ref(Dialogues, fb.VictoryDialogueId, w, "dialogue");
                Ref(Dialogues, fb.DefeatDialogueId, w, "dialogue");
                CheckLines(fb.BattleLines, w);
            }
            Ref(Dialogues, l.FirstVisitDialogueId, w, "dialogue");
            CheckConditions(l.AccessConditions, w);
            CheckConditions(l.VisibleConditions, w);
        }
        foreach (var n in Content.Npcs)
        {
            var w = $"PNJ {n.Id}";
            if (!string.IsNullOrEmpty(n.LocationId)) Ref(Locations, n.LocationId, w, "lieu");
            Check(!string.IsNullOrEmpty(n.LocationId) || n.StartsInCamp, $"{w} : aucun lieu");
            Ref(Dialogues, n.DefaultDialogueId, w, "dialogue");
            CheckConditions(n.VisibleConditions, w);
            foreach (var p in n.Placements)
            {
                Ref(Locations, p.LocationId, w, "lieu");
                CheckConditions(p.Conditions, w);
            }
            foreach (var cd in n.ConditionalDialogues)
            {
                Ref(Dialogues, cd.DialogueId, w, "dialogue");
                CheckConditions(cd.Conditions, w);
            }
        }
        foreach (var d in Content.Dialogues)
        {
            var w = $"Dialogue {d.Id}";
            Check(d.Nodes.Count > 0, $"{w} : aucune réplique");
            var ids = new HashSet<string>();
            foreach (var n in d.Nodes) Check(ids.Add(n.Id), $"{w} : réplique « {n.Id} » en double");
            void Target(string? target)
            {
                if (string.IsNullOrEmpty(target)) return;
                var (dialogueId, label) = SplitTarget(target);
                if (dialogueId is null) { Check(ids.Contains(label), $"{w} : réplique « {label} » introuvable"); return; }
                if (!Dialogues.TryGetValue(dialogueId, out var other)) { Check(false, $"{w} : dialogue « {dialogueId} » introuvable"); return; }
                if (label.Length > 0) Check(other.Nodes.Any(n => n.Id == label), $"{w} : réplique « {target} » introuvable");
            }
            foreach (var n in d.Nodes)
            {
                Target(n.NextId);
                CheckActions(n.Actions, w);
                foreach (var b in n.Branches)
                {
                    Target(b.NextId);
                    CheckConditions(b.Conditions, w);
                }
                foreach (var v in n.Variants) CheckConditions(v.Conditions, w);
                foreach (var ch in n.Choices)
                {
                    Target(ch.NextId);
                    CheckActions(ch.Actions, w);
                    CheckConditions(ch.Conditions, w);
                }
            }
        }
        foreach (var q in Content.Quests)
        {
            var w = $"Quête {q.Id}";
            CheckConditions(q.AutoStart, w);
            if (q.IsStaged)
            {
                var stageIds = new HashSet<string>();
                foreach (var st in q.Stages) Check(stageIds.Add(st.Id), $"{w} : étape « {st.Id} » en double");
                foreach (var st in q.Stages)
                {
                    var ws = $"{w}, étape {st.Id}";
                    CheckObjectives(st.Objectives, ws);
                    CheckActions(st.OnEnter, ws);
                    foreach (var x in st.Exits)
                    {
                        Check(stageIds.Contains(x.NextStageId), $"{ws} : étape suivante « {x.NextStageId} » introuvable");
                        CheckConditions(x.Conditions, ws);
                        CheckActions(x.Actions, ws);
                    }
                    Check(!(st.IsEnding && st.Exits.Count > 0), $"{ws} : une fin ne peut pas avoir de suite");
                }
                CheckActions(q.Rewards, w);
                continue;
            }
            Check(q.Objectives.Count > 0, $"{w} : aucun objectif");
            CheckObjectives(q.Objectives, w);
            CheckActions(q.Rewards, w);
        }

        void CheckObjectives(IEnumerable<QuestObjective> objectives, string w)
        {
            foreach (var o in objectives)
            {
                switch (o.Type)
                {
                    case ObjectiveType.TalkTo: Ref(Npcs, o.TargetId, w, "PNJ"); break;
                    case ObjectiveType.Defeat: Ref(Monsters, o.TargetId, w, "monstre"); break;
                    case ObjectiveType.Reach: Ref(Locations, o.TargetId, w, "lieu"); break;
                    case ObjectiveType.Bring:
                        Ref(Items, o.TargetId, w, "objet");
                        Ref(Npcs, o.NpcId, w, "PNJ");
                        break;
                }
                Check(!string.IsNullOrEmpty(o.TargetId), $"{w} : objectif sans cible");
            }
        }

        CheckIds(Starts.Select(x => x.Id), "Départ");
        foreach (var start in Starts)
        {
            var w = $"Départ « {start.Name} »";
            Check(Locations.ContainsKey(start.LocationId), $"{w} : lieu « {start.LocationId} » introuvable");
            foreach (var s in start.Inventory) Ref(Items, s.ItemId, w, "objet");
            Ref(Dialogues, start.IntroDialogueId, w, "dialogue");
            foreach (var id in start.HeroIds.Concat(start.Companions)) Ref(Characters, id, w, "personnage");
            CheckActions(start.Actions, w);
        }
        Check(Starters.Any(), "Aucun personnage de départ (cocher « Proposé au départ » sur un PJ)");
        return errors;

        void CheckIds(IEnumerable<string> ids, string what)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                Check(!string.IsNullOrWhiteSpace(id), $"{what} sans identifiant");
                Check(seen.Add(id), $"{what} : identifiant « {id} » en double");
            }
        }

        void CheckConditions(IEnumerable<Condition> conditions, string w)
        {
            foreach (var c in conditions)
            {
                switch (c.Type)
                {
                    case ConditionType.QuestActive or ConditionType.QuestCompleted or ConditionType.QuestNotStarted:
                        Ref(Quests, c.Arg, w, "quête"); break;
                    case ConditionType.HasItem: Ref(Items, c.Arg, w, "objet"); break;
                    case ConditionType.InParty or ConditionType.NotInParty or ConditionType.Speaker:
                        Ref(Characters, c.Arg, w, "personnage"); break;
                    case ConditionType.AtLocation or ConditionType.Visited: Ref(Locations, c.Arg, w, "lieu"); break;
                    case ConditionType.MetNpc: Ref(Npcs, c.Arg, w, "PNJ"); break;
                    case ConditionType.Karma when !c.Arg.StartsWith('@'): Ref(Characters, c.Arg, w, "personnage"); break;
                    case ConditionType.Friendship:
                        Check(c.Arg.StartsWith('@') || Npcs.ContainsKey(c.Arg) || Characters.ContainsKey(c.Arg), $"{w} : personnage « {c.Arg} » introuvable");
                        break;
                    case ConditionType.QuestAtStage or ConditionType.QuestStageReached or ConditionType.QuestEnding or ConditionType.QuestFailed:
                        Ref(Quests, c.Arg, w, "quête");
                        if (c.Arg2.Length > 0 && Quests.TryGetValue(c.Arg, out var cq))
                            Check(cq.Stages.Any(st => st.Id == c.Arg2), $"{w} : étape « {c.Arg2} » introuvable dans la quête {c.Arg}");
                        break;
                    case ConditionType.CampResource:
                        Check(Content.Camp.Resources.Any(r => r.Id == c.Arg), $"{w} : ressource « {c.Arg} » introuvable"); break;
                    case ConditionType.CampBuilt:
                        Check(Content.Camp.Buildings.Any(b => b.Id == c.Arg), $"{w} : lieu du camp « {c.Arg} » introuvable"); break;
                    case ConditionType.AnyOf or ConditionType.AllOf:
                        if (c.Children is { } children) CheckConditions(children, w);
                        break;
                }
            }
        }

        void CheckLines(IEnumerable<BattleLine> lines, string w)
        {
            foreach (var line in lines)
            {
                CheckConditions(line.Conditions, w);
                CheckActions(line.Actions, w);
            }
        }

        void CheckActions(IEnumerable<GameAction> actions, string w)
        {
            foreach (var a in actions)
            {
                switch (a.Type)
                {
                    case ActionType.Recruit or ActionType.LeaveParty: Ref(Characters, a.Arg, w, "personnage"); break;
                    case ActionType.RevealLocation or ActionType.HideLocation: Ref(Locations, a.Arg, w, "lieu"); break;
                    case ActionType.MoveNpc:
                        Ref(Npcs, a.Arg, w, "PNJ");
                        Ref(Locations, a.Arg2, w, "lieu");
                        break;
                    case ActionType.AddKarma or ActionType.SetKarma when !a.Arg.StartsWith('@') && a.Arg.Length > 0:
                        Ref(Characters, a.Arg, w, "personnage"); break;
                    case ActionType.GiveItem or ActionType.TakeItem: Ref(Items, a.Arg, w, "objet"); break;
                    case ActionType.StartQuest or ActionType.CompleteQuest or ActionType.FailQuest: Ref(Quests, a.Arg, w, "quête"); break;
                    case ActionType.SetQuestStage:
                        Ref(Quests, a.Arg, w, "quête");
                        if (Quests.TryGetValue(a.Arg, out var aq))
                            Check(aq.Stages.Any(st => st.Id == a.Arg2), $"{w} : étape « {a.Arg2} » introuvable dans la quête {a.Arg}");
                        break;
                    case ActionType.Teleport: Ref(Locations, a.Arg, w, "lieu"); break;
                    case ActionType.AddCampResource:
                        Check(Content.Camp.Resources.Any(r => r.Id == a.Arg), $"{w} : ressource « {a.Arg} » introuvable"); break;
                    case ActionType.BuildCampBuilding:
                        Check(Content.Camp.Buildings.Any(b => b.Id == a.Arg), $"{w} : lieu du camp « {a.Arg} » introuvable"); break;
                    case ActionType.StartBattle:
                        foreach (var id in SplitIds(a.Arg)) Ref(Monsters, id, w, "monstre");
                        break;
                }
            }
        }
    }

    /// <summary>« etiquette » → (null, etiquette) ; « dialogue:etiquette » → (dialogue, etiquette).</summary>
    public static (string? DialogueId, string Label) SplitTarget(string target)
    {
        var colon = target.IndexOf(':');
        return colon < 0 ? (null, target) : (target[..colon], target[(colon + 1)..]);
    }

    public static string[] SplitIds(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
