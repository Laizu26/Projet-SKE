using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;

namespace ProjetSKE.Core.Systems;

public sealed record TravelResult(
    bool Success,
    string? Error = null,
    // Dialogue à jouer à l'arrivée (première visite ou intro d'un combat fixe).
    string? DialogueId = null,
    // Monstres à combattre à l'arrivée (combat fixe ou rencontre aléatoire).
    IReadOnlyList<string>? BattleMonsterIds = null,
    string? FixedBattleId = null);

public sealed record BattleRewards(int Xp, int Gold, IReadOnlyList<string> ItemIds, IReadOnlyList<string> LevelUps);

public sealed record DefeatResult(bool IsGameOver, int GoldLost, string? ReturnLocationId);

/// <summary>Partie en cours : toutes les règles du jeu hors déroulé du combat et du dialogue.</summary>
public sealed partial class GameSession
{
    public GameDatabase Db { get; }
    public GameState State { get; }
    public Random Rng { get; }

    /// <summary>Messages à afficher au joueur (recrutement, objectif accompli, récompense...).</summary>
    public List<string> Notifications { get; } = [];

    public GameSession(GameDatabase db, GameState state, Random? rng = null)
    {
        Db = db;
        State = state;
        Rng = rng ?? new Random();
        Sanitize();
    }

    public static GameSession NewGame(GameDatabase db, string heroId, Random? rng = null) => NewGame(db, heroId, null, rng);

    /// <summary>Nouvelle partie avec un départ donné (null = départ principal).</summary>
    public static GameSession NewGame(GameDatabase db, string heroId, string? startId, Random? rng = null)
    {
        var start = db.StartById(startId);
        var time = db.Content.Time;
        var minutes = GameClock.StartMinutes(new TimeSettings
        {
            HoursPerDay = time.HoursPerDay,
            StartDay = start.Day ?? time.StartDay,
            StartHour = start.Hour ?? time.StartHour,
        });
        var state = new GameState
        {
            HeroId = heroId,
            StartId = start.Id,
            Gold = start.Gold,
            CurrentLocationId = start.LocationId,
            LastCityId = start.LocationId,
            Minutes = minutes,
        };
        foreach (var v in db.Content.Variables) state.Variables[v.Id] = v.Initial;
        var session = new GameSession(db, state, rng);
        foreach (var stack in start.Inventory) session.AddItem(stack.ItemId, stack.Count);
        session.Recruit(heroId);
        foreach (var companion in start.Companions) session.Recruit(companion);
        foreach (var npc in db.Content.Npcs.Where(n => n.StartsInCamp)) session.JoinCamp(npc.Id, npc.StartRankId);
        session.DiscoverLocation(state.CurrentLocationId);
        foreach (var action in start.Actions.Where(a => a.Type != ActionType.StartBattle)) session.Execute(action);
        session.UpdateQuests();
        session.Notifications.Clear();
        return session;
    }

    /// <summary>
    /// Retire d'une sauvegarde tout ce qui n'existe plus dans le contenu (après modification dans l'éditeur),
    /// pour qu'une ancienne partie reste jouable.
    /// </summary>
    private void Sanitize()
    {
        if (State.Version < 2)
        {
            // Ancienne sauvegarde : début du calendrier, karma de départ, variables initiales.
            State.Minutes = GameClock.StartMinutes(Db.Content.Time);
            foreach (var c in State.Party)
                if (Db.Characters.TryGetValue(c.DefId, out var d)) c.Karma = d.BaseKarma ?? Db.Content.Karma.Default;
            foreach (var v in Db.Content.Variables) State.Variables.TryAdd(v.Id, v.Initial);
            State.Version = GameState.CurrentVersion;
        }
        if (State.SpeakerId is { } sp && !State.Party.Any(c => c.DefId == sp)) State.SpeakerId = null;
        State.Camp.RemoveAll(m => !Db.Npcs.ContainsKey(m.Id) && !Db.Characters.ContainsKey(m.Id));
        foreach (var m in State.Camp)
        {
            if (!Db.Content.Camp.Ranks.Any(r => r.Id == m.RankId)) m.RankId = Db.Content.Camp.Ranks.OrderBy(r => r.Level).FirstOrDefault()?.Id ?? "";
            if (m.TaskId is { } t && !Db.Content.Camp.Tasks.Any(x => x.Id == t)) m.TaskId = null;
        }
        State.Party.RemoveAll(c => !Db.Characters.ContainsKey(c.DefId));
        foreach (var c in State.Party)
        {
            foreach (var slot in Enum.GetValues<EquipSlot>())
                if (c.GetEquipped(slot) is { } id && !Db.Items.ContainsKey(id)) c.SetEquipped(slot, null);
            ClampVitals(c);
        }
        if (State.Party.Count > 0 && !State.Party.Any(c => c.IsActive)) State.Party[0].IsActive = true;
        foreach (var id in State.Inventory.Keys.Where(id => !Db.Items.ContainsKey(id)).ToList()) State.Inventory.Remove(id);
        foreach (var id in State.Quests.Keys.Where(id => !Db.Quests.ContainsKey(id)).ToList()) State.Quests.Remove(id);
        State.SeenCharacters.RemoveWhere(id => !Db.Characters.ContainsKey(id));
        State.SeenMonsters.RemoveWhere(id => !Db.Monsters.ContainsKey(id));
        State.SeenLocations.RemoveWhere(id => !Db.Locations.ContainsKey(id));
        State.SeenWeapons.RemoveWhere(id => !Db.Items.ContainsKey(id));
        State.SeenRelics.RemoveWhere(id => !Db.Items.ContainsKey(id));
        State.SeenNpcs.RemoveWhere(id => !Db.Npcs.ContainsKey(id));
        var fallback = Db.Locations.ContainsKey(Db.Start.LocationId) ? Db.Start.LocationId : Db.Content.Locations.FirstOrDefault()?.Id ?? "";
        if (!Db.Locations.ContainsKey(State.CurrentLocationId)) State.CurrentLocationId = fallback;
        if (!Db.Locations.ContainsKey(State.LastCityId)) State.LastCityId = fallback;
    }

    public GameConfig Config => State.Config;
    public BalanceSettings Balance => Db.Balance;
    public LocationDef CurrentLocation => Db.Locations[State.CurrentLocationId];
    public bool InCity => CurrentLocation.IsCity;
    public bool HasFlag(string flag) => State.Flags.Contains(flag);
    public void SetFlag(string flag) => State.Flags.Add(flag);

    // ------------------------------------------------------------------ Conditions et actions

    public bool Check(Condition c) => c.Negate ? !CheckRaw(c) : CheckRaw(c);

