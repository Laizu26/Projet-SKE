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
    public string? HeadId { get; set; }
    public string? HandsId { get; set; }
    public string? LegsId { get; set; }
    public string? FeetId { get; set; }
    public string? AccessoryId { get; set; }
    public string? ShieldId { get; set; }
    /// <summary>Karma propre à ce personnage (évolue avec ses choix).</summary>
    public int Karma { get; set; }
    /// <summary>Jauges propres à ce personnage (folie...) : id de jauge → valeur.</summary>
    public Dictionary<string, int> Gauges { get; set; } = [];
    /// <summary>Passifs donnés / retirés en cours de partie (en plus / à la place de ceux de sa fiche).</summary>
    public HashSet<string> GainedPassives { get; set; } = [];
    public HashSet<string> LostPassives { get; set; } = [];
    /// <summary>Pouvoirs donnés / retirés en cours de partie.</summary>
    public HashSet<string> GainedPowers { get; set; } = [];
    public HashSet<string> LostPowers { get; set; } = [];

    public string? GetEquipped(EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => WeaponId,
        EquipSlot.Armor => ArmorId,
        EquipSlot.Head => HeadId,
        EquipSlot.Hands => HandsId,
        EquipSlot.Legs => LegsId,
        EquipSlot.Feet => FeetId,
        EquipSlot.Accessory => AccessoryId,
        EquipSlot.Shield => ShieldId,
        _ => RelicId,
    };

    public void SetEquipped(EquipSlot slot, string? itemId)
    {
        switch (slot)
        {
            case EquipSlot.Weapon: WeaponId = itemId; break;
            case EquipSlot.Armor: ArmorId = itemId; break;
            case EquipSlot.Head: HeadId = itemId; break;
            case EquipSlot.Hands: HandsId = itemId; break;
            case EquipSlot.Legs: LegsId = itemId; break;
            case EquipSlot.Feet: FeetId = itemId; break;
            case EquipSlot.Accessory: AccessoryId = itemId; break;
            case EquipSlot.Shield: ShieldId = itemId; break;
            default: RelicId = itemId; break;
        }
    }
}

public sealed class QuestProgress
{
    public QuestStatus Status { get; set; } = QuestStatus.Active;
    /// <summary>Index de l'objectif en cours (les objectifs se font dans l'ordre).</summary>
    public int Step { get; set; }
    /// <summary>Compteur de l'objectif en cours (ex : monstres vaincus).</summary>
    public int Count { get; set; }
    /// <summary>Quête à étapes : étape en cours, étapes traversées, et fin obtenue.</summary>
    public string? StageId { get; set; }
    public List<string> Path { get; set; } = [];
    public string? EndingId { get; set; }
    /// <summary>Quête en plusieurs parties : état de chaque partie (id de partie → progression).</summary>
    public Dictionary<string, QuestProgress> Parts { get; set; } = [];
}

/// <summary>Un membre du campement (PNJ ou PJ).</summary>
public sealed class CampMemberState
{
    public string Id { get; set; } = "";
    public string RankId { get; set; } = "";
    /// <summary>Tâche en cours (vide = au repos).</summary>
    public string? TaskId { get; set; }
    /// <summary>Moment (minutes) où le cycle en cours se termine.</summary>
    public long NextAt { get; set; }
}

public sealed class GameState
{
    /// <summary>Version du format : 2 = temps, karma, amitié, variables.</summary>
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;
    public string HeroId { get; set; } = "";
    /// <summary>Départ choisi en début de partie.</summary>
    public string StartId { get; set; } = "principal";
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
    public HashSet<string> SeenNpcs { get; set; } = [];
    /// <summary>Avancement des quêtes commencées : id de quête → progression.</summary>
    public Dictionary<string, QuestProgress> Quests { get; set; } = [];
    public string Journal { get; set; } = "";
    /// <summary>Temps écoulé depuis le début du calendrier, en minutes.</summary>
    public long Minutes { get; set; }
    /// <summary>Variables du scénario : id → valeur.</summary>
    public Dictionary<string, int> Variables { get; set; } = [];
    /// <summary>Amitiés : « qui>envers qui » → valeur (voir GameSession.GetFriendship).</summary>
    public Dictionary<string, int> Relations { get; set; } = [];
    /// <summary>PJ qui parle aux PNJ (vide = le héros).</summary>
    public string? SpeakerId { get; set; }
    /// <summary>PNJ déplacés par un effet : id du PNJ → lieu.</summary>
    public Dictionary<string, string> NpcLocations { get; set; } = [];
    /// <summary>Lieux révélés ou cachés par un effet (prioritaires sur leurs conditions de visibilité).</summary>
    public HashSet<string> RevealedLocations { get; set; } = [];
    public HashSet<string> HiddenLocations { get; set; } = [];
    /// <summary>PNJ montrés ou cachés par un effet (prioritaires sur « caché au début » et leurs conditions).</summary>
    public HashSet<string> ShownNpcs { get; set; } = [];
    public HashSet<string> HiddenNpcs { get; set; } = [];
    /// <summary>Partie de prologue (tutoriel) : jamais sauvegardée, finie par l'effet « Prologue : terminer ».</summary>
    public bool IsTutorial { get; set; }
    public bool TutorialDone { get; set; }
    /// <summary>Parties de l'interface verrouillées (onglets, carte du royaume, fuite...).</summary>
    public HashSet<UiFeature> LockedFeatures { get; set; } = [];
    /// <summary>Choix faits dans les dialogues : « dialogue:réplique:choix ».</summary>
    public HashSet<string> Choices { get; set; } = [];
    public List<CampMemberState> Camp { get; set; } = [];
    /// <summary>Stocks du camp : id de ressource → quantité.</summary>
    public Dictionary<string, int> CampResources { get; set; } = [];
    public HashSet<string> CampBuildings { get; set; } = [];
    /// <summary>Dernier jour où la consommation quotidienne a été comptée.</summary>
    public int CampLastDay { get; set; }
    /// <summary>Journal du campement (derniers événements, le plus récent à la fin).</summary>
    public List<string> CampLog { get; set; } = [];
    public GameConfig Config { get; set; } = new();
    public DateTime SavedAt { get; set; }
}
