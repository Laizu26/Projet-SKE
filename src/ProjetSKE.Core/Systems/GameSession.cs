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
public sealed class GameSession
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

    public static GameSession NewGame(GameDatabase db, string heroId, Random? rng = null)
    {
        var state = new GameState
        {
            HeroId = heroId,
            Gold = db.Start.Gold,
            CurrentLocationId = db.Start.LocationId,
            LastCityId = db.Start.LocationId,
        };
        var session = new GameSession(db, state, rng);
        foreach (var stack in db.Start.Inventory) session.AddItem(stack.ItemId, stack.Count);
        session.Recruit(heroId);
        session.DiscoverLocation(state.CurrentLocationId);
        session.Notifications.Clear();
        return session;
    }

    /// <summary>
    /// Retire d'une sauvegarde tout ce qui n'existe plus dans le contenu (après modification dans l'éditeur),
    /// pour qu'une ancienne partie reste jouable.
    /// </summary>
    private void Sanitize()
    {
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

    public bool Check(Condition c) => c.Type switch
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
        ConditionType.LevelAtLeast => State.Party.Count > 0 && State.Party.Max(p => p.Level) >= c.Amount,
        _ => true,
    };

    public bool CheckAll(IEnumerable<Condition> conditions) => conditions.All(Check);

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
                Notifications.Add($"Obtenu : {a.Amount} or");
                break;
            case ActionType.TakeGold:
                State.Gold = Math.Max(0, State.Gold - a.Amount);
                Notifications.Add($"Payé : {a.Amount} or");
                break;
            case ActionType.GiveXp:
                foreach (var c in ActiveParty.ToList())
                    if (GiveXp(c, a.Amount) > 0) Notifications.Add($"{DefOf(c).Name} passe niveau {c.Level} !");
                Notifications.Add($"+{a.Amount} XP");
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
        }
        return null;
    }

    // ------------------------------------------------------------------ Quêtes

    public QuestStatus GetQuestStatus(string questId) =>
        State.Quests.TryGetValue(questId, out var p) ? p.Status : QuestStatus.NotStarted;

    public bool StartQuest(string questId)
    {
        if (!Db.Quests.TryGetValue(questId, out var quest) || State.Quests.ContainsKey(questId)) return false;
        State.Quests[questId] = new QuestProgress();
        Notifications.Add($"Nouvelle quête : {quest.Name}");
        UpdateQuests();
        return true;
    }

    /// <summary>Termine une quête (tous objectifs) et donne ses récompenses.</summary>
    public bool CompleteQuest(string questId)
    {
        if (!Db.Quests.TryGetValue(questId, out var quest)) return false;
        if (GetQuestStatus(questId) == QuestStatus.Completed) return false;
        State.Quests[questId] = new QuestProgress { Status = QuestStatus.Completed, Step = quest.Objectives.Count };
        Notifications.Add($"Quête terminée : {quest.Name}");
        foreach (var reward in quest.Rewards) Execute(reward);
        return true;
    }

    /// <summary>Oublie une quête (outil développeur).</summary>
    public void ResetQuest(string questId) => State.Quests.Remove(questId);

    public IEnumerable<(QuestDef Quest, QuestProgress Progress)> QuestLog =>
        State.Quests.Where(kv => Db.Quests.ContainsKey(kv.Key)).Select(kv => (Db.Quests[kv.Key], kv.Value));

    public string ObjectiveText(QuestObjective o)
    {
        if (o.Description.Length > 0) return o.Description;
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

    /// <summary>
    /// Fait avancer les quêtes actives. Appelé avec un événement (PNJ à qui l'on parle, monstre vaincu),
    /// ou sans événement pour vérifier les objectifs "aller à" et "posséder un objet".
    /// </summary>
    public void UpdateQuests(ObjectiveType? eventType = null, string? eventTarget = null)
    {
        foreach (var questId in State.Quests.Where(kv => kv.Value.Status == QuestStatus.Active).Select(kv => kv.Key).ToList())
        {
            if (!Db.Quests.TryGetValue(questId, out var quest)) continue;
            var progress = State.Quests[questId];
            var consumed = false; // un même événement ne valide qu'un objectif par quête
            while (progress.Status == QuestStatus.Active && progress.Step < quest.Objectives.Count)
            {
                var o = quest.Objectives[progress.Step];
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
                if (!done) break;
                if (o.Type is ObjectiveType.TalkTo || (o.Type is ObjectiveType.Bring && !string.IsNullOrEmpty(o.NpcId)))
                    consumed = true;
                progress.Step++;
                progress.Count = 0;
                if (progress.Step >= quest.Objectives.Count) CompleteQuest(questId);
                else Notifications.Add($"Objectif accompli : {ObjectiveText(o)}");
            }
        }
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
                .Select(c => (c.Name, c.Title, c.Description))
                .Concat(State.SeenNpcs.Select(id => Db.Npcs[id])
                    .Select(n => (n.Name, "PNJ · " + NameOrId(Db.Locations, n.LocationId, l => l.Name), n.Description))),
            EncyclopediaCategory.Monsters => State.SeenMonsters.Select(id => Db.Monsters[id])
                .Select(m => (m.Name, m.IsBoss ? "Boss" : $"PV {m.Stats.MaxHp} · ATQ {m.Stats.Attack} · DEF {m.Stats.Defense}", m.Description)),
            EncyclopediaCategory.Locations => State.SeenLocations.Select(id => Db.Locations[id])
                .Select(l => (l.Name, LocationTypeName(l.Type), l.Description)),
            EncyclopediaCategory.Weapons => State.SeenWeapons.Select(id => Db.Items[id])
                .Select(i => (i.Name, i.Bonus.ToBonusString(), i.Description)),
            _ => State.SeenRelics.Select(id => Db.Items[id])
                .Select(i => (i.Name, i.RelicUsage == RelicUsage.Quest ? "Objet de quête" : i.Bonus.ToBonusString(), i.Description)),
        };
        return entries.OrderBy(e => e.Item1).ToList();
    }

    public static string LocationTypeName(LocationType type) => type switch
    {
        LocationType.City => "Ville",
        LocationType.Dungeon => "Donjon",
        _ => "Nature",
    };

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
        return true;
    }

    public IEnumerable<NpcDef> VisibleNpcs => Db.NpcsAt(State.CurrentLocationId).Where(n => CheckAll(n.VisibleConditions));

    /// <summary>Parler à un PNJ : fait avancer les quêtes puis renvoie le dialogue à jouer (selon l'avancement).</summary>
    public string? Talk(string npcId)
    {
        if (!Db.Npcs.TryGetValue(npcId, out var npc)) return null;
        State.SeenNpcs.Add(npcId);
        UpdateQuests(ObjectiveType.TalkTo, npcId);
        var conditional = npc.ConditionalDialogues.FirstOrDefault(d => Db.Dialogues.ContainsKey(d.DialogueId) && CheckAll(d.Conditions));
        var id = conditional?.DialogueId ?? npc.DefaultDialogueId;
        return id is not null && Db.Dialogues.ContainsKey(id) ? id : null;
    }

    // ------------------------------------------------------------------ Carte et voyage

    public IReadOnlyList<LocationDef> Destinations =>
        CurrentLocation.ConnectedIds.Where(Db.Locations.ContainsKey).Select(id => Db.Locations[id]).ToList();

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
        if (!CurrentLocation.ConnectedIds.Contains(destinationId) || !Db.Locations.TryGetValue(destinationId, out var dest))
            return new TravelResult(false, "Ce lieu n'est pas accessible d'ici.");
        if (!CanEnter(dest))
            return new TravelResult(false, dest.LockedMessage.Length > 0 ? dest.LockedMessage : "Le passage est bloqué.");

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
    public IReadOnlyList<string>? Explore() => PickEncounter(CurrentLocation);

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
        var lost = State.Gold * Math.Clamp(Config.DefeatGoldLossPercent, 0, 100) / 100;
        State.Gold -= lost;
        State.CurrentLocationId = State.LastCityId;
        HealAll();
        return new DefeatResult(false, lost, State.LastCityId);
    }

    public void AfterFlee()
    {
        foreach (var c in ActiveParty.Where(c => c.CurrentHp <= 0)) c.CurrentHp = 1;
    }

    // ------------------------------------------------------------------ Dialogues

    public DialogueRunner StartDialogue(string dialogueId) => new(this, Db.Dialogues[dialogueId]);
}