    private bool CheckRaw(Condition c) => c.Type switch
    {
        ConditionType.FlagSet => HasFlag(c.Arg),
        ConditionType.FlagNotSet => !HasFlag(c.Arg),
        ConditionType.QuestNotStarted => GetQuestStatus(c.Arg) == QuestStatus.NotStarted,
        ConditionType.QuestActive => GetQuestStatus(c.Arg) == QuestStatus.Active,
        ConditionType.QuestCompleted => GetQuestStatus(c.Arg) == QuestStatus.Completed,
        ConditionType.HasItem => OwnsCount(c.Arg) >= Math.Max(1, c.Amount),
        ConditionType.InParty => IsInParty(c.Arg),
        ConditionType.NotInParty => !IsInParty(c.Arg),
        ConditionType.GoldAtLeast => State.Gold >= c.Amount,
        ConditionType.LevelAtLeast => MaxLevel >= c.Amount,
        ConditionType.Variable => Compare(GetVariable(c.Arg), c.Op, c.Amount),
        ConditionType.Karma => Compare(GetKarma(c.Arg), c.Op, c.Amount),
        ConditionType.Friendship => Compare(GetFriendship(c.Arg, c.Arg2), c.Op, c.Amount),
        ConditionType.Gold => Compare(State.Gold, c.Op, c.Amount),
        ConditionType.Level => Compare(MaxLevel, c.Op, c.Amount),
        ConditionType.PartySize => Compare(State.Party.Count, c.Op, c.Amount),
        ConditionType.Speaker => SpeakerId == c.Arg,
        ConditionType.HourBetween => HourBetween(Clock.Hour, c.Amount, c.Amount2),
        ConditionType.Day => Compare(Clock.Day, c.Op, c.Amount),
        ConditionType.Period => SameName(Clock.Period, c.Arg),
        ConditionType.WeekDay => SameName(Clock.WeekDay, c.Arg),
        ConditionType.Month => SameName(Clock.Month, c.Arg),
        ConditionType.AtLocation => State.CurrentLocationId == c.Arg,
        ConditionType.Visited => State.SeenLocations.Contains(c.Arg),
        ConditionType.MetNpc => State.SeenNpcs.Contains(c.Arg),
        ConditionType.Chance => Rng.Next(100) < c.Amount,
        ConditionType.CampMember => CampMember(ResolveWho(c.Arg)) is not null,
        ConditionType.CampRank => CampMember(ResolveWho(c.Arg)) is not null && Compare(RankLevel(ResolveWho(c.Arg)), c.Op, c.Amount),
        ConditionType.CampTask => CampMember(ResolveWho(c.Arg))?.TaskId == c.Arg2,
        ConditionType.QuestAtStage => QuestProgressOf(c.Arg) is { Status: QuestStatus.Active } p && p.StageId == c.Arg2,
        ConditionType.QuestStageReached => QuestProgressOf(c.Arg)?.Path.Contains(c.Arg2) == true,
        ConditionType.QuestEnding => QuestProgressOf(c.Arg)?.EndingId is { } ending && (c.Arg2.Length == 0 || ending == c.Arg2),
        ConditionType.QuestFailed => GetQuestStatus(c.Arg) == QuestStatus.Failed,
        ConditionType.AnyOf => c.Children is not { Count: > 0 } children || children.Any(Check),
        ConditionType.AllOf => c.Children is not { Count: > 0 } all || all.All(Check),
        _ => true,
    };

    public bool CheckAll(IEnumerable<Condition> conditions) => conditions.All(Check);

    public static bool Compare(int value, CompareOp op, int amount) => op switch
    {
        CompareOp.AtLeast => value >= amount,
        CompareOp.AtMost => value <= amount,
        CompareOp.Equal => value == amount,
        CompareOp.NotEqual => value != amount,
        CompareOp.Greater => value > amount,
        _ => value < amount,
    };

    /// <summary>Heure comprise entre « de » (inclus) et « à » (exclu) ; 22 → 6 passe par minuit.</summary>
    public static bool HourBetween(int hour, int from, int to) =>
        from == to || (from < to ? hour >= from && hour < to : hour >= from || hour < to);

