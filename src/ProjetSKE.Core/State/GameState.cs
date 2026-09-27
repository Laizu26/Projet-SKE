using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.State;

// Tout ce qui est sauvegardé. Propriétés publiques get/set pour la sérialisation JSON.

public sealed class GameConfig
{
    public TravelEncounterMode TravelEncounters { get; set; } = TravelEncounterMode.Both;
    public DefeatRule Defeat { get; set; } = DefeatRule.ReturnToLastCity;
    /// <summary>Pourcentage d'or perdu en cas de défaite (règle ReturnToLastCity).</summary>
    public int DefeatGoldLossPercent { get; set; } = 20;
    public FleeRule Flee { get; set; } = FleeRule.SpeedBased;
    /// <summary>Nombre maximum de personnages titulaires (qui combattent).</summary>
    public int MaxActiveParty { get; set; } = 4;
}

public sealed class CharacterState
{
    public string DefId { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int CurrentHp { get; set; }
    public int CurrentMana { get; set; }
    /// <summary>Titulaire (combat) ou réserve (reste au campement).</summary>
    public bool IsActive { get; set; } = true;
    public string? WeaponId { get; set; }
    public string? ArmorId { get; set; }
    public string? RelicId { get; set; }

    public string? GetEquipped(EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => WeaponId,
        EquipSlot.Armor => ArmorId,
        _ => RelicId,
    };

    public void SetEquipped(EquipSlot slot, string? itemId)
    {
        switch (slot)
        {
            case EquipSlot.Weapon: WeaponId = itemId; break;
            case EquipSlot.Armor: ArmorId = itemId; break;
            default: RelicId = itemId; break;
        }
    }
}

public sealed class GameState
{
    public int Version { get; set; } = 1;
    public string HeroId { get; set; } = "";
    public List<CharacterState> Party { get; set; } = [];
    /// <summary>Sac commun : id d'objet → quantité.</summary>
    public Dictionary<string, int> Inventory { get; set; } = [];
    public int Gold { get; set; }
    public string CurrentLocationId { get; set; } = "";
    public string LastCityId { get; set; } = "";
    public HashSet<string> Flags { get; set; } = [];
    public HashSet<string> SeenCharacters { get; set; } = [];
    public HashSet<string> SeenMonsters { get; set; } = [];
    public HashSet<string> SeenLocations { get; set; } = [];
    public HashSet<string> SeenWeapons { get; set; } = [];
    public HashSet<string> SeenRelics { get; set; } = [];
    public string Journal { get; set; } = "";
    public GameConfig Config { get; set; } = new();
    public DateTime SavedAt { get; set; }
}
