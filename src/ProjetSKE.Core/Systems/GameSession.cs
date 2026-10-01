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
        // Sauvegarde chargée : on repart des passifs qui agissent maintenant (sans rejouer leurs dialogues).
        CheckPassiveActivations(playDialogues: false);
    }

    public static GameSession NewGame(GameDatabase db, string heroId, Random? rng = null) => NewGame(db, heroId, null, rng);

    /// <summary>Nouvelle partie avec un départ donné (null = le départ du héros).</summary>
    public static GameSession NewGame(GameDatabase db, string heroId, string? startId, Random? rng = null) =>
        Create(db, heroId, startId is null ? db.StartFor(heroId) : db.StartById(startId), rng);

    /// <summary>Partie de prologue (avant la sélection des héros), selon les réglages du tutoriel.</summary>
    public static GameSession NewTutorial(GameDatabase db, Random? rng = null)
    {
        var t = db.Content.Tutorial;
        var hero = t.HeroId is { Length: > 0 } h && db.Characters.ContainsKey(h) ? h : "";
        var start = t.Start;
        if (!db.Locations.ContainsKey(start.LocationId)) start = new StartSettings { LocationId = db.Start.LocationId, Gold = t.Start.Gold };
        var session = Create(db, hero, start, rng, tutorial: t);
        return session;
    }

    private static GameSession Create(GameDatabase db, string heroId, StartSettings start, Random? rng, TutorialSettings? tutorial = null)
    {
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
        if (tutorial is not null)
        {
            state.IsTutorial = true;
            state.LockedFeatures = [.. tutorial.LockedAtStart];
        }
        var session = new GameSession(db, state, rng) { _quietPassives = true };
        foreach (var stack in start.Inventory) session.AddItem(stack.ItemId, stack.Count);
        if (heroId.Length > 0) session.Recruit(heroId);
        foreach (var companion in start.Companions) session.Recruit(companion);
        // Propre au héros joué : ses compagnons de route et sa situation de départ.
        // (pas pendant le prologue : on n'y joue pas encore son héros)
        var heroDef = tutorial is null ? db.Characters.GetValueOrDefault(heroId) : null;
        foreach (var companion in heroDef?.StartCompanions ?? []) session.Recruit(companion);
        foreach (var npc in db.Content.Npcs.Where(n => n.StartsInCamp)) session.JoinCamp(npc.Id, npc.StartRankId);
        foreach (var r in db.Content.Camp.Resources) state.CampResources[r.Id] = r.Initial;
        foreach (var b in db.Content.Camp.Buildings.Where(b => b.BuiltAtStart)) state.CampBuildings.Add(b.Id);
        state.CampLastDay = session.Clock.Day;
        session.DiscoverLocation(state.CurrentLocationId);
        foreach (var action in start.Actions.Where(a => a.Type != ActionType.StartBattle)) session.Execute(action);
        foreach (var action in (heroDef?.StartActions ?? []).Where(a => a.Type != ActionType.StartBattle)) session.Execute(action);
        // Événement déjà en cours au départ (ex : on commence un jour de fête).
        session.UpdateEvents(state.Minutes - 1);
        session.UpdateQuests();
        // Passifs déjà là au départ : pas de dialogue d'activation.
        session._quietPassives = false;
        session.CheckPassiveActivations(playDialogues: false);
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
        // Amitié / amour « envers l'équipe » (anciennes parties) : c'est désormais envers le héros.
        foreach (var relations in new[] { State.Relations, State.Love })
        {
            foreach (var key in relations.Keys.Where(k => k.EndsWith(">@equipe", StringComparison.Ordinal)).ToList())
            {
                var heroKey = key[..^"@equipe".Length] + State.HeroId;
                relations.TryAdd(heroKey, relations[key]);
                relations.Remove(key);
            }
        }
        // Jauges ajoutées après le début de la partie : valeur de départ du personnage.
        foreach (var c in State.Party.Concat(State.Offstage)) InitGauges(c);
        State.Camp.RemoveAll(m => !Db.Npcs.ContainsKey(m.Id) && !Db.Characters.ContainsKey(m.Id));
        foreach (var m in State.Camp)
        {
            if (!Db.Content.Camp.Ranks.Any(r => r.Id == m.RankId)) m.RankId = Db.Content.Camp.Ranks.OrderBy(r => r.Level).FirstOrDefault()?.Id ?? "";
            if (m.TaskId is { } t && !Db.Content.Camp.Tasks.Any(x => x.Id == t)) m.TaskId = null;
        }
        // Ressources ajoutées dans l'éditeur après le début de la partie : stock initial.
        foreach (var r in Db.Content.Camp.Resources) State.CampResources.TryAdd(r.Id, r.Initial);
        foreach (var id in State.CampResources.Keys.Where(id => !Db.Content.Camp.Resources.Any(r => r.Id == id)).ToList())
            State.CampResources.Remove(id);
        State.CampBuildings.RemoveWhere(id => !Db.Content.Camp.Buildings.Any(b => b.Id == id));
        if (State.CampLastDay <= 0) State.CampLastDay = Clock.Day;
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

    /// <summary>
    /// Dialogues à jouer dès que possible (effet « Dialogue : lancer » depuis une quête, un départ...),
    /// dans l'ordre. L'écran de jeu les joue un par un quand rien d'autre n'est affiché.
    /// </summary>
    public Queue<string> PendingDialogues { get; } = new();

    public GameConfig Config => State.Config;
    public BalanceSettings Balance => Db.Balance;
    public LocationDef CurrentLocation => Db.Locations[State.CurrentLocationId];
    public bool InCity => CurrentLocation.IsCity;
    /// <summary>Le lieu actuel a une auberge / une boutique (réglé lieu par lieu dans l'éditeur).</summary>
    public bool HasInn => CurrentLocation.HasInn;
    public bool HasShop => CurrentLocation.HasShop;
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
        // Dans l'équipe, ou présent dans la scène du dialogue en cours.
        ConditionType.InParty => IsPresent(c.Arg),
        ConditionType.NotInParty => !IsPresent(c.Arg),
        ConditionType.GoldAtLeast => State.Gold >= c.Amount,
        ConditionType.LevelAtLeast => MaxLevel >= c.Amount,
        ConditionType.Variable => Compare(GetVariable(c.Arg), c.Op, c.Amount),
        ConditionType.Karma => Compare(GetKarma(c.Arg), c.Op, c.Amount),
        ConditionType.Gauge => Compare(GetGauge(c.Arg, c.Arg2), c.Op, c.Amount),
        ConditionType.Stat => StatCondition(c),
        ConditionType.HasEffect => HasEffectCondition(c),
        ConditionType.CompareStats => CompareStatsCondition(c),
        ConditionType.HasPassive => HasActivePassive(c.Arg, c.Arg2),
        ConditionType.HasPower => Db.Powers.TryGetValue(c.Arg2, out var pw) && ConditionTargets(c.Arg).Any(p => PowersOf(p).Contains(pw)),
        ConditionType.Friendship => Compare(GetFriendship(c.Arg, c.Arg2), c.Op, c.Amount),
        ConditionType.Love => Compare(GetLove(c.Arg, c.Arg2), c.Op, c.Amount),
        ConditionType.Gold => Compare(State.Gold, c.Op, c.Amount),
        ConditionType.Level => Compare(MaxLevel, c.Op, c.Amount),
        ConditionType.PartySize => Compare(PartyCount(c.Arg), c.Op, c.Amount),
        ConditionType.Speaker => SpeakerId == c.Arg,
        ConditionType.IsHero => State.HeroId == c.Arg,
        ConditionType.ChoiceMade => State.Choices.Contains($"{c.Arg}:{c.Arg2}"),
        ConditionType.HourBetween => HourBetween(Clock.Hour, c.Amount, c.Amount2),
        ConditionType.Day => Compare(Clock.Day, c.Op, c.Amount),
        ConditionType.Period => SameName(Clock.Period, c.Arg),
        ConditionType.WeekDay => SameName(Clock.WeekDay, c.Arg),
        ConditionType.Month => SameName(Clock.Month, c.Arg),
        ConditionType.EventActive => IsEventActive(c.Arg),
        ConditionType.DungeonDone => State.DungeonsDone.Contains(c.Arg),
        // Être dans une taverne de Havrefort, c'est aussi être à Havrefort.
        ConditionType.AtLocation => Db.IsWithin(State.CurrentLocationId, c.Arg),
        ConditionType.Visited => State.SeenLocations.Contains(c.Arg),
        ConditionType.MetNpc => State.SeenNpcs.Contains(c.Arg),
        ConditionType.Chance => Rng.Next(100) < c.Amount,
        ConditionType.CampMember => CampMember(ResolveWho(c.Arg)) is not null,
        ConditionType.CampRank => CampMember(ResolveWho(c.Arg)) is not null && Compare(RankLevel(ResolveWho(c.Arg)), c.Op, c.Amount),
        ConditionType.CampTask => CampMember(ResolveWho(c.Arg))?.TaskId == c.Arg2,
        ConditionType.CampResource => Compare(GetCampResource(c.Arg), c.Op, c.Amount),
        ConditionType.CampBuilt => State.CampBuildings.Contains(c.Arg),
        ConditionType.QuestAtStage => QuestProgressOf(c.Arg) is { Status: QuestStatus.Active } p && p.StageId == c.Arg2,
        ConditionType.QuestStageReached => QuestProgressOf(c.Arg)?.Path.Contains(c.Arg2) == true,
        ConditionType.QuestEnding => QuestProgressOf(c.Arg)?.EndingId is { } ending && (c.Arg2.Length == 0 || ending == c.Arg2),
        ConditionType.QuestFailed => GetQuestStatus(c.Arg) == QuestStatus.Failed,
        ConditionType.QuestPartNotStarted => GetPartStatus(c.Arg, c.Arg2) == QuestStatus.NotStarted,
        ConditionType.QuestPartActive => GetPartStatus(c.Arg, c.Arg2) == QuestStatus.Active,
        ConditionType.QuestPartCompleted => GetPartStatus(c.Arg, c.Arg2) == QuestStatus.Completed,
        ConditionType.QuestPartFailed => GetPartStatus(c.Arg, c.Arg2) == QuestStatus.Failed,
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
            case ActionType.StartBattle or ActionType.StartTraining:
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
            case ActionType.GivePower or ActionType.RemovePower:
                if (!Db.Powers.TryGetValue(a.Arg, out var power)) break;
                foreach (var c in KarmaTargets(a.Arg2.Length > 0 ? a.Arg2 : "@parle"))
                {
                    var had = PowersOf(c).Contains(power);
                    if (a.Type == ActionType.GivePower) { c.LostPowers.Remove(power.Id); c.GainedPowers.Add(power.Id); }
                    else { c.GainedPowers.Remove(power.Id); c.LostPowers.Add(power.Id); }
                    var has = PowersOf(c).Contains(power);
                    if (had != has) Notifications.Add($"{DefOf(c).Name} {(has ? "maîtrise" : "perd")} le pouvoir « {power.Name} »");
                }
                break;
            case ActionType.GivePassive or ActionType.RemovePassive:
                if (!Db.Passives.TryGetValue(a.Arg, out var passive)) break;
                foreach (var c in KarmaTargets(a.Arg2.Length > 0 ? a.Arg2 : "@parle"))
                {
                    var had = PassivesOf(c).Contains(passive);
                    if (a.Type == ActionType.GivePassive) { c.LostPassives.Remove(passive.Id); c.GainedPassives.Add(passive.Id); }
                    else { c.GainedPassives.Remove(passive.Id); c.LostPassives.Add(passive.Id); }
                    var has = PassivesOf(c).Contains(passive);
                    if (had != has) Notifications.Add($"{DefOf(c).Name} {(has ? "obtient" : "perd")} le passif « {passive.Name} »");
                }
                break;
            case ActionType.AddGauge or ActionType.SetGauge:
                if (!Db.Gauges.TryGetValue(a.Arg, out var gauge)) break;
                foreach (var c in KarmaTargets(a.Arg2.Length > 0 ? a.Arg2 : "@parle"))
                {
                    var before = GaugeOf(c, gauge);
                    var now = gauge.Clamp(a.Type == ActionType.AddGauge ? before + a.Amount : a.Amount);
                    c.Gauges[gauge.Id] = now;
                    if (gauge.Visible && now != before)
                        Notifications.Add($"{DefOf(c).Name} : {gauge.Name} {Signed(now - before)}");
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
            case ActionType.AddLove or ActionType.SetLove:
            {
                var before = GetLove(a.Arg, a.Arg2);
                SetLove(a.Arg, a.Arg2, a.Type == ActionType.AddLove ? before + a.Amount : a.Amount);
                var now = GetLove(a.Arg, a.Arg2);
                if (Db.Content.Love.Visible && now != before)
                    Notifications.Add($"{CharacterName(a.Arg)} : {Db.Content.Love.Name} {Signed(now - before)}");
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
            case ActionType.ShowNpc:
                State.HiddenNpcs.Remove(a.Arg);
                State.ShownNpcs.Add(a.Arg);
                break;
            case ActionType.HideNpc:
                State.ShownNpcs.Remove(a.Arg);
                State.HiddenNpcs.Add(a.Arg);
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
            case ActionType.AddCampResource:
            {
                var before = GetCampResource(a.Arg);
                AddCampResource(a.Arg, a.Amount);
                var gained = GetCampResource(a.Arg) - before;
                if (gained != 0 && CampRules.Resources.FirstOrDefault(r => r.Id == a.Arg) is { } res)
                    Notifications.Add($"{res.Name} {Signed(gained)}");
                break;
            }
            case ActionType.BuildCampBuilding:
                if (MarkBuilt(a.Arg) && CampRules.Buildings.FirstOrDefault(b => b.Id == a.Arg) is { } built)
                    Notifications.Add($"Construit : {built.Name}");
                break;
            case ActionType.SetQuestStage:
                GoToStage(a.Arg, a.Arg2);
                break;
            case ActionType.FailQuest:
                FailQuest(a.Arg);
                break;
            case ActionType.StartDialogue:
                if (Db.Dialogues.ContainsKey(a.Arg)) PendingDialogues.Enqueue(a.Arg);
                break;
            case ActionType.UnlockFeature:
                if (UiFeatures.TryParse(a.Arg, out var unlock) && State.LockedFeatures.Remove(unlock))
                    Notifications.Add($"Débloqué : {FeatureName(unlock)}");
                break;
            case ActionType.LockFeature:
                if (UiFeatures.TryParse(a.Arg, out var locked)) State.LockedFeatures.Add(locked);
                break;
            case ActionType.EndTutorial:
                if (State.IsTutorial) State.TutorialDone = true;
                break;
            case ActionType.StartQuestPart:
                StartPart(a.Arg, a.Arg2);
                break;
            case ActionType.CompleteQuestPart:
                CompletePart(a.Arg, a.Arg2);
                break;
            case ActionType.FailQuestPart:
                FailPart(a.Arg, a.Arg2);
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
        var before = State.Minutes;
        State.Minutes += minutes;
        UpdateCamp();
        UpdateEvents(before);
        CheckPassiveActivations(); // passif « la nuit »...
    }

    // ------------------------------------------------------------------ Donjons

    /// <summary>Portes de donjon du lieu actuel.</summary>
    public IEnumerable<DungeonDef> DungeonsHere =>
        CurrentLocation.DungeonIds.Where(Db.Dungeons.ContainsKey).Select(id => Db.Dungeons[id]);

    /// <summary>La porte est ouverte : conditions remplies, et pas déjà terminé (sauf donjon qu'on peut refaire).</summary>
    public bool CanEnterDungeon(DungeonDef d) =>
        d.Steps.Count > 0 && CheckAll(d.Conditions) && (d.Repeatable || !State.DungeonsDone.Contains(d.Id));

    public DungeonDef? CurrentDungeon => State.Dungeon is { } run ? Db.Dungeons.GetValueOrDefault(run.Id) : null;

    /// <summary>Étape à jouer (null = pas dans un donjon).</summary>
    public DungeonStep? DungeonStep =>
        CurrentDungeon is { } d && State.Dungeon!.Step < d.Steps.Count ? d.Steps[State.Dungeon.Step] : null;

    public bool EnterDungeon(string id)
    {
        if (!Db.Dungeons.TryGetValue(id, out var d) || !CanEnterDungeon(d)) return false;
        State.Dungeon = new DungeonRun { Id = id, Step = 0 };
        SkipDungeonSteps(); // toutes les étapes sautées : terminé tout de suite
        return true;
    }

    /// <summary>Saute les étapes dont les conditions ne sont pas remplies ; termine le donjon après la dernière.</summary>
    private void SkipDungeonSteps()
    {
        while (CurrentDungeon is { } d && State.Dungeon is { } run)
        {
            if (run.Step >= d.Steps.Count) { FinishDungeon(d); return; }
            if (CheckAll(d.Steps[run.Step].Conditions)) return;
            run.Step++;
        }
    }

    /// <summary>L'étape en cours est réussie : ses effets, puis l'étape suivante (ou la fin). Renvoie true si le donjon est fini.</summary>
    public bool CompleteDungeonStep()
    {
        if (DungeonStep is not { } step || State.Dungeon is not { } run) return false;
        foreach (var action in step.Actions) Execute(action);
        run.Step++;
        SkipDungeonSteps();
        UpdateQuests();
        return State.Dungeon is null;
    }

    private void FinishDungeon(DungeonDef d)
    {
        State.Dungeon = null;
        State.DungeonsDone.Add(d.Id);
        Notifications.Add($"Donjon terminé : {d.Name}");
        foreach (var action in d.CompleteActions) Execute(action);
    }

    /// <summary>Quitter le donjon (fuite, abandon) : la progression est perdue.</summary>
    public void LeaveDungeon() => State.Dungeon = null;

    // ------------------------------------------------------------------ Événements du calendrier

    /// <summary>L'événement a lieu en ce moment (date, heure et conditions).</summary>
    public bool IsEventActive(string id) =>
        Db.Events.TryGetValue(id, out var e) && Db.Content.Time.Enabled
        && Calendar.IsOn(e, State.Minutes, Db.Content.Time) && CheckAll(e.Conditions);

    /// <summary>Événements en cours (pour les afficher).</summary>
    public IEnumerable<CalendarEventDef> ActiveEvents => Db.Content.Events.Where(e => IsEventActive(e.Id));

    /// <summary>
    /// Le temps a passé depuis <paramref name="before"/> : les événements qui ont commencé lancent leurs effets de début
    /// (une fois par occurrence, si leurs conditions sont remplies), ceux qui sont finis leurs effets de fin.
    /// </summary>
    public void UpdateEvents(long before)
    {
        var time = Db.Content.Time;
        if (!time.Enabled || Db.Content.Events.Count == 0) return;
        var now = State.Minutes;
        foreach (var e in Db.Content.Events)
        {
            foreach (var day in Calendar.CandidateDays(e, before, now, time))
            {
                var (start, end) = Calendar.Window(e, day, time);
                var key = $"{e.Id}@{day}";
                // Début : l'occurrence a commencé et n'était pas déjà finie au dernier passage.
                if (start <= now && end > before && !State.EventLog.Contains(key) && CheckAll(e.Conditions))
                {
                    State.EventLog.Add(key);
                    if (e.Announce) Notifications.Add(e.Message.Length > 0 ? FormatText(e.Message) : $"Événement : {e.Name}");
                    foreach (var action in e.StartActions) Execute(action);
                }
                // Fin : effets de fin d'une occurrence qui avait commencé.
                if (end <= now && State.EventLog.Contains(key) && State.EventLog.Add(key + ":fin"))
                    foreach (var action in e.EndActions) Execute(action);
            }
        }
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
    public string SpeakerId => State.SpeakerId is { } id && IsPresent(id) ? id : State.HeroId;

    public CharacterState? Speaker => State.Party.FirstOrDefault(c => c.DefId == SpeakerId) ?? State.Party.FirstOrDefault();

    public void SetSpeaker(string? characterId) => State.SpeakerId = characterId is not null && IsPresent(characterId) ? characterId : null;

    // ------------------------------------------------------------------ PJ présents dans la scène (hors groupe)

    /// <summary>PJ présents dans la scène du dialogue en cours, en plus du groupe (voir <see cref="DialogueDef.ScenePjIds"/>).</summary>
    public IReadOnlyList<string> SceneCast { get; private set; } = [];

    /// <summary>Scène du dialogue qui commence (null = fin du dialogue : plus personne en plus du groupe).</summary>
    public void SetScene(DialogueDef? dialogue) =>
        SceneCast = dialogue?.ScenePjIds.Where(Db.Characters.ContainsKey).Distinct().ToList() ?? [];

    /// <summary>Le PJ est là : dans le groupe, ou présent dans la scène du dialogue en cours.</summary>
    public bool IsPresent(string characterId) => IsInParty(characterId) || SceneCast.Contains(characterId);

    /// <summary>État d'un PJ : dans le groupe, sinon hors du groupe (créé si besoin pour garder ses valeurs).</summary>
    public CharacterState? StateOf(string characterId, bool create = false)
    {
        if (State.Party.FirstOrDefault(c => c.DefId == characterId) is { } member) return member;
        if (State.Offstage.FirstOrDefault(c => c.DefId == characterId) is { } away) return away;
        if (!create || !Db.Characters.TryGetValue(characterId, out var def)) return null;
        var created = NewCharacterState(def);
        State.Offstage.Add(created);
        return created;
    }

    /// <summary>« @parle » = PJ qui parle, « @heros » = héros, sinon identifiant tel quel.</summary>
    public string ResolveWho(string who) => who switch
    {
        "" or "@parle" => SpeakerId,
        "@heros" => State.HeroId,
        "@membre" => _campContext ?? SpeakerId,
        "@soi" => _passiveOwner ?? SpeakerId,
        _ => who,
    };

    private static int ClampScale(ScaleSettings s, int value) => Math.Clamp(value, Math.Min(s.Min, s.Max), Math.Max(s.Min, s.Max));

    /// <summary>Karma d'un PJ (« @parle », « @heros », id), ou moyenne de l'équipe (« @equipe »).</summary>
    public int GetKarma(string who)
    {
        if (who == "@equipe")
            return State.Party.Count > 0 ? (int)Math.Round(State.Party.Average(c => c.Karma)) : Db.Content.Karma.Default;
        var id = ResolveWho(who);
        // Même hors du groupe (présent dans la scène, ou parti) : ses valeurs sont gardées.
        return StateOf(id)?.Karma ?? (Db.Characters.TryGetValue(id, out var d) ? d.BaseKarma ?? Db.Content.Karma.Default : Db.Content.Karma.Default);
    }

    /// <summary>PJ concernés par une condition : un PJ précis (même hors du groupe), ou « @equipe » (le groupe et la scène).</summary>
    private IEnumerable<CharacterState> ConditionTargets(string who)
    {
        if (who == "@equipe") return State.Party.Concat(SceneCast.Select(id => StateOf(id)).OfType<CharacterState>()).Distinct();
        var id = ResolveWho(who.Length > 0 ? who : "@parle");
        return StateOf(id) is { } c ? [c] : Db.Characters.ContainsKey(id) ? [NewCharacterState(Db.Characters[id])] : [];
    }

    /// <summary>Le PJ (« @parle », « @heros », id ; « @equipe » = au moins un) a ce passif, et il agit.</summary>
    private bool HasActivePassive(string who, string passiveId)
    {
        if (!Db.Passives.TryGetValue(passiveId, out var p)) return false;
        // PNJ : ses passifs de fiche.
        if (who != "@equipe" && Db.Npcs.TryGetValue(ResolveWho(who.Length > 0 ? who : "@parle"), out var npc))
            return ActivePassives(npc.Id, npc.PassiveIds).Contains(p);
        return ConditionTargets(who).Any(c => PassivesOf(c).Contains(p) && IsPassiveActive(c, p));
    }

    /// <summary>Valeur d'une jauge (folie...) pour un PJ.</summary>
    public int GaugeOf(CharacterState c, CharacterGaugeDef gauge) =>
        c.Gauges.TryGetValue(gauge.Id, out var v) ? v : StartGauge(c.DefId, gauge);

    private int StartGauge(string characterId, CharacterGaugeDef gauge) =>
        gauge.Clamp(Db.Characters.TryGetValue(characterId, out var d) && d.BaseGauges.TryGetValue(gauge.Id, out var v) ? v : gauge.Default);

    private void InitGauges(CharacterState c)
    {
        foreach (var g in Db.Content.Gauges) c.Gauges.TryAdd(g.Id, StartGauge(c.DefId, g));
    }

    /// <summary>Jauge d'un PJ (« @parle », « @heros », id), ou moyenne de l'équipe (« @equipe »).</summary>
    public int GetGauge(string who, string gaugeId)
    {
        if (!Db.Gauges.TryGetValue(gaugeId, out var gauge)) return 0;
        if (who == "@equipe")
            return State.Party.Count > 0 ? (int)Math.Round(State.Party.Average(c => GaugeOf(c, gauge))) : gauge.Default;
        var id = ResolveWho(who.Length > 0 ? who : "@parle");
        return StateOf(id) is { } pc ? GaugeOf(pc, gauge) : StartGauge(id, gauge);
    }

    /// <summary>
    /// PJ visés par un effet : « @equipe » = le groupe et les PJ présents dans la scène ; un PJ précis = même hors
    /// du groupe (ses valeurs sont gardées pour quand il le rejoindra).
    /// </summary>
    private IEnumerable<CharacterState> KarmaTargets(string who)
    {
        if (who == "@equipe")
            return State.Party.Concat(SceneCast.Where(id => !IsInParty(id)).Select(id => StateOf(id, create: true)!)).ToList();
        var id = ResolveWho(who);
        return StateOf(id, create: true) is { } c ? [c] : [];
    }

    /// <summary>
    /// Envers qui : par défaut le héros (le PP, celui qu'on joue) ; « @parle » ou un PJ précis sinon.
    /// « @equipe » (ancien réglage) désigne aussi le héros : on ne joue que lui.
    /// </summary>
    private string ResolveToward(string toward) => toward is "" or "@equipe" or "@heros" ? State.HeroId : ResolveWho(toward);

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

    /// <summary>Amour d'un personnage (PNJ ou PJ) envers l'équipe ou un PJ précis (comme l'amitié).</summary>
    public int GetLove(string who, string toward = "")
    {
        who = ResolveWho(who);
        if (State.Love.TryGetValue(RelationKey(who, ResolveToward(toward)), out var value)) return value;
        return Db.Npcs.TryGetValue(who, out var npc) && npc.BaseLove is { } nb ? nb
            : Db.Characters.TryGetValue(who, out var pj) && pj.BaseLove is { } pb ? pb
            : Db.Content.Love.Default;
    }

    public void SetLove(string who, string toward, int value)
    {
        who = ResolveWho(who);
        if (string.IsNullOrEmpty(who)) return;
        State.Love[RelationKey(who, ResolveToward(toward))] = ClampScale(Db.Content.Love, value);
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
                "jauge" => GetGauge("@parle", arg).ToString(),
                "var" => GetVariable(arg).ToString(),
                "amitie" => GetFriendship(arg).ToString(),
                "amour" => GetLove(arg).ToString(),
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
        else if (quest.HasParts)
            foreach (var part in quest.Parts.Where(p => p.StartConditions.Count == 0)) BeginPart(quest, progress, part);
        UpdateQuests();
        return true;
    }

    // ------------------------------------------------------------------ Quêtes en plusieurs parties

    /// <summary>État d'une partie de quête (pas commencée si la quête ou la partie n'a pas démarré).</summary>
    public QuestStatus GetPartStatus(string questId, string partId) =>
        State.Quests.TryGetValue(questId, out var p) && p.Parts.TryGetValue(partId, out var part) ? part.Status : QuestStatus.NotStarted;

    private void BeginPart(QuestDef quest, QuestProgress progress, QuestPart part)
    {
        if (progress.Parts.ContainsKey(part.Id)) return;
        progress.Parts[part.Id] = new QuestProgress();
        if (part.Name.Length > 0) Notifications.Add($"{quest.Name} : {FormatText(part.Name)}");
    }

    /// <summary>Démarre une partie (et la quête si besoin) : effet « Quête : démarrer une partie ».</summary>
    public bool StartPart(string questId, string partId)
    {
        if (!Db.Quests.TryGetValue(questId, out var quest) || quest.Parts.FirstOrDefault(p => p.Id == partId) is not { } part) return false;
        if (GetQuestStatus(questId) == QuestStatus.NotStarted) StartQuest(questId);
        if (State.Quests.GetValueOrDefault(questId) is not { Status: QuestStatus.Active } progress || progress.Parts.ContainsKey(partId)) return false;
        BeginPart(quest, progress, part);
        UpdateQuests();
        return true;
    }

    public bool CompletePart(string questId, string partId) => EndPart(questId, partId, QuestStatus.Completed);

    public bool FailPart(string questId, string partId) => EndPart(questId, partId, QuestStatus.Failed);

    private bool EndPart(string questId, string partId, QuestStatus status)
    {
        if (!Db.Quests.TryGetValue(questId, out var quest) || quest.Parts.FirstOrDefault(p => p.Id == partId) is not { } part) return false;
        if (GetQuestStatus(questId) == QuestStatus.NotStarted) StartQuest(questId);
        if (State.Quests.GetValueOrDefault(questId) is not { Status: QuestStatus.Active } progress) return false;
        if (!progress.Parts.TryGetValue(partId, out var pp)) progress.Parts[partId] = pp = new QuestProgress();
        if (pp.Status is QuestStatus.Completed or QuestStatus.Failed) return false;
        FinishPart(quest, part, pp, status);
        CheckPartsDone(quest, progress);
        UpdateQuests();
        return true;
    }

    private void FinishPart(QuestDef quest, QuestPart part, QuestProgress pp, QuestStatus status)
    {
        pp.Status = status;
        var name = part.Name.Length > 0 ? FormatText(part.Name) : part.Id;
        if (status == QuestStatus.Completed)
        {
            pp.Step = part.Objectives.Count;
            Notifications.Add($"Partie terminée : {name}");
            foreach (var reward in part.Rewards.Where(a => a.Type != ActionType.StartBattle)) Execute(reward);
        }
        else Notifications.Add($"Partie échouée : {name}");
    }

    /// <summary>La quête réussit quand toutes les parties obligatoires sont terminées ; elle échoue si l'une d'elles échoue.</summary>
    private void CheckPartsDone(QuestDef quest, QuestProgress progress)
    {
        if (progress.Status != QuestStatus.Active) return;
        var required = quest.Parts.Where(p => !p.Optional).ToList();
        if (required.Any(p => progress.Parts.GetValueOrDefault(p.Id)?.Status == QuestStatus.Failed)) FailQuest(quest.Id);
        else if (required.Count > 0 && required.All(p => progress.Parts.GetValueOrDefault(p.Id)?.Status == QuestStatus.Completed)) CompleteQuest(quest.Id);
        else if (required.Count == 0 && quest.Parts.All(p => progress.Parts.GetValueOrDefault(p.Id)?.Status is QuestStatus.Completed or QuestStatus.Failed))
            CompleteQuest(quest.Id);
    }

    /// <summary>Fait avancer les parties d'une quête : démarrages, échecs, objectifs.</summary>
    private void UpdateParts(QuestDef quest, QuestProgress progress, ObjectiveType? eventType, string? eventTarget)
    {
        foreach (var part in quest.Parts)
        {
            if (progress.Status != QuestStatus.Active) return;
            if (!progress.Parts.TryGetValue(part.Id, out var pp))
            {
                if (part.StartConditions.Count == 0 || !CheckAll(part.StartConditions)) continue;
                BeginPart(quest, progress, part);
                pp = progress.Parts[part.Id];
            }
            if (pp.Status != QuestStatus.Active) continue;
            if (part.FailConditions.Count > 0 && CheckAll(part.FailConditions))
            {
                FinishPart(quest, part, pp, QuestStatus.Failed);
                continue;
            }
            var consumed = false; // chaque partie peut réagir au même événement
            if (part.Objectives.Count > 0 && AdvanceObjectives(part.Objectives, pp, eventType, eventTarget, ref consumed))
                FinishPart(quest, part, pp, QuestStatus.Completed);
        }
        CheckPartsDone(quest, progress);
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
                if (quest.HasParts)
                {
                    UpdateParts(quest, progress, eventType, eventTarget);
                    continue;
                }
                // Quête simple sans objectif : elle ne se termine que par l'effet « Quête : terminer »
                // (sinon « aucun objectif » voudrait dire « tous remplis » et elle finirait dès son départ).
                if (!quest.IsStaged && quest.Objectives.Count == 0) continue;
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
        CheckPassiveActivations();
    }

    /// <summary>
    /// Repère les passifs des PJ du groupe qui viennent de se mettre à agir (ou d'arrêter) et met leur dialogue
    /// en file (joué dès que possible, avec ce PJ comme « celui qui parle »).
    /// </summary>
    // Création de la partie en cours : on ne joue pas encore les dialogues d'activation.
    private bool _quietPassives;

    public void CheckPassiveActivations(bool playDialogues = true)
    {
        if (_quietPassives) return;
        var now = new HashSet<string>();
        foreach (var c in State.Party)
            foreach (var p in ActivePassives(c))
                now.Add($"{c.DefId}:{p.Id}");
        if (playDialogues)
        {
            foreach (var key in now.Where(k => !State.ActivePassiveLog.Contains(k)))
                QueuePassiveDialogue(key, activated: true);
            foreach (var key in State.ActivePassiveLog.Where(k => !now.Contains(k)))
                QueuePassiveDialogue(key, activated: false);
        }
        State.ActivePassiveLog = now;
    }

    private void QueuePassiveDialogue(string key, bool activated)
    {
        var parts = key.Split(':', 2);
        if (parts.Length < 2 || !Db.Passives.TryGetValue(parts[1], out var p)) return;
        var dialogue = activated ? p.ActivationDialogueId : p.DeactivationDialogueId;
        if (dialogue is null || !Db.Dialogues.ContainsKey(dialogue)) return;
        if (activated && p.ActivationOnce && !State.PassiveDialoguesPlayed.Add(key)) return;
        if (State.Party.Any(c => c.DefId == parts[0])) SetSpeaker(parts[0]);
        PendingDialogues.Enqueue(dialogue);
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
                    done = Db.IsWithin(State.CurrentLocationId, o.TargetId);
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
            // Les PNJ combattus restent dans « Personnages », pas dans le bestiaire.
            EncyclopediaCategory.Monsters => State.SeenMonsters.Where(id => Db.Monsters.ContainsKey(id) && !Db.Npcs.ContainsKey(id)).Select(id => Db.Monsters[id])
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

    /// <summary>PV du héros (personnage principal) en % de ses PV max (100 s'il n'est pas dans l'équipe).</summary>
    public int HeroHpPercent
    {
        get
        {
            if (State.Party.FirstOrDefault(c => c.DefId == State.HeroId) is not { } hero) return 100;
            var max = GetStats(hero).MaxHp;
            return max <= 0 ? 100 : (int)Math.Ceiling(Math.Clamp(hero.CurrentHp, 0, max) * 100.0 / max);
        }
    }

    // ------------------------------------------------------------------ Passifs

    /// <summary>Porteur du passif dont on vérifie les conditions (« @soi »).</summary>
    private string? _passiveOwner;

    /// <summary>Passifs du personnage (fiche selon son niveau, + donnés, − retirés), qu'ils agissent ou non.</summary>
    public IReadOnlyList<PassiveDef> PassivesOf(CharacterState c) =>
        DefOf(c).Passives.Where(p => p.Level <= c.Level).Select(p => p.PassiveId)
            .Concat(c.GainedPassives)
            .Where(id => !c.LostPassives.Contains(id))
            .Distinct()
            .Where(Db.Passives.ContainsKey)
            .Select(id => Db.Passives[id])
            .ToList();

    /// <summary>Le passif agit en ce moment (ses conditions passent, « @soi » = le porteur).</summary>
    public bool IsPassiveActive(CharacterState c, PassiveDef p)
    {
        if (p.Conditions.Count == 0) return true;
        var previous = _passiveOwner;
        _passiveOwner = c.DefId;
        try { return CheckAll(p.Conditions); }
        finally { _passiveOwner = previous; }
    }

    /// <summary>Passifs d'un PNJ ou d'un monstre qui agissent en ce moment (« @soi » = lui).</summary>
    public IReadOnlyList<PassiveDef> ActivePassives(string ownerId, IEnumerable<string> passiveIds)
    {
        var result = new List<PassiveDef>();
        var previous = _passiveOwner;
        _passiveOwner = ownerId;
        try
        {
            foreach (var id in passiveIds.Distinct())
                if (Db.Passives.TryGetValue(id, out var p) && (p.Conditions.Count == 0 || CheckAll(p.Conditions))) result.Add(p);
        }
        finally { _passiveOwner = previous; }
        return result;
    }

    /// <summary>Stats de base + passifs (bonus fixes, puis en % du total).</summary>
    public static StatBlock WithPassives(StatBlock stats, IEnumerable<PassiveDef> passives)
    {
        var list = passives.ToList();
        foreach (var p in list) stats += p.Bonus;
        var percent = new StatBlock();
        foreach (var p in list) percent += p.Percent;
        stats = new StatBlock(
            stats.MaxHp + stats.MaxHp * percent.MaxHp / 100,
            stats.MaxMana + stats.MaxMana * percent.MaxMana / 100,
            stats.Attack + stats.Attack * percent.Attack / 100,
            stats.Defense + stats.Defense * percent.Defense / 100,
            stats.Magic + stats.Magic * percent.Magic / 100,
            stats.Speed + stats.Speed * percent.Speed / 100);
        stats.MaxHp = Math.Max(1, stats.MaxHp);
        stats.MaxMana = Math.Max(0, stats.MaxMana);
        stats.Speed = Math.Max(1, stats.Speed);
        return stats;
    }

    /// <summary>Passifs qui agissent en ce moment.</summary>
    public IReadOnlyList<PassiveDef> ActivePassives(CharacterState c) => PassivesOf(c).Where(p => IsPassiveActive(c, p)).ToList();

    /// <summary>Bonus d'XP / d'or en % des passifs actifs de l'équipe (cumulés).</summary>
    public int PartyPassivePercent(Func<PassiveDef, int> pick) => ActiveParty.Sum(c => ActivePassives(c).Sum(pick));

    private readonly HashSet<string> _computingStats = [];

    /// <summary>Stats d'un PJ sans ses passifs (base, niveau, équipement).</summary>
    public StatBlock StatsWithoutPassives(CharacterState c)
    {
        var def = DefOf(c);
        var stats = def.BaseStats + def.GrowthPerLevel.Times(c.Level - 1);
        foreach (var slot in Enum.GetValues<EquipSlot>())
            if (c.GetEquipped(slot) is { } itemId && Db.Items.TryGetValue(itemId, out var item))
                stats += item.Bonus;
        return stats;
    }

    public StatBlock GetStats(CharacterState c)
    {
        var def = DefOf(c);
        var stats = def.BaseStats + def.GrowthPerLevel.Times(c.Level - 1);
        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            if (c.GetEquipped(slot) is { } itemId && Db.Items.TryGetValue(itemId, out var item))
                stats += item.Bonus;
        }
        // Passifs : bonus fixes, puis en % du total. Une condition de passif qui regarde les stats de ce même PJ
        // (« ATQ de @soi ≥ 20 ») les voit sans les passifs : sinon le calcul tournerait en rond.
        if (!_computingStats.Add(c.DefId)) return stats;
        try { return WithPassives(stats, ActivePassives(c)); }
        finally { _computingStats.Remove(c.DefId); }
    }

    /// <summary>Compétences connues : celles de sa fiche, puis celles de ses pouvoirs (chacune à son niveau).</summary>
    public IReadOnlyList<SkillDef> GetSkills(CharacterState c) =>
        DefOf(c).Skills.Where(s => s.Level <= c.Level && Db.Skills.ContainsKey(s.SkillId)).Select(s => Db.Skills[s.SkillId])
            .Concat(PowersOf(c).SelectMany(p => Db.SkillsOfPower(p.Id)).Where(s => s.PowerLevel <= c.Level))
            .Distinct()
            .ToList();

    /// <summary>Pouvoirs du personnage (fiche + donnés − retirés).</summary>
    public IReadOnlyList<PowerDef> PowersOf(CharacterState c) =>
        DefOf(c).PowerIds.Concat(c.GainedPowers)
            .Where(id => !c.LostPowers.Contains(id))
            .Distinct()
            .Where(Db.Powers.ContainsKey)
            .Select(id => Db.Powers[id])
            .ToList();

    public bool IsInParty(string characterId) => State.Party.Any(c => c.DefId == characterId);

    /// <summary>Ajoute un personnage à l'équipe (en réserve si les titulaires sont au complet).</summary>
    public bool Recruit(string characterId)
    {
        if (IsInParty(characterId) || !Db.Characters.TryGetValue(characterId, out var def)) return false;
        // Déjà rencontré hors du groupe (scène, départ) : il revient avec ses valeurs (karma, folie, passifs...).
        var c = State.Offstage.FirstOrDefault(o => o.DefId == characterId);
        if (c is not null) State.Offstage.Remove(c);
        else c = NewCharacterState(def);
        c.IsActive = ActiveParty.Count() < Config.MaxActiveParty;
        State.Party.Add(c);
        SetFlag($"recruited:{characterId}");
        DiscoverCharacter(characterId);
        foreach (var slot in Enum.GetValues<EquipSlot>())
            if (c.GetEquipped(slot) is { } id) DiscoverItem(id);
        return true;
    }

    /// <summary>État tout neuf d'un PJ : équipement, karma et jauges de départ, PV et PM pleins.</summary>
    private CharacterState NewCharacterState(CharacterDef def)
    {
        var c = new CharacterState
        {
            DefId = def.Id,
            WeaponId = ValidItem(def.StartingWeaponId),
            ArmorId = ValidItem(def.StartingArmorId),
            RelicId = ValidItem(def.StartingRelicId),
            Karma = def.BaseKarma ?? Db.Content.Karma.Default,
        };
        InitGauges(c);
        foreach (var gearId in def.StartingGearIds)
            if (ValidItem(gearId) is { } g && Db.Items[g].Slot is { } gearSlot && c.GetEquipped(gearSlot) is null) c.SetEquipped(gearSlot, g);
        var stats = GetStats(c);
        c.CurrentHp = stats.MaxHp;
        c.CurrentMana = stats.MaxMana;
        return c;
    }

    /// <summary>Retire un PJ de l'équipe (ses objets équipés retournent dans le sac). Le dernier PJ reste.</summary>
    public bool Leave(string characterId)
    {
        var c = State.Party.FirstOrDefault(p => p.DefId == characterId);
        if (c is null || State.Party.Count <= 1) return false;
        foreach (var slot in Enum.GetValues<EquipSlot>()) Unequip(c, slot);
        State.Party.Remove(c);
        State.Offstage.Add(c); // ses valeurs (karma, folie, passifs...) sont gardées s'il revient
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
        CountItem(itemId) + State.Party.Sum(c => Enum.GetValues<EquipSlot>().Count(slot => c.GetEquipped(slot) == itemId));

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
        HasShop ? CurrentLocation.ShopItemIds.Where(Db.Items.ContainsKey).Select(id => Db.Items[id]).ToList() : [];

    public int SellPrice(ItemDef item) => item.Price * Math.Clamp(Balance.SellPercent, 0, 100) / 100;

    public bool CanBuy(ItemDef item) =>
        HasShop && CurrentLocation.ShopItemIds.Contains(item.Id) && State.Gold >= item.Price
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
        if (!HasShop || !Db.Items.TryGetValue(itemId, out var item) || !item.IsSellable) return false;
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
        if (!HasInn || State.Gold < CurrentLocation.InnPrice) return false;
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
        Db.Content.Npcs.Where(n => NpcLocation(n) == State.CurrentLocationId && IsNpcVisible(n)).ToList();

    /// <summary>Le PNJ se montre : montré/caché par un effet, sinon caché s'il l'est au début, sinon selon ses conditions.</summary>
    public bool IsNpcVisible(NpcDef npc)
    {
        if (State.HiddenNpcs.Contains(npc.Id)) return false;
        if (State.ShownNpcs.Contains(npc.Id)) return true;
        return !npc.HiddenAtStart && CheckAll(npc.VisibleConditions);
    }

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

    // ------------------------------------------------------------------ Interface verrouillée (prologue)

    public bool IsLocked(UiFeature feature) => State.LockedFeatures.Contains(feature);

    /// <summary>Nom d'une partie de l'interface, pour le joueur.</summary>
    public static string FeatureName(UiFeature f) => f switch
    {
        UiFeature.TabCamp => "l'onglet Camp",
        UiFeature.TabMap => "l'onglet Carte",
        UiFeature.TabQuests => "l'onglet Quêtes",
        UiFeature.TabEncyclopedia => "l'onglet Encyclopédie",
        UiFeature.TabShop => "l'onglet Boutique",
        UiFeature.TabJournal => "l'onglet Journal",
        UiFeature.TabMenu => "l'onglet Menu",
        UiFeature.WorldMap => "la carte du royaume",
        UiFeature.Explore => "l'exploration",
        UiFeature.BattleSkills => "les compétences en combat",
        UiFeature.BattleItems => "les objets en combat",
        UiFeature.BattleFlee => "la fuite en combat",
        UiFeature.BattleDefend => "la défense en combat",
        UiFeature.CampManagement => "la gestion du camp",
        UiFeature.CampResources => "les ressources du camp",
        UiFeature.CampPeople => "les persos du camp",
        UiFeature.CampTeam => "l'équipe",
        UiFeature.CampBag => "le sac",
        UiFeature.CampPlaces => "les lieux du camp",
        _ => f.ToString(),
    };

    // ------------------------------------------------------------------ Carte et voyage

    /// <summary>Lieux du royaume où l'on peut voyager (depuis le lieu du royaume où l'on se trouve, même dans un sous-lieu).</summary>
    public IReadOnlyList<LocationDef> Destinations =>
        RootLocation.ConnectedIds.Where(Db.Locations.ContainsKey).Select(id => Db.Locations[id]).Where(IsVisible).ToList();

    /// <summary>Lieu de la carte du royaume où se trouve l'équipe (le lieu actuel, ou celui qui le contient).</summary>
    public LocationDef RootLocation => Db.RootOf(CurrentLocation);

    /// <summary>Sous-lieux visibles du lieu actuel (on peut y entrer).</summary>
    public IReadOnlyList<LocationDef> SubLocations => Db.ChildrenOf(State.CurrentLocationId).Where(IsVisible).ToList();

    /// <summary>
    /// Déplacement à l'intérieur : entrer dans un sous-lieu, en sortir vers le lieu qui le contient,
    /// ou passer d'un sous-lieu à un autre du même lieu.
    /// </summary>
    public bool IsInnerMove(LocationDef dest) =>
        dest.ParentId == State.CurrentLocationId
        || IsExit(dest)
        || (CurrentLocation.ParentId is { Length: > 0 } parent && dest.ParentId == parent);

    /// <summary>Le lieu contient le lieu actuel (en sortir est toujours possible).</summary>
    private bool IsExit(LocationDef dest) => dest.Id != State.CurrentLocationId && Db.IsWithin(State.CurrentLocationId, dest.Id);

    /// <summary>
    /// Le lieu apparaît sur la carte : révélé/caché par un effet, sinon caché s'il l'est au début,
    /// sinon selon ses conditions de visibilité.
    /// </summary>
    public bool IsVisible(LocationDef loc)
    {
        if (loc.Id == State.CurrentLocationId || State.RevealedLocations.Contains(loc.Id)) return true;
        if (State.HiddenLocations.Contains(loc.Id) || loc.HiddenAtStart) return false;
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
        if (!Db.Locations.TryGetValue(destinationId, out var dest) || !IsVisible(dest))
            return new TravelResult(false, "Ce lieu n'est pas accessible d'ici.");
        var inner = IsInnerMove(dest);
        if (!inner && !RootLocation.ConnectedIds.Contains(destinationId))
            return new TravelResult(false, "Ce lieu n'est pas accessible d'ici.");
        // Sortir vers le lieu qui nous contient est toujours possible ; entrer peut être bloqué.
        var exit = IsExit(dest);
        if (!exit && !CanEnter(dest))
            return new TravelResult(false, dest.LockedMessage.Length > 0 ? dest.LockedMessage : "Le passage est bloqué.");

        // Voyage dans le royaume : durée du voyage. À l'intérieur d'un lieu : la durée réglée sur le sous-lieu (0 par défaut).
        AdvanceTime(inner ? (exit ? 0 : dest.TravelMinutes ?? 0) : dest.TravelMinutes ?? Db.Content.Time.TravelMinutes);
        var firstVisit = MoveTo(dest);

        // 1. Combat fixe : déclenché automatiquement à la première arrivée, rejouable ensuite depuis la carte.
        if (FixedAllowed && PendingFixedBattle is { } fb && !HasFlag(FixedBattleSeenFlag(fb.Id)))
        {
            SetFlag(FixedBattleSeenFlag(fb.Id));
            return new TravelResult(true, DialogueId: ValidDialogue(fb.IntroDialogueId), BattleMonsterIds: fb.MonsterIds, FixedBattleId: fb.Id);
        }

        // 2. Un PNJ du lieu attaque l'équipe.
        if (HostileNpc() is { } npc)
        {
            var c = npc.Combat!;
            return new TravelResult(true, DialogueId: ValidDialogue(c.AttackDialogueId),
                BattleMonsterIds: [npc.Id, .. c.AllyIds.Where(Db.Monsters.ContainsKey)]);
        }

        // 3. Dialogue de première visite.
        if (firstVisit && ValidDialogue(dest.FirstVisitDialogueId) is { } dialogueId)
            return new TravelResult(true, DialogueId: dialogueId);

        // 4. Rencontre aléatoire.
        if (RandomAllowed && RollEncounter(dest) is { } monsters)
            return new TravelResult(true, BattleMonsterIds: monsters);

        return new TravelResult(true);
    }

    /// <summary>Flag posé quand un PNJ a été vaincu (utilisable en condition).</summary>
    public static string NpcBeatenFlag(string npcId) => $"pnj_vaincu:{npcId}";

    /// <summary>
    /// PNJ du lieu actuel qui attaque l'équipe : il se bat, il est réglé pour attaquer, ses conditions passent,
    /// et il n'a pas déjà été vaincu (sauf s'il attaque encore après une défaite).
    /// </summary>
    public NpcDef? HostileNpc() => VisibleNpcs.FirstOrDefault(n =>
        n.Combat is { Attacks: true } c
        && (c.AttacksAgain || !HasFlag(NpcBeatenFlag(n.Id)))
        && CheckAll(c.AttackConditions));

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

    /// <summary>Adversaires du terrain d'entraînement du lieu actuel (vide = pas de terrain).</summary>
    public IReadOnlyList<MonsterDef> TrainingOpponents =>
        CurrentLocation.Training ? CurrentLocation.TrainingOpponentIds.Where(Db.Monsters.ContainsKey).Select(id => Db.Monsters[id]).ToList() : [];

    /// <summary>Combat d'entraînement (sans risque) : voir <see cref="Battle.IsTraining"/>.</summary>
    public Battle StartTraining(IReadOnlyList<string> opponentIds)
    {
        var before = ActiveParty.Select(c => (c, c.CurrentHp, c.CurrentMana)).ToList();
        var battle = new Battle(this, opponentIds.Where(Db.Monsters.ContainsKey).ToList(), null) { IsTraining = true };
        battle.Before.AddRange(before);
        return battle;
    }

    /// <summary>Fin d'un entraînement : chacun retrouve ses PV et PM d'avant.</summary>
    private void EndTraining(Battle battle)
    {
        foreach (var (c, hp, mana) in battle.Before)
        {
            c.CurrentHp = hp;
            c.CurrentMana = mana;
        }
    }

    public BattleRewards ApplyVictory(Battle battle)
    {
        AdvanceTime(Db.Content.Time.BattleMinutes);
        if (battle.IsTraining)
        {
            // Entraînement : XP réduite seulement (ni or, ni butin, ni quête), puis chacun se remet.
            var trainingXp = battle.Enemies.Sum(e => e.Monster!.Xp) * Math.Clamp(Balance.TrainingXpPercent, 0, 1000) / 100;
            var ups = new List<string>();
            foreach (var c in ActiveParty)
                if (GiveXp(c, trainingXp) > 0) ups.Add($"{DefOf(c).Name} passe niveau {c.Level} !");
            EndTraining(battle);
            return new BattleRewards(trainingXp, 0, [], ups);
        }
        var monsters = battle.Enemies.Select(e => e.Monster!).ToList();
        // Passifs de l'équipe : bonus d'XP et d'or en %.
        var xp = monsters.Sum(m => m.Xp);
        xp += xp * PartyPassivePercent(p => p.XpPercent) / 100;
        var gold = monsters.Sum(m => m.Gold);
        gold += gold * PartyPassivePercent(p => p.GoldPercent) / 100;
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
        foreach (var m in monsters.Where(m => Db.Npcs.ContainsKey(m.Id))) SetFlag(NpcBeatenFlag(m.Id));
        foreach (var m in monsters) UpdateQuests(ObjectiveType.Defeat, m.Id);
        return new BattleRewards(xp, gold, items, levelUps);
    }

    /// <summary>Défaite ; un entraînement perdu n'a aucune conséquence (on se relève, rien n'est perdu).</summary>
    public DefeatResult ApplyDefeat(Battle battle)
    {
        if (!battle.IsTraining) return ApplyDefeat();
        AdvanceTime(Db.Content.Time.BattleMinutes);
        EndTraining(battle);
        return new DefeatResult(false, 0, null);
    }

    public DefeatResult ApplyDefeat()
    {
        if (Config.Defeat == DefeatRule.GameOver) return new DefeatResult(true, 0, null);
        AdvanceTime(Db.Content.Time.BattleMinutes);
        var lost = State.Gold * Math.Clamp(Config.DefeatGoldLossPercent, 0, 100) / 100;
        State.Gold -= lost;
        State.CurrentLocationId = State.LastCityId;
        State.Dungeon = null; // vaincu : on est ramené hors du donjon
        HealAll();
        return new DefeatResult(false, lost, State.LastCityId);
    }

    /// <summary>Fuite ; arrêter un entraînement remet chacun en l'état d'avant.</summary>
    public void AfterFlee(Battle battle)
    {
        if (!battle.IsTraining) { AfterFlee(); return; }
        AdvanceTime(Db.Content.Time.BattleMinutes);
        EndTraining(battle);
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