    private static bool SameName(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private int MaxLevel => State.Party.Count > 0 ? State.Party.Max(p => p.Level) : 0;

    /// <summary>Applique un effet. Renvoie les monstres à combattre si l'action lance un combat.</summary>
    public IReadOnlyList<string>? Execute(GameAction a)
    {
        switch (a.Type)
        {
            case ActionType.SetFlag:
                SetFlag(a.Arg);
                break;
            case ActionType.ClearFlag:
                State.Flags.Remove(a.Arg);
                break;
            case ActionType.Recruit:
                if (Recruit(a.Arg))
                {
                    var c = State.Party[^1];
                    Notifications.Add($"{Db.Characters[a.Arg].Name} rejoint l'équipe{(c.IsActive ? "" : " (réserve)")} !");
                }
                break;
            case ActionType.LeaveParty:
                if (Leave(a.Arg)) Notifications.Add($"{Db.Characters[a.Arg].Name} quitte l'équipe.");
                break;
            case ActionType.GiveItem:
                if (Db.Items.TryGetValue(a.Arg, out var given) && AddItem(a.Arg, a.Amount))
                    Notifications.Add($"Obtenu : {given.Name} x{a.Amount}");
                break;
            case ActionType.TakeItem:
                if (Db.Items.TryGetValue(a.Arg, out var taken) && RemoveItem(a.Arg, Math.Min(a.Amount, CountItem(a.Arg))))
                    Notifications.Add($"Donné : {taken.Name}");
                break;
            case ActionType.GiveGold:
                State.Gold += a.Amount;
                Notifications.Add($"Obtenu : {a.Amount} {Db.T("money")}");
                break;
            case ActionType.TakeGold:
                State.Gold = Math.Max(0, State.Gold - a.Amount);
                Notifications.Add($"Payé : {a.Amount} {Db.T("money")}");
                break;
            case ActionType.GiveXp:
                foreach (var c in ActiveParty.ToList())
                    if (GiveXp(c, a.Amount) > 0) Notifications.Add($"{DefOf(c).Name} passe niveau {c.Level} !");
                Notifications.Add($"+{a.Amount} {Db.T("xp")}");
                break;
            case ActionType.HealParty:
                HealAll();
                Notifications.Add("L'équipe est soignée.");
                break;
            case ActionType.Teleport:
                if (Db.Locations.TryGetValue(a.Arg, out var loc))
                {
                    MoveTo(loc);
                    Notifications.Add($"Vous voici à {loc.Name}.");
                }
                break;
            case ActionType.StartQuest:
                StartQuest(a.Arg);
                break;
            case ActionType.CompleteQuest:
                CompleteQuest(a.Arg);
                break;
            case ActionType.StartBattle:
                var monsters = GameDatabase.SplitIds(a.Arg).Where(Db.Monsters.ContainsKey).ToList();
                return monsters.Count > 0 ? monsters : null;
            case ActionType.SetVariable:
                SetVariable(a.Arg, a.Amount);
                break;
            case ActionType.AddVariable:
                SetVariable(a.Arg, GetVariable(a.Arg) + a.Amount);
                break;
            case ActionType.AddKarma or ActionType.SetKarma:
                foreach (var c in KarmaTargets(a.Arg))
                {
                    var before = c.Karma;
                    c.Karma = ClampScale(Db.Content.Karma, a.Type == ActionType.AddKarma ? c.Karma + a.Amount : a.Amount);
                    if (Db.Content.Karma.Visible && c.Karma != before)
                        Notifications.Add($"{DefOf(c).Name} : {Db.Content.Karma.Name} {Signed(c.Karma - before)}");
                }
                break;
            case ActionType.AddFriendship or ActionType.SetFriendship:
            {
                var before = GetFriendship(a.Arg, a.Arg2);
                var after = a.Type == ActionType.AddFriendship ? before + a.Amount : a.Amount;
                SetFriendship(a.Arg, a.Arg2, after);
                var now = GetFriendship(a.Arg, a.Arg2);
                if (Db.Content.Friendship.Visible && now != before)
                    Notifications.Add($"{CharacterName(a.Arg)} : {Db.Content.Friendship.Name} {Signed(now - before)}");
                break;
            }
            case ActionType.AdvanceTime:
                AdvanceTime(a.Amount);
                break;
            case ActionType.WaitUntilHour:
                AdvanceTime(Clock.MinutesUntilHour(a.Amount));
                break;
            case ActionType.ShowMessage:
                if (a.Arg.Length > 0) Notifications.Add(FormatText(a.Arg));
                break;
            case ActionType.MoveNpc:
                if (string.IsNullOrEmpty(a.Arg2)) State.NpcLocations.Remove(a.Arg);
                else State.NpcLocations[a.Arg] = a.Arg2;
                break;
            case ActionType.RevealLocation:
                State.HiddenLocations.Remove(a.Arg);
                if (State.RevealedLocations.Add(a.Arg) && Db.Locations.TryGetValue(a.Arg, out var revealed))
                    Notifications.Add($"Nouveau lieu sur la carte : {revealed.Name}");
                break;
            case ActionType.HideLocation:
                State.RevealedLocations.Remove(a.Arg);
                State.HiddenLocations.Add(a.Arg);
                break;
            case ActionType.JoinCamp:
                if (JoinCamp(ResolveWho(a.Arg), a.Arg2.Length > 0 ? a.Arg2 : null))
                    Notifications.Add($"{CharacterName(a.Arg)} rejoint le campement.");
                break;
            case ActionType.LeaveCamp:
                if (LeaveCamp(ResolveWho(a.Arg))) Notifications.Add($"{CharacterName(a.Arg)} quitte le campement.");
                break;
            case ActionType.SetCampRank:
                if (SetCampRank(ResolveWho(a.Arg), a.Arg2) && CampRules.Ranks.FirstOrDefault(r => r.Id == a.Arg2) is { } rank)
                    Notifications.Add($"{CharacterName(a.Arg)} devient {rank.Name}.");
                break;
            case ActionType.SetCampTask:
                SetCampTask(ResolveWho(a.Arg), a.Arg2);
                break;
            case ActionType.SetQuestStage:
                GoToStage(a.Arg, a.Arg2);
                break;
            case ActionType.FailQuest:
                FailQuest(a.Arg);
                break;
        }
        return null;
    }

    private static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();

    // ------------------------------------------------------------------ Temps

    public GameClock Clock => new(State.Minutes, Db.Content.Time);

    /// <summary>Fait passer le temps (si l'échelle de temps est activée).</summary>
    public void AdvanceTime(long minutes)
    {
        if (!Db.Content.Time.Enabled || minutes <= 0) return;
        State.Minutes += minutes;
        UpdateCamp();
    }

    // ------------------------------------------------------------------ Variables

    public int GetVariable(string id) =>
        State.Variables.TryGetValue(id, out var v) ? v : Db.Variables.TryGetValue(id, out var def) ? def.Initial : 0;

    public void SetVariable(string id, int value)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (Db.Variables.TryGetValue(id, out var def))
        {
            if (def.Min is { } min) value = Math.Max(min, value);
            if (def.Max is { } max) value = Math.Min(max, value);
        }
        State.Variables[id] = value;
    }

    // ------------------------------------------------------------------ Qui parle, karma, amitié

    /// <summary>PJ qui parle aux PNJ : celui choisi, sinon le héros.</summary>
    public string SpeakerId => State.SpeakerId is { } id && IsInParty(id) ? id : State.HeroId;

    public CharacterState? Speaker => State.Party.FirstOrDefault(c => c.DefId == SpeakerId) ?? State.Party.FirstOrDefault();

    public void SetSpeaker(string? characterId) => State.SpeakerId = characterId is not null && IsInParty(characterId) ? characterId : null;

    /// <summary>« @parle » = PJ qui parle, « @heros » = héros, sinon identifiant tel quel.</summary>
    public string ResolveWho(string who) => who switch
    {
        "" or "@parle" => SpeakerId,
        "@heros" => State.HeroId,
        "@membre" => _campContext ?? SpeakerId,
        _ => who,
    };

    private static int ClampScale(ScaleSettings s, int value) => Math.Clamp(value, Math.Min(s.Min, s.Max), Math.Max(s.Min, s.Max));

    /// <summary>Karma d'un PJ (« @parle », « @heros », id), ou moyenne de l'équipe (« @equipe »).</summary>
    public int GetKarma(string who)
    {
        if (who == "@equipe")
            return State.Party.Count > 0 ? (int)Math.Round(State.Party.Average(c => c.Karma)) : Db.Content.Karma.Default;
        var id = ResolveWho(who);
        return State.Party.FirstOrDefault(c => c.DefId == id)?.Karma ?? Db.Content.Karma.Default;
    }

    private IEnumerable<CharacterState> KarmaTargets(string who)
    {
        if (who == "@equipe") return State.Party.ToList();
        var id = ResolveWho(who);
        return State.Party.Where(c => c.DefId == id).ToList();
    }

    /// <summary>Envers qui : « @equipe » (par défaut, l'équipe entière), « @parle », « @heros » ou un PJ.</summary>
    private string ResolveToward(string toward) => toward is "" or "@equipe" ? "@equipe" : ResolveWho(toward);

    private static string RelationKey(string who, string toward) => $"{who}>{toward}";

