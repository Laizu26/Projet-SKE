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

/// <summary>Partie en cours : toutes les règles du jeu hors combat et dialogue.</summary>
public sealed class GameSession
{
    public GameDatabase Db { get; }
    public GameState State { get; }
    public Random Rng { get; }

    public GameSession(GameDatabase db, GameState state, Random? rng = null)
    {
        Db = db;
        State = state;
        Rng = rng ?? new Random();
    }

    public static GameSession NewGame(GameDatabase db, string heroId, Random? rng = null)
    {
        var state = new GameState
        {
            HeroId = heroId,
            Gold = db.StartGold,
            CurrentLocationId = db.StartLocationId,
            LastCityId = db.StartLocationId,
        };
        var session = new GameSession(db, state, rng);
        foreach (var (id, count) in db.StartInventory) session.AddItem(id, count);
        session.Recruit(heroId);
        session.DiscoverLocation(db.StartLocationId);
        return session;
    }

    public GameConfig Config => State.Config;
    public LocationDef CurrentLocation => Db.Locations[State.CurrentLocationId];
    public bool InCity => CurrentLocation.IsCity;
    public bool HasFlag(string flag) => State.Flags.Contains(flag);
    public void SetFlag(string flag) => State.Flags.Add(flag);

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
                .Select(c => (c.Name, c.Title, c.Description)),
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
        return stats with { MaxHp = Math.Max(1, stats.MaxHp), Speed = Math.Max(1, stats.Speed) };
    }

    public IReadOnlyList<SkillDef> GetSkills(CharacterState c) =>
        DefOf(c).Skills.Where(s => s.Level <= c.Level).Select(s => Db.Skills[s.SkillId]).ToList();

    public bool IsInParty(string characterId) => State.Party.Any(c => c.DefId == characterId);

    /// <summary>Ajoute un personnage à l'équipe (en réserve si les titulaires sont au complet).</summary>
    public bool Recruit(string characterId)
    {
        if (IsInParty(characterId) || !Db.Characters.TryGetValue(characterId, out var def)) return false;
        var c = new CharacterState
        {
            DefId = characterId,
            WeaponId = def.StartingWeaponId,
            ArmorId = def.StartingArmorId,
            IsActive = ActiveParty.Count() < Config.MaxActiveParty,
        };
        var stats = GetStats(c);
        c.CurrentHp = stats.MaxHp;
        c.CurrentMana = stats.MaxMana;
        State.Party.Add(c);
        SetFlag($"recruited:{characterId}");
        DiscoverCharacter(characterId);
        if (def.StartingWeaponId is { } w) DiscoverItem(w);
        return true;
    }

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

    public static int XpToNextLevel(int level) => 25 * level;

    /// <summary>Donne de l'XP. Renvoie le nombre de niveaux gagnés.</summary>
    public int GiveXp(CharacterState c, int xp)
    {
        var gained = 0;
        c.Xp += xp;
        while (c.Xp >= XpToNextLevel(c.Level))
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

    /// <summary>Possédé dans le sac ou équipé sur un personnage.</summary>
    public bool OwnsItem(string itemId) =>
        CountItem(itemId) > 0 || State.Party.Any(c => c.WeaponId == itemId || c.ArmorId == itemId || c.RelicId == itemId);

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

    // ------------------------------------------------------------------ Ville : boutique et auberge

    public IReadOnlyList<ItemDef> ShopStock =>
        InCity ? CurrentLocation.ShopItemIds.Select(id => Db.Items[id]).ToList() : [];

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
        State.Gold += item.SellPrice;
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

    public IEnumerable<NpcDef> VisibleNpcs =>
        CurrentLocation.Npcs.Where(n =>
            (n.HiddenIfFlag is null || !HasFlag(n.HiddenIfFlag)) &&
            (n.RequiresFlag is null || HasFlag(n.RequiresFlag)));

    // ------------------------------------------------------------------ Carte et voyage

    public IReadOnlyList<LocationDef> Destinations =>
        CurrentLocation.ConnectedIds.Select(id => Db.Locations[id]).ToList();

    public static string FixedBattleDoneFlag(string battleId) => $"battle_won:{battleId}";
    private static string FixedBattleSeenFlag(string battleId) => $"battle_seen:{battleId}";

    /// <summary>Combat fixe du lieu actuel, s'il n'a pas encore été gagné.</summary>
    public FixedBattleDef? PendingFixedBattle =>
        CurrentLocation.FixedBattle is { } fb && !HasFlag(FixedBattleDoneFlag(fb.Id)) ? fb : null;

    private bool FixedAllowed => Config.TravelEncounters is TravelEncounterMode.FixedOnly or TravelEncounterMode.Both;
    private bool RandomAllowed => Config.TravelEncounters is TravelEncounterMode.RandomOnly or TravelEncounterMode.Both;

    public TravelResult Travel(string destinationId)
    {
        if (!CurrentLocation.ConnectedIds.Contains(destinationId) || !Db.Locations.TryGetValue(destinationId, out var dest))
            return new TravelResult(false, "Ce lieu n'est pas accessible d'ici.");

        State.CurrentLocationId = destinationId;
        var firstVisit = State.SeenLocations.Add(destinationId);
        if (dest.IsCity) State.LastCityId = destinationId;

        // 1. Combat fixe : déclenché automatiquement à la première arrivée, rejouable ensuite depuis la carte.
        if (FixedAllowed && PendingFixedBattle is { } fb && !HasFlag(FixedBattleSeenFlag(fb.Id)))
        {
            SetFlag(FixedBattleSeenFlag(fb.Id));
            return new TravelResult(true, DialogueId: fb.IntroDialogueId, BattleMonsterIds: fb.MonsterIds, FixedBattleId: fb.Id);
        }

        // 2. Dialogue de première visite.
        if (firstVisit && dest.FirstVisitDialogueId is { } dialogueId)
            return new TravelResult(true, DialogueId: dialogueId);

        // 3. Rencontre aléatoire.
        if (RandomAllowed && RollEncounter(dest) is { } monsters)
            return new TravelResult(true, BattleMonsterIds: monsters);

        return new TravelResult(true);
    }

    /// <summary>Chercher un combat dans la zone actuelle (bouton "Explorer").</summary>
    public IReadOnlyList<string>? Explore() => PickEncounter(CurrentLocation);

    private IReadOnlyList<string>? RollEncounter(LocationDef loc) =>
        loc.RandomEncounters.Count > 0 && Rng.NextDouble() < loc.EncounterChance ? PickEncounter(loc) : null;

    private IReadOnlyList<string>? PickEncounter(LocationDef loc)
    {
        var total = loc.RandomEncounters.Sum(g => g.Weight);
        if (total <= 0) return null;
        var roll = Rng.Next(total);
        foreach (var g in loc.RandomEncounters)
        {
            if (roll < g.Weight) return g.MonsterIds;
            roll -= g.Weight;
        }
        return null;
    }

    // ------------------------------------------------------------------ Combat

    public Battle StartBattle(IReadOnlyList<string> monsterIds, string? fixedBattleId = null) =>
        new(this, monsterIds, fixedBattleId);

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
