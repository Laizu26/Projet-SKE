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
    public IReadOnlyDictionary<string, CharacterGaugeDef> Gauges { get; }
    public IReadOnlyDictionary<string, PassiveDef> Passives { get; }
    public IReadOnlyDictionary<string, PowerDef> Powers { get; }

    /// <summary>Compétences d'un pouvoir, par niveau d'apprentissage.</summary>
    public IEnumerable<SkillDef> SkillsOfPower(string powerId) =>
        Content.Skills.Where(s => s.PowerId == powerId).OrderBy(s => s.PowerLevel);
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

    /// <summary>
    /// Départ d'un héros : celui choisi sur sa fiche, sinon (anciens contenus) un autre départ qui le cite,
    /// sinon le départ principal. Le joueur ne choisit pas : le départ dépend du personnage.
    /// </summary>
    public StartSettings StartFor(string heroId)
    {
        if (Characters.TryGetValue(heroId, out var hero) && !string.IsNullOrEmpty(hero.StartId)
            && Starts.FirstOrDefault(s => s.Id == hero.StartId) is { } chosen)
            return chosen;
        return Content.ExtraStarts.FirstOrDefault(s => s.HeroIds.Contains(heroId)) ?? Content.Start;
    }

    /// <summary>Héros proposés qui commencent par ce départ.</summary>
    public IEnumerable<CharacterDef> HeroesOf(StartSettings start) => Starters.Where(c => StartFor(c.Id) == start);
    public BalanceSettings Balance => Content.Balance;

    // ------------------------------------------------------------------ Lieux dans des lieux

    /// <summary>Lieu qui contient celui-ci (null = lieu de la carte du royaume).</summary>
    public LocationDef? ParentOf(LocationDef loc) =>
        loc.ParentId is { Length: > 0 } p && p != loc.Id && Locations.TryGetValue(p, out var parent) ? parent : null;

    /// <summary>Chemin depuis le lieu de la carte du royaume jusqu'à ce lieu (ex : Havrefort › Taverne › Cave).</summary>
    public IReadOnlyList<LocationDef> PathOf(LocationDef loc)
    {
        var path = new List<LocationDef> { loc };
        for (var cur = ParentOf(loc); cur is not null && path.Count < 32 && !path.Contains(cur); cur = ParentOf(cur)) path.Insert(0, cur);
        return path;
    }

    /// <summary>Lieu de la carte du royaume qui contient ce lieu (lui-même s'il est sur la carte).</summary>
    public LocationDef RootOf(LocationDef loc) => PathOf(loc)[0];

    /// <summary>Sous-lieux directs (dans l'ordre de l'éditeur).</summary>
    public IEnumerable<LocationDef> ChildrenOf(string id) => Content.Locations.Where(l => l.ParentId == id && l.Id != id);

    /// <summary>Vrai si le lieu est <paramref name="ancestorId"/> ou se trouve dedans (à n'importe quelle profondeur).</summary>
    public bool IsWithin(string locationId, string ancestorId) =>
        Locations.TryGetValue(locationId, out var loc) && PathOf(loc).Any(l => l.Id == ancestorId);

    public GameDatabase(GameContent content)
    {
        Content = content;
        // En cas d'identifiant en double, le dernier gagne (la validation le signale).
        Skills = Index(content.Skills, s => s.Id);
        Items = Index(content.Items, i => i.Id);
        Characters = Index(content.Characters, c => c.Id);
        // Les PNJ qui se battent sont aussi des adversaires (même identifiant que le PNJ).
        Monsters = Index(content.Monsters.Concat(content.Npcs.Where(n => n.Combat is not null).Select(n => n.Combat!.AsMonster(n))), m => m.Id);
        Locations = Index(content.Locations, l => l.Id);
        Npcs = Index(content.Npcs, n => n.Id);
        Dialogues = Index(content.Dialogues, d => d.Id);
        Quests = Index(content.Quests, q => q.Id);
        Variables = Index(content.Variables, v => v.Id);
        Portraits = Index(content.Portraits, p => p.Id);
        Gauges = Index(content.Gauges, g => g.Id);
        Passives = Index(content.Passives, p => p.Id);
        Powers = Index(content.Powers, p => p.Id);
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
        CheckIds(Content.Gauges.Select(x => x.Id), "Jauge");
        CheckIds(Content.Passives.Select(x => x.Id), "Passif");
        CheckIds(Content.Powers.Select(x => x.Id), "Pouvoir");
        foreach (var s in Content.Skills) Ref(Powers, s.PowerId, $"Compétence {s.Id}", "pouvoir");
        foreach (var c in Content.Characters)
            foreach (var p in c.PowerIds) Ref(Powers, p, $"Personnage {c.Id}", "pouvoir");
        foreach (var p in Content.Passives)
        {
            CheckConditions(p.Conditions, $"Passif {p.Id}");
        }
        foreach (var c in Content.Characters)
            foreach (var p in c.Passives) Ref(Passives, p.PassiveId, $"Personnage {c.Id}", "passif");
        foreach (var c in Content.Characters)
            foreach (var id in c.BaseGauges.Keys) Ref(Gauges, id, $"Personnage {c.Id}", "jauge");
        foreach (var n in Content.Npcs.Where(n => n.Combat is not null && Content.Monsters.Any(m => m.Id == n.Id)))
            Check(false, $"PNJ {n.Id} : un monstre a le même identifiant (le combat ne saurait pas lequel prendre)");
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
            foreach (var g in c.StartingGearIds) Ref(Items, g, w, "équipement");
            foreach (var id in c.StartCompanions) Ref(Characters, id, w, "compagnon de départ");
            CheckActions(c.StartActions, w);
            Check(c.Skills.Any(s => s.Level <= 1), $"{w} : aucune compétence au niveau 1");
            if (!string.IsNullOrEmpty(c.StartId)) Check(Starts.Any(s => s.Id == c.StartId), $"{w} : départ « {c.StartId} » introuvable");
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
        if (Content.Tutorial is { Enabled: true } tuto)
        {
            const string w = "Prologue";
            Ref(Characters, tuto.HeroId, w, "personnage joué");
            Check(Locations.ContainsKey(tuto.Start.LocationId), $"{w} : aucun lieu de départ");
            foreach (var id in tuto.Start.Companions) Ref(Characters, id, w, "compagnon");
            foreach (var s in tuto.Start.Inventory) Ref(Items, s.ItemId, w, "objet");
            foreach (var d in tuto.Start.IntroDialogues) Check(Dialogues.ContainsKey(d), $"{w} : dialogue « {d} » introuvable");
            CheckActions(tuto.Start.Actions, w);
        }
        foreach (var n in Content.Npcs)
        {
            if (n.Combat is not { } c) continue;
            var w = $"PNJ {n.Id} (combat)";
            foreach (var s in c.SkillIds) Ref(Skills, s, w, "compétence");
            foreach (var d in c.Drops) Ref(Items, d.ItemId, w, "butin");
            foreach (var id in c.AllyIds) Ref(Monsters, id, w, "allié");
            Check(c.SkillIds.Count > 0, $"{w} : aucune compétence");
            Check(c.Stats.MaxHp > 0, $"{w} : PV à 0");
            Ref(Dialogues, c.AttackDialogueId, w, "dialogue");
            Ref(Dialogues, c.VictoryDialogueId, w, "dialogue");
            Ref(Dialogues, c.DefeatDialogueId, w, "dialogue");
            CheckConditions(c.AttackConditions, w);
            CheckLines(c.BattleLines, w);
        }
        foreach (var l in Content.Locations)
        {
            var w = $"Lieu {l.Id}";
            if (!string.IsNullOrEmpty(l.ParentId))
            {
                Ref(Locations, l.ParentId, w, "lieu parent");
                var loop = l.ParentId == l.Id;
                for (var (cur, steps) = (ParentOf(l), 0); cur is not null && !loop && steps < 64; cur = ParentOf(cur), steps++)
                    loop = cur == l;
                Check(!loop, $"{w} : il se trouve dans lui-même (boucle de lieux parents)");
            }
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
                if (n.ElseId is not "fin") Target(n.ElseId);
                CheckConditions(n.Conditions, w);
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
            if (q.HasParts)
            {
                var partIds = new HashSet<string>();
                foreach (var part in q.Parts)
                {
                    var wp = $"{w}, partie {part.Id}";
                    Check(!string.IsNullOrWhiteSpace(part.Id), $"{w} : partie sans identifiant");
                    Check(partIds.Add(part.Id), $"{w} : partie « {part.Id} » en double");
                    CheckObjectives(part.Objectives, wp);
                    CheckConditions(part.StartConditions, wp);
                    CheckConditions(part.FailConditions, wp);
                    CheckActions(part.Rewards, wp);
                }
                CheckActions(q.Rewards, w);
                continue;
            }
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
            foreach (var id in start.IntroDialogues) Ref(Dialogues, id, w, "dialogue");
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
                    case ConditionType.InParty or ConditionType.NotInParty or ConditionType.Speaker or ConditionType.IsHero:
                        Ref(Characters, c.Arg, w, "personnage"); break;
                    case ConditionType.AtLocation or ConditionType.Visited: Ref(Locations, c.Arg, w, "lieu"); break;
                    case ConditionType.MetNpc: Ref(Npcs, c.Arg, w, "PNJ"); break;
                    case ConditionType.Karma when !c.Arg.StartsWith('@'): Ref(Characters, c.Arg, w, "personnage"); break;
                    case ConditionType.HasPower:
                        Check(Powers.ContainsKey(c.Arg2), $"{w} : pouvoir « {c.Arg2} » introuvable");
                        if (c.Arg.Length > 0 && !c.Arg.StartsWith('@')) Ref(Characters, c.Arg, w, "personnage");
                        break;
                    case ConditionType.HasPassive:
                        Check(Passives.ContainsKey(c.Arg2), $"{w} : passif « {c.Arg2} » introuvable");
                        if (c.Arg.Length > 0 && !c.Arg.StartsWith('@')) Ref(Characters, c.Arg, w, "personnage");
                        break;
                    case ConditionType.Gauge:
                        Check(Gauges.ContainsKey(c.Arg2), $"{w} : jauge « {c.Arg2} » introuvable");
                        if (c.Arg.Length > 0 && !c.Arg.StartsWith('@')) Ref(Characters, c.Arg, w, "personnage");
                        break;
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
                    case ConditionType.QuestPartNotStarted or ConditionType.QuestPartActive or ConditionType.QuestPartCompleted or ConditionType.QuestPartFailed:
                        Ref(Quests, c.Arg, w, "quête");
                        if (Quests.TryGetValue(c.Arg, out var pq))
                            Check(pq.Parts.Any(p => p.Id == c.Arg2), $"{w} : partie « {c.Arg2} » introuvable dans la quête {c.Arg}");
                        break;
                    case ConditionType.ChoiceMade:
                    {
                        var parts = c.Arg2.Split(':');
                        var node = Dialogues.TryGetValue(c.Arg, out var cd) ? cd.Nodes.FirstOrDefault(n => n.Id == parts[0]) : null;
                        Check(node is not null && parts.Length == 2 && node.Choices.Any(ch => node.ChoiceKey(ch) == parts[1]),
                            $"{w} : choix « {c.Arg}:{c.Arg2} » introuvable");
                        break;
                    }
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
                    case ActionType.GivePower or ActionType.RemovePower:
                        Check(Powers.ContainsKey(a.Arg), $"{w} : pouvoir « {a.Arg} » introuvable");
                        if (a.Arg2.Length > 0 && !a.Arg2.StartsWith('@')) Ref(Characters, a.Arg2, w, "personnage");
                        break;
                    case ActionType.GivePassive or ActionType.RemovePassive:
                        Check(Passives.ContainsKey(a.Arg), $"{w} : passif « {a.Arg} » introuvable");
                        if (a.Arg2.Length > 0 && !a.Arg2.StartsWith('@')) Ref(Characters, a.Arg2, w, "personnage");
                        break;
                    case ActionType.AddGauge or ActionType.SetGauge:
                        Check(Gauges.ContainsKey(a.Arg), $"{w} : jauge « {a.Arg} » introuvable");
                        if (a.Arg2.Length > 0 && !a.Arg2.StartsWith('@')) Ref(Characters, a.Arg2, w, "personnage");
                        break;
                    case ActionType.GiveItem or ActionType.TakeItem: Ref(Items, a.Arg, w, "objet"); break;
                    case ActionType.StartQuest or ActionType.CompleteQuest or ActionType.FailQuest: Ref(Quests, a.Arg, w, "quête"); break;
                    case ActionType.SetQuestStage:
                        Ref(Quests, a.Arg, w, "quête");
                        if (Quests.TryGetValue(a.Arg, out var aq))
                            Check(aq.Stages.Any(st => st.Id == a.Arg2), $"{w} : étape « {a.Arg2} » introuvable dans la quête {a.Arg}");
                        break;
                    case ActionType.Teleport: Ref(Locations, a.Arg, w, "lieu"); break;
                    case ActionType.StartDialogue: Check(Dialogues.ContainsKey(a.Arg), $"{w} : dialogue « {a.Arg} » introuvable"); break;
                    case ActionType.UnlockFeature or ActionType.LockFeature:
                        Check(UiFeatures.TryParse(a.Arg, out _), $"{w} : partie de l'interface « {a.Arg} » inconnue");
                        break;
                    case ActionType.StartQuestPart or ActionType.CompleteQuestPart or ActionType.FailQuestPart:
                        Ref(Quests, a.Arg, w, "quête");
                        if (Quests.TryGetValue(a.Arg, out var apq))
                            Check(apq.Parts.Any(p => p.Id == a.Arg2), $"{w} : partie « {a.Arg2} » introuvable dans la quête {a.Arg}");
                        break;
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