    /// <summary>Amitié d'un personnage (PNJ ou PJ) envers l'équipe ou un PJ précis.</summary>
    public int GetFriendship(string who, string toward = "")
    {
        who = ResolveWho(who);
        var key = RelationKey(who, ResolveToward(toward));
        if (State.Relations.TryGetValue(key, out var value)) return value;
        return Db.Npcs.TryGetValue(who, out var npc) && npc.BaseFriendship is { } nb ? nb
            : Db.Characters.TryGetValue(who, out var pj) && pj.BaseFriendship is { } pb ? pb
            : Db.Content.Friendship.Default;
    }

    public void SetFriendship(string who, string toward, int value)
    {
        who = ResolveWho(who);
        if (string.IsNullOrEmpty(who)) return;
        State.Relations[RelationKey(who, ResolveToward(toward))] = ClampScale(Db.Content.Friendship, value);
    }

    public string CharacterName(string id)
    {
        id = ResolveWho(id);
        return Db.Npcs.TryGetValue(id, out var n) ? n.Name : Db.Characters.TryGetValue(id, out var c) ? c.Name : id;
    }

    // ------------------------------------------------------------------ Textes

    /// <summary>
    /// Remplace les balises d'un texte : %pj% (qui parle), %heros%, %pays%, %monnaie%, %heure%, %date%, %jour%,
    /// %periode%, %lieu%, %or%, %karma%, %karma:id%, %var:id%, %amitie:id%, %nom:id%, %classe%, %titre% (:id possible).
    /// </summary>
    public string FormatText(string text)
    {
        if (!text.Contains('%')) return text;
        return TagRegex().Replace(text, m =>
        {
            var tag = m.Groups[1].Value;
            var arg = m.Groups[2].Success ? m.Groups[2].Value : "";
            return tag.ToLowerInvariant() switch
            {
                "pj" => CharacterName("@parle"),
                "heros" => CharacterName("@heros"),
                "pays" => Db.Content.World.CountryName,
                "monnaie" => Db.T("money"),
                "heure" => Clock.TimeText,
                "date" => Clock.DateText,
                "jour" => Clock.Day.ToString(),
                "periode" => Clock.Period,
                "lieu" => CurrentLocation.Name,
                "or" => State.Gold.ToString(),
                "karma" => GetKarma(arg.Length > 0 ? arg : "@parle").ToString(),
                "var" => GetVariable(arg).ToString(),
                "amitie" => GetFriendship(arg).ToString(),
                "nom" => CharacterName(arg),
                "membre" => CharacterName("@membre"),
                "classe" => Db.Characters.TryGetValue(ResolveWho(arg), out var pc) ? pc.Class : "",
                "titre" => Db.Characters.TryGetValue(ResolveWho(arg), out var pt) ? pt.Title : "",
                _ => m.Value,
            };
        });
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"%([a-zA-Z]+)(?::([^%\s]+))?%")]
    private static partial System.Text.RegularExpressions.Regex TagRegex();

    // ------------------------------------------------------------------ Quêtes

    public QuestStatus GetQuestStatus(string questId) =>
        State.Quests.TryGetValue(questId, out var p) ? p.Status : QuestStatus.NotStarted;

    public QuestProgress? QuestProgressOf(string questId) => State.Quests.GetValueOrDefault(questId);

    /// <summary>Étape en cours d'une quête à étapes (null sinon).</summary>
    public QuestStage? CurrentStage(QuestDef quest, QuestProgress progress) =>
        progress.StageId is { } id ? quest.Stages.FirstOrDefault(st => st.Id == id) : null;

    /// <summary>Objectifs actuellement à remplir (quête simple : sa liste ; quête à étapes : ceux de l'étape).</summary>
    public IReadOnlyList<QuestObjective> ActiveObjectives(QuestDef quest, QuestProgress progress) =>
        quest.IsStaged ? CurrentStage(quest, progress)?.Objectives ?? [] : quest.Objectives;

    public bool StartQuest(string questId)
    {
        if (!Db.Quests.TryGetValue(questId, out var quest) || State.Quests.ContainsKey(questId)) return false;
        var progress = new QuestProgress();
        State.Quests[questId] = progress;
        Notifications.Add($"Nouvelle quête : {quest.Name}");
        if (quest.IsStaged) EnterStage(quest, progress, quest.Stages[0]);
        UpdateQuests();
        return true;
    }

    /// <summary>Termine une quête avec succès et donne ses récompenses.</summary>
    public bool CompleteQuest(string questId)
    {
        if (!Db.Quests.TryGetValue(questId, out var quest)) return false;
        if (GetQuestStatus(questId) is QuestStatus.Completed or QuestStatus.Failed) return false;
        var progress = State.Quests.GetValueOrDefault(questId) ?? new QuestProgress();
        progress.Status = QuestStatus.Completed;
        progress.Step = quest.Objectives.Count;
        State.Quests[questId] = progress;
        Notifications.Add($"Quête terminée : {quest.Name}");
        foreach (var reward in quest.Rewards) Execute(reward);
        return true;
    }

    /// <summary>La quête échoue (pas de récompense).</summary>
    public bool FailQuest(string questId)
    {
        if (!Db.Quests.TryGetValue(questId, out var quest)) return false;
        if (GetQuestStatus(questId) is QuestStatus.Completed or QuestStatus.Failed) return false;
        var progress = State.Quests.GetValueOrDefault(questId) ?? new QuestProgress();
        progress.Status = QuestStatus.Failed;
        State.Quests[questId] = progress;
        Notifications.Add($"Quête échouée : {quest.Name}");
        return true;
    }

    /// <summary>Envoie une quête à étapes directement à une étape (la démarre si besoin) : sert aux choix de dialogue.</summary>
    public bool GoToStage(string questId, string stageId)
    {
        if (!Db.Quests.TryGetValue(questId, out var quest) || quest.Stages.FirstOrDefault(st => st.Id == stageId) is not { } stage) return false;
        if (GetQuestStatus(questId) is QuestStatus.Completed or QuestStatus.Failed) return false;
        if (!State.Quests.TryGetValue(questId, out var progress))
        {
            progress = new QuestProgress();
            State.Quests[questId] = progress;
            Notifications.Add($"Nouvelle quête : {quest.Name}");
        }
        EnterStage(quest, progress, stage);
        UpdateQuests();
        return true;
    }

    private void EnterStage(QuestDef quest, QuestProgress progress, QuestStage stage)
    {
        progress.StageId = stage.Id;
        progress.Step = 0;
        progress.Count = 0;
        progress.Path.Add(stage.Id);
        if (stage.Name.Length > 0 && !stage.IsEnding) Notifications.Add($"{quest.Name} : {FormatText(stage.Name)}");
        foreach (var action in stage.OnEnter.Where(a => a.Type != ActionType.StartBattle)) Execute(action);
        if (!stage.IsEnding) return;

        progress.EndingId = stage.Id;
        progress.Status = stage.Failure ? QuestStatus.Failed : QuestStatus.Completed;
        var ending = stage.Name.Length > 0 ? $" — {FormatText(stage.Name)}" : "";
        Notifications.Add(stage.Failure ? $"Quête échouée : {quest.Name}{ending}" : $"Quête terminée : {quest.Name}{ending}");
        if (!stage.Failure) foreach (var reward in quest.Rewards) Execute(reward);
    }

    /// <summary>Oublie une quête (outil développeur).</summary>
    public void ResetQuest(string questId) => State.Quests.Remove(questId);

    public IEnumerable<(QuestDef Quest, QuestProgress Progress)> QuestLog =>
        State.Quests.Where(kv => Db.Quests.ContainsKey(kv.Key)).Select(kv => (Db.Quests[kv.Key], kv.Value));

    public string ObjectiveText(QuestObjective o)
    {
        if (o.Description.Length > 0) return FormatText(o.Description);
        return o.Type switch
        {
            ObjectiveType.TalkTo => $"Parler à {NameOrId(Db.Npcs, o.TargetId, n => n.Name)}",
            ObjectiveType.Defeat => $"Vaincre {NameOrId(Db.Monsters, o.TargetId, m => m.Name)}" + (o.Count > 1 ? $" ×{o.Count}" : ""),
            ObjectiveType.Reach => $"Aller à {NameOrId(Db.Locations, o.TargetId, l => l.Name)}",
            _ => $"Apporter {NameOrId(Db.Items, o.TargetId, i => i.Name)}" + (o.Count > 1 ? $" ×{o.Count}" : "")
                 + (string.IsNullOrEmpty(o.NpcId) ? "" : $" à {NameOrId(Db.Npcs, o.NpcId, n => n.Name)}"),
        };
    }

    private static string NameOrId<T>(IReadOnlyDictionary<string, T> dict, string id, Func<T, string> name) =>
        dict.TryGetValue(id, out var v) ? name(v) : id;

    private bool _updatingQuests;

    /// <summary>
    /// Fait avancer les quêtes actives. Appelé avec un événement (PNJ à qui l'on parle, monstre vaincu),
    /// ou sans événement pour vérifier les objectifs "aller à" et "posséder un objet", les démarrages automatiques
    /// et les embranchements des quêtes à étapes (dont les conditions ont pu changer).
    /// </summary>
    public void UpdateQuests(ObjectiveType? eventType = null, string? eventTarget = null)
    {
        if (_updatingQuests) return; // un effet de quête peut rappeler cette méthode
        _updatingQuests = true;
        try
        {
            foreach (var quest in Db.Content.Quests.Where(q => q.AutoStart.Count > 0 && !State.Quests.ContainsKey(q.Id)).ToList())
            {
                if (CheckAll(quest.AutoStart))
                {
                    _updatingQuests = false;
                    StartQuest(quest.Id);
                    _updatingQuests = true;
                }
            }

            foreach (var questId in State.Quests.Where(kv => kv.Value.Status == QuestStatus.Active).Select(kv => kv.Key).ToList())
            {
                if (!Db.Quests.TryGetValue(questId, out var quest)) continue;
                var progress = State.Quests[questId];
                var consumed = false; // un même événement ne valide qu'un objectif par quête
                for (var guard = 0; guard < 30 && progress.Status == QuestStatus.Active; guard++)
                {
                    var objectives = ActiveObjectives(quest, progress);
                    if (!AdvanceObjectives(objectives, progress, eventType, eventTarget, ref consumed)) break;

                    // Tous les objectifs actuels sont remplis.
                    if (!quest.IsStaged)
                    {
                        CompleteQuest(questId);
                        break;
                    }
                    if (CurrentStage(quest, progress) is not { } stage) break;
                    if (stage.Exits.Count == 0)
                    {
                        // Étape sans suite : la quête est réussie.
                        progress.EndingId = stage.Id;
                        CompleteQuest(questId);
                        break;
                    }
                    var exit = stage.Exits.FirstOrDefault(x => CheckAll(x.Conditions) && quest.Stages.Any(st => st.Id == x.NextStageId));
                    if (exit is null) break; // on attend qu'une condition devienne vraie (choix, flag, heure...)
                    foreach (var action in exit.Actions.Where(a => a.Type != ActionType.StartBattle)) Execute(action);
                    EnterStage(quest, progress, quest.Stages.First(st => st.Id == exit.NextStageId));
                }
            }
        }
        finally
        {
            _updatingQuests = false;
        }
    }

    /// <summary>Avance dans une liste d'objectifs. Renvoie true quand ils sont tous remplis.</summary>
    private bool AdvanceObjectives(IReadOnlyList<QuestObjective> objectives, QuestProgress progress,
        ObjectiveType? eventType, string? eventTarget, ref bool consumed)
    {
        while (progress.Step < objectives.Count)
        {
            var o = objectives[progress.Step];
            var matches = !consumed && eventType == o.Type && eventTarget == o.TargetId;
            var done = false;
            switch (o.Type)
            {
                case ObjectiveType.Defeat:
                    if (matches) { progress.Count++; consumed = true; }
                    done = progress.Count >= Math.Max(1, o.Count);
                    break;
                case ObjectiveType.Reach:
                    done = State.CurrentLocationId == o.TargetId;
                    break;
                case ObjectiveType.TalkTo:
                    done = matches;
                    break;
                case ObjectiveType.Bring:
                    var enough = CountItem(o.TargetId) >= Math.Max(1, o.Count);
                    var atNpc = string.IsNullOrEmpty(o.NpcId)
                        || (!consumed && eventType == ObjectiveType.TalkTo && eventTarget == o.NpcId);
                    done = enough && atNpc;
                    if (done && o.ConsumeItems) RemoveItem(o.TargetId, Math.Max(1, o.Count));
                    break;
            }
            if (!done) return false;
            if (o.Type is ObjectiveType.TalkTo || (o.Type is ObjectiveType.Bring && !string.IsNullOrEmpty(o.NpcId)))
                consumed = true;
            progress.Step++;
            progress.Count = 0;
            if (progress.Step < objectives.Count) Notifications.Add($"Objectif accompli : {ObjectiveText(o)}");
        }
        return true;
    }

    // ------------------------------------------------------------------ Encyclopédie

    public void DiscoverCharacter(string id) => State.SeenCharacters.Add(id);
    public void DiscoverMonster(string id) => State.SeenMonsters.Add(id);
    public void DiscoverLocation(string id) => State.SeenLocations.Add(id);

    public void DiscoverItem(string id)
    {
        if (!Db.Items.TryGetValue(id, out var item)) return;
        if (item.Type == ItemType.Weapon) State.SeenWeapons.Add(id);
        else if (item.Type == ItemType.Relic) State.SeenRelics.Add(id);
    }

    /// <summary>Entrées découvertes d'une catégorie : (nom, sous-titre, description).</summary>
    public IReadOnlyList<(string Name, string Subtitle, string Description)> GetEncyclopedia(EncyclopediaCategory category)
    {
        IEnumerable<(string, string, string)> entries = category switch
        {
            EncyclopediaCategory.Characters => State.SeenCharacters.Select(id => Db.Characters[id])
                .Select(c => (c.Name, c.ClassAndTitle, c.Description))
                .Concat(State.SeenNpcs.Select(id => Db.Npcs[id])
                    .Select(n => (n.Name, "PNJ · " + NameOrId(Db.Locations, NpcLocation(n), l => l.Name), n.Description))),
            EncyclopediaCategory.Monsters => State.SeenMonsters.Select(id => Db.Monsters[id])
                .Select(m => (m.Name, m.IsBoss ? "Boss" : $"PV {m.Stats.MaxHp} · ATQ {m.Stats.Attack} · DEF {m.Stats.Defense}", m.Description)),
            EncyclopediaCategory.Locations => State.SeenLocations.Select(id => Db.Locations[id])
                .Select(l => (l.Name, LocationTypeLabel(l.Type), l.Description)),
            EncyclopediaCategory.Weapons => State.SeenWeapons.Select(id => Db.Items[id])
                .Select(i => (i.Name, i.Bonus.ToBonusString(), i.Description)),
            _ => State.SeenRelics.Select(id => Db.Items[id])
                .Select(i => (i.Name, i.RelicUsage == RelicUsage.Quest ? "Objet de quête" : i.Bonus.ToBonusString(), i.Description)),
        };
        return entries.OrderBy(e => e.Item1).ToList();
    }

    public static string LocationTypeName(LocationType type) => LocationTypeName(GameDatabase.Default, type);

    public static string LocationTypeName(GameDatabase db, LocationType type) => type switch
    {
        LocationType.City => db.T("loc.city"),
        LocationType.Dungeon => db.T("loc.dungeon"),
        _ => db.T("loc.wild"),
    };

    public string LocationTypeLabel(LocationType type) => LocationTypeName(Db, type);

    // ------------------------------------------------------------------ Équipe

    public IEnumerable<CharacterState> ActiveParty => State.Party.Where(c => c.IsActive);

    public CharacterDef DefOf(CharacterState c) => Db.Characters[c.DefId];

    public StatBlock GetStats(CharacterState c)
    {
        var def = DefOf(c);
        var stats = def.BaseStats + def.GrowthPerLevel.Times(c.Level - 1);
        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            if (c.GetEquipped(slot) is { } itemId && Db.Items.TryGetValue(itemId, out var item))
                stats += item.Bonus;
        }
        stats.MaxHp = Math.Max(1, stats.MaxHp);
        stats.MaxMana = Math.Max(0, stats.MaxMana);
        stats.Speed = Math.Max(1, stats.Speed);
        return stats;
    }

    public IReadOnlyList<SkillDef> GetSkills(CharacterState c) =>
        DefOf(c).Skills.Where(s => s.Level <= c.Level && Db.Skills.ContainsKey(s.SkillId)).Select(s => Db.Skills[s.SkillId]).ToList();

    public bool IsInParty(string characterId) => State.Party.Any(c => c.DefId == characterId);

    /// <summary>Ajoute un personnage à l'équipe (en réserve si les titulaires sont au complet).</summary>
    public bool Recruit(string characterId)
    {
        if (IsInParty(characterId) || !Db.Characters.TryGetValue(characterId, out var def)) return false;
        var c = new CharacterState
        {
            DefId = characterId,
            WeaponId = ValidItem(def.StartingWeaponId),
            ArmorId = ValidItem(def.StartingArmorId),
            RelicId = ValidItem(def.StartingRelicId),
            IsActive = ActiveParty.Count() < Config.MaxActiveParty,
            Karma = def.BaseKarma ?? Db.Content.Karma.Default,
        };
        var stats = GetStats(c);
        c.CurrentHp = stats.MaxHp;
        c.CurrentMana = stats.MaxMana;
        State.Party.Add(c);
        SetFlag($"recruited:{characterId}");
        DiscoverCharacter(characterId);
        foreach (var slot in Enum.GetValues<EquipSlot>())
            if (c.GetEquipped(slot) is { } id) DiscoverItem(id);
        return true;
    }

    /// <summary>Retire un PJ de l'équipe (ses objets équipés retournent dans le sac). Le dernier PJ reste.</summary>
    public bool Leave(string characterId)
    {
        var c = State.Party.FirstOrDefault(p => p.DefId == characterId);
        if (c is null || State.Party.Count <= 1) return false;
        foreach (var slot in Enum.GetValues<EquipSlot>()) Unequip(c, slot);
        State.Party.Remove(c);
        if (!State.Party.Any(p => p.IsActive)) State.Party[0].IsActive = true;
        if (State.HeroId == characterId) State.HeroId = State.Party[0].DefId;
        if (State.SpeakerId == characterId) State.SpeakerId = null;
        return true;
    }

    private string? ValidItem(string? id) => id is not null && Db.Items.ContainsKey(id) ? id : null;

    /// <summary>Passe un personnage de titulaire à réserve ou l'inverse.</summary>
    public bool ToggleActive(CharacterState c)
    {
        if (c.IsActive)
        {
            if (ActiveParty.Count() <= 1) return false; // il faut au moins un titulaire
            c.IsActive = false;
            return true;
        }
        if (ActiveParty.Count() >= Config.MaxActiveParty) return false;
        c.IsActive = true;
        return true;
    }

    public void HealAll()
    {
        foreach (var c in State.Party)
        {
            var s = GetStats(c);
            c.CurrentHp = s.MaxHp;
            c.CurrentMana = s.MaxMana;
        }
    }

    /// <summary>Garde PV/PM dans les limites après un changement d'équipement ou de niveau.</summary>
    public void ClampVitals(CharacterState c)
    {
        var s = GetStats(c);
        c.CurrentHp = Math.Clamp(c.CurrentHp, 0, s.MaxHp);
        c.CurrentMana = Math.Clamp(c.CurrentMana, 0, s.MaxMana);
    }

    // ------------------------------------------------------------------ Progression

    public int XpToNextLevel(int level) => Math.Max(1, Balance.XpPerLevel * level);

    /// <summary>Donne de l'XP. Renvoie le nombre de niveaux gagnés.</summary>
    public int GiveXp(CharacterState c, int xp)
    {
        var gained = 0;
        c.Xp += xp;
        while (c.Level < Balance.MaxLevel && c.Xp >= XpToNextLevel(c.Level))
        {
            var before = GetStats(c);
            c.Xp -= XpToNextLevel(c.Level);
            c.Level++;
            gained++;
            var after = GetStats(c);
            c.CurrentHp += after.MaxHp - before.MaxHp;
            c.CurrentMana += after.MaxMana - before.MaxMana;
        }
        ClampVitals(c);
        return gained;
    }

    // ------------------------------------------------------------------ Sac

    public int CountItem(string itemId) => State.Inventory.GetValueOrDefault(itemId);

    /// <summary>Nombre possédé, dans le sac ou équipé sur un personnage.</summary>
    public int OwnsCount(string itemId) =>
        CountItem(itemId) + State.Party.Count(c => c.WeaponId == itemId || c.ArmorId == itemId || c.RelicId == itemId);

    public bool OwnsItem(string itemId) => OwnsCount(itemId) > 0;

    public bool AddItem(string itemId, int count = 1)
    {
        if (count <= 0 || !Db.Items.TryGetValue(itemId, out var item)) return false;
        if (item.IsUnique)
        {
            if (OwnsItem(itemId)) return false;
            count = 1;
        }
        State.Inventory[itemId] = CountItem(itemId) + count;
        DiscoverItem(itemId);
        return true;
    }

    public bool RemoveItem(string itemId, int count = 1)
    {
        var have = CountItem(itemId);
        if (count <= 0 || have < count) return false;
        if (have == count) State.Inventory.Remove(itemId);
        else State.Inventory[itemId] = have - count;
        return true;
    }

    /// <summary>Objets du sac triés : consommables, armes, armures, reliques, quête.</summary>
    public IReadOnlyList<(ItemDef Item, int Count)> Bag() =>
        State.Inventory
            .Where(kv => kv.Value > 0 && Db.Items.ContainsKey(kv.Key))
            .Select(kv => (Db.Items[kv.Key], kv.Value))
            .OrderBy(t => t.Item1.Type).ThenBy(t => t.Item1.Name)
            .ToList();

    public bool Equip(CharacterState c, string itemId)
    {
        if (!Db.Items.TryGetValue(itemId, out var item) || item.Slot is not { } slot) return false;
        if (!RemoveItem(itemId)) return false;
        if (c.GetEquipped(slot) is { } previous) State.Inventory[previous] = CountItem(previous) + 1;
        c.SetEquipped(slot, itemId);
        ClampVitals(c);
        return true;
    }

    public bool Unequip(CharacterState c, EquipSlot slot)
    {
        if (c.GetEquipped(slot) is not { } itemId) return false;
        c.SetEquipped(slot, null);
        State.Inventory[itemId] = CountItem(itemId) + 1;
        ClampVitals(c);
        return true;
    }

    /// <summary>Utilise un consommable hors combat sur un personnage.</summary>
    public bool UseItem(string itemId, CharacterState target)
    {
        if (!Db.Items.TryGetValue(itemId, out var item) || !item.IsConsumable || CountItem(itemId) == 0) return false;
        var s = GetStats(target);
        if (target.CurrentHp >= s.MaxHp && target.CurrentMana >= s.MaxMana) return false;
        RemoveItem(itemId);
        target.CurrentHp = Math.Min(s.MaxHp, target.CurrentHp + item.HealHp);
        target.CurrentMana = Math.Min(s.MaxMana, target.CurrentMana + item.HealMana);
        return true;
    }

    // ------------------------------------------------------------------ Ville : boutique, auberge, habitants

    public IReadOnlyList<ItemDef> ShopStock =>
        InCity ? CurrentLocation.ShopItemIds.Where(Db.Items.ContainsKey).Select(id => Db.Items[id]).ToList() : [];

    public int SellPrice(ItemDef item) => item.Price * Math.Clamp(Balance.SellPercent, 0, 100) / 100;

    public bool CanBuy(ItemDef item) =>
        InCity && CurrentLocation.ShopItemIds.Contains(item.Id) && State.Gold >= item.Price
        && !(item.IsUnique && OwnsItem(item.Id));

    public bool Buy(string itemId)
    {
        if (!Db.Items.TryGetValue(itemId, out var item) || !CanBuy(item)) return false;
        State.Gold -= item.Price;
        AddItem(itemId);
        return true;
    }

    public bool Sell(string itemId)
    {
        if (!InCity || !Db.Items.TryGetValue(itemId, out var item) || !item.IsSellable) return false;
        if (!RemoveItem(itemId)) return false;
        State.Gold += SellPrice(item);
        return true;
    }

    /// <summary>Marque les armes et reliques de la boutique comme rencontrées.</summary>
    public void BrowseShop()
    {
        foreach (var item in ShopStock) DiscoverItem(item.Id);
    }

    public bool Rest()
    {
        if (!InCity || State.Gold < CurrentLocation.InnPrice) return false;
        State.Gold -= CurrentLocation.InnPrice;
        HealAll();
        AdvanceTime(Clock.MinutesUntilHour(Db.Content.Time.InnWakeHour));
        return true;
    }

    /// <summary>Lieu où se trouve un PNJ : déplacement forcé par un effet, sinon premier placement valide, sinon son lieu habituel.</summary>
    public string NpcLocation(NpcDef npc)
    {
        if (State.NpcLocations.TryGetValue(npc.Id, out var forced)) return forced;
        return npc.Placements.FirstOrDefault(p => CheckAll(p.Conditions))?.LocationId ?? npc.LocationId;
    }

    public IEnumerable<NpcDef> VisibleNpcs =>
        Db.Content.Npcs.Where(n => NpcLocation(n) == State.CurrentLocationId && CheckAll(n.VisibleConditions)).ToList();

    /// <summary>Parler à un PNJ : fait avancer les quêtes puis renvoie le dialogue à jouer (selon l'avancement et qui parle).</summary>
    public string? Talk(string npcId, string? speakerId = null)
    {
        if (!Db.Npcs.TryGetValue(npcId, out var npc)) return null;
        if (speakerId is not null) SetSpeaker(speakerId);
        State.SeenNpcs.Add(npcId);
        AdvanceTime(Db.Content.Time.TalkMinutes);
        UpdateQuests(ObjectiveType.TalkTo, npcId);
        var conditional = npc.ConditionalDialogues.FirstOrDefault(d => Db.Dialogues.ContainsKey(d.DialogueId) && CheckAll(d.Conditions));
        var id = conditional?.DialogueId ?? npc.DefaultDialogueId;
        return id is not null && Db.Dialogues.ContainsKey(id) ? id : null;
    }

    // ------------------------------------------------------------------ Carte et voyage

    public IReadOnlyList<LocationDef> Destinations =>
        CurrentLocation.ConnectedIds.Where(Db.Locations.ContainsKey).Select(id => Db.Locations[id]).Where(IsVisible).ToList();

    /// <summary>Le lieu apparaît sur la carte : révélé/caché par un effet, sinon selon ses conditions de visibilité.</summary>
    public bool IsVisible(LocationDef loc)
    {
        if (loc.Id == State.CurrentLocationId || State.RevealedLocations.Contains(loc.Id)) return true;
        if (State.HiddenLocations.Contains(loc.Id)) return false;
        return CheckAll(loc.VisibleConditions);
    }

    public bool CanEnter(LocationDef loc) => CheckAll(loc.AccessConditions);

    public static string FixedBattleDoneFlag(string battleId) => $"battle_won:{battleId}";
    private static string FixedBattleSeenFlag(string battleId) => $"battle_seen:{battleId}";

    /// <summary>Combat fixe du lieu actuel, s'il n'a pas encore été gagné.</summary>
    public FixedBattleDef? PendingFixedBattle =>
        CurrentLocation.FixedBattle is { MonsterIds.Count: > 0 } fb && !HasFlag(FixedBattleDoneFlag(fb.Id)) ? fb : null;

    private bool FixedAllowed => Config.TravelEncounters is TravelEncounterMode.FixedOnly or TravelEncounterMode.Both;
    private bool RandomAllowed => Config.TravelEncounters is TravelEncounterMode.RandomOnly or TravelEncounterMode.Both;

    /// <summary>Déplacement direct (sans rencontre). Renvoie true si c'est une première visite.</summary>
    public bool MoveTo(LocationDef loc)
    {
        State.CurrentLocationId = loc.Id;
        if (loc.IsCity) State.LastCityId = loc.Id;
        var first = State.SeenLocations.Add(loc.Id);
        UpdateQuests();
        return first;
    }

    public TravelResult Travel(string destinationId)
    {
        if (!CurrentLocation.ConnectedIds.Contains(destinationId) || !Db.Locations.TryGetValue(destinationId, out var dest) || !IsVisible(dest))
            return new TravelResult(false, "Ce lieu n'est pas accessible d'ici.");
        if (!CanEnter(dest))
            return new TravelResult(false, dest.LockedMessage.Length > 0 ? dest.LockedMessage : "Le passage est bloqué.");

        AdvanceTime(dest.TravelMinutes ?? Db.Content.Time.TravelMinutes);
        var firstVisit = MoveTo(dest);

        // 1. Combat fixe : déclenché automatiquement à la première arrivée, rejouable ensuite depuis la carte.
        if (FixedAllowed && PendingFixedBattle is { } fb && !HasFlag(FixedBattleSeenFlag(fb.Id)))
        {
            SetFlag(FixedBattleSeenFlag(fb.Id));
            return new TravelResult(true, DialogueId: ValidDialogue(fb.IntroDialogueId), BattleMonsterIds: fb.MonsterIds, FixedBattleId: fb.Id);
        }

        // 2. Dialogue de première visite.
        if (firstVisit && ValidDialogue(dest.FirstVisitDialogueId) is { } dialogueId)
            return new TravelResult(true, DialogueId: dialogueId);

        // 3. Rencontre aléatoire.
        if (RandomAllowed && RollEncounter(dest) is { } monsters)
            return new TravelResult(true, BattleMonsterIds: monsters);

        return new TravelResult(true);
    }

    private string? ValidDialogue(string? id) => id is not null && Db.Dialogues.ContainsKey(id) ? id : null;

    /// <summary>Chercher un combat dans la zone actuelle (bouton "Explorer").</summary>
    public IReadOnlyList<string>? Explore()
    {
        AdvanceTime(Db.Content.Time.ExploreMinutes);
        return PickEncounter(CurrentLocation);
    }

    private IReadOnlyList<string>? RollEncounter(LocationDef loc) =>
        loc.RandomEncounters.Count > 0 && Rng.NextDouble() < loc.EncounterChance ? PickEncounter(loc) : null;

    private IReadOnlyList<string>? PickEncounter(LocationDef loc)
    {
        var groups = loc.RandomEncounters.Where(g => g.Weight > 0 && g.MonsterIds.Count > 0 && g.MonsterIds.All(Db.Monsters.ContainsKey)).ToList();
        var total = groups.Sum(g => g.Weight);
        if (total <= 0) return null;
        var roll = Rng.Next(total);
        foreach (var g in groups)
        {
            if (roll < g.Weight) return g.MonsterIds;
            roll -= g.Weight;
        }
        return null;
    }

    // ------------------------------------------------------------------ Combat

    public Battle StartBattle(IReadOnlyList<string> monsterIds, string? fixedBattleId = null) =>
        new(this, monsterIds.Where(Db.Monsters.ContainsKey).ToList(), fixedBattleId);

    public BattleRewards ApplyVictory(Battle battle)
    {
        AdvanceTime(Db.Content.Time.BattleMinutes);
        var monsters = battle.Enemies.Select(e => e.Monster!).ToList();
        var xp = monsters.Sum(m => m.Xp);
        var gold = monsters.Sum(m => m.Gold);
        State.Gold += gold;

        var items = new List<string>();
        foreach (var drop in monsters.SelectMany(m => m.Drops))
        {
            if (Rng.NextDouble() < drop.Chance && AddItem(drop.ItemId)) items.Add(drop.ItemId);
        }

        var levelUps = new List<string>();
        foreach (var c in ActiveParty)
        {
            if (c.CurrentHp <= 0) c.CurrentHp = 1; // les K.O. se relèvent après la victoire
            var gained = GiveXp(c, xp);
            if (gained > 0) levelUps.Add($"{DefOf(c).Name} passe niveau {c.Level} !");
        }

        if (battle.FixedBattleId is { } id) SetFlag(FixedBattleDoneFlag(id));
        foreach (var m in monsters) UpdateQuests(ObjectiveType.Defeat, m.Id);
        return new BattleRewards(xp, gold, items, levelUps);
    }

    public DefeatResult ApplyDefeat()
    {
        if (Config.Defeat == DefeatRule.GameOver) return new DefeatResult(true, 0, null);
        AdvanceTime(Db.Content.Time.BattleMinutes);
        var lost = State.Gold * Math.Clamp(Config.DefeatGoldLossPercent, 0, 100) / 100;
        State.Gold -= lost;
        State.CurrentLocationId = State.LastCityId;
        HealAll();
        return new DefeatResult(false, lost, State.LastCityId);
    }

    public void AfterFlee()
    {
        AdvanceTime(Db.Content.Time.BattleMinutes);
        foreach (var c in ActiveParty.Where(c => c.CurrentHp <= 0)) c.CurrentHp = 1;
    }

    // ------------------------------------------------------------------ Dialogues

    public DialogueRunner StartDialogue(string dialogueId) => new(this, Db.Dialogues[dialogueId]);

    /// <summary>Quêtes affichées au joueur (les quêtes secrètes sont cachées).</summary>
    public IEnumerable<(QuestDef Quest, QuestProgress Progress)> VisibleQuestLog => QuestLog.Where(q => !q.Quest.Hidden);
}
