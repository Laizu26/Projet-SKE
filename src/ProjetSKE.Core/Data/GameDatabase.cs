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

    public StartSettings Start => Content.Start;
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
    }

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

        foreach (var c in Content.Characters)
        {
            var w = $"Personnage {c.Id}";
            foreach (var s in c.Skills) Ref(Skills, s.SkillId, w, "compétence");
            Ref(Items, c.StartingWeaponId, w, "arme");
            Ref(Items, c.StartingArmorId, w, "armure");
            Ref(Items, c.StartingRelicId, w, "relique");
            Check(c.Skills.Any(s => s.Level <= 1), $"{w} : aucune compétence au niveau 1");
        }
        foreach (var m in Content.Monsters)
        {
            var w = $"Monstre {m.Id}";
            foreach (var s in m.SkillIds) Ref(Skills, s, w, "compétence");
            foreach (var d in m.Drops) Ref(Items, d.ItemId, w, "butin");
            Check(m.SkillIds.Count > 0, $"{w} : aucune compétence");
            Check(m.Stats.MaxHp > 0, $"{w} : PV à 0");
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
            }
            Ref(Dialogues, l.FirstVisitDialogueId, w, "dialogue");
            CheckConditions(l.AccessConditions, w);
        }
        foreach (var n in Content.Npcs)
        {
            var w = $"PNJ {n.Id}";
            Ref(Locations, n.LocationId, w, "lieu");
            Check(!string.IsNullOrEmpty(n.LocationId), $"{w} : aucun lieu");
            Ref(Dialogues, n.DefaultDialogueId, w, "dialogue");
            CheckConditions(n.VisibleConditions, w);
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
            foreach (var n in d.Nodes)
            {
                if (n.NextId is { Length: > 0 } next) Check(ids.Contains(next), $"{w} : réplique « {next} » introuvable");
                CheckActions(n.Actions, w);
                foreach (var ch in n.Choices)
                {
                    if (ch.NextId is { Length: > 0 } cn) Check(ids.Contains(cn), $"{w} : réplique « {cn} » introuvable");
                    CheckActions(ch.Actions, w);
                    CheckConditions(ch.Conditions, w);
                }
            }
        }
        foreach (var q in Content.Quests)
        {
            var w = $"Quête {q.Id}";
            Check(q.Objectives.Count > 0, $"{w} : aucun objectif");
            foreach (var o in q.Objectives)
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
            CheckActions(q.Rewards, w);
        }

        Check(Locations.ContainsKey(Start.LocationId), $"Départ : lieu « {Start.LocationId} » introuvable");
        foreach (var s in Start.Inventory) Ref(Items, s.ItemId, "Départ", "objet");
        Ref(Dialogues, Start.IntroDialogueId, "Départ", "dialogue");
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
                    case ConditionType.InParty or ConditionType.NotInParty: Ref(Characters, c.Arg, w, "personnage"); break;
                }
            }
        }

        void CheckActions(IEnumerable<GameAction> actions, string w)
        {
            foreach (var a in actions)
            {
                switch (a.Type)
                {
                    case ActionType.Recruit: Ref(Characters, a.Arg, w, "personnage"); break;
                    case ActionType.GiveItem or ActionType.TakeItem: Ref(Items, a.Arg, w, "objet"); break;
                    case ActionType.StartQuest or ActionType.CompleteQuest: Ref(Quests, a.Arg, w, "quête"); break;
                    case ActionType.Teleport: Ref(Locations, a.Arg, w, "lieu"); break;
                    case ActionType.StartBattle:
                        foreach (var id in SplitIds(a.Arg)) Ref(Monsters, id, w, "monstre");
                        break;
                }
            }
        }
    }

    public static string[] SplitIds(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
