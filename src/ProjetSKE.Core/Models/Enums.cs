namespace ProjetSKE.Core.Models;

/// <summary>Nature d'une compétence : dégâts (ATQ ou MAG), soin, effets seuls, ou résurrection d'un allié K.O.</summary>
public enum SkillKind { Physical, Magical, Heal, Status, Revive }

/// <summary>Effet durable posé par une compétence.</summary>
public enum EffectType { Poison, Regen, Stun, StatUp, StatDown, Shield, Cleanse }

/// <summary>Statistique modifiée par un bonus / malus.</summary>
public enum StatKind { Attack, Defense, Magic, Speed }

public enum SkillTarget { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self }

public enum ItemType { Consumable, Weapon, Armor, Relic, Quest }

/// <summary>Une relique est un objet unique : soit équipable (bonus passif), soit objet de quête.</summary>
public enum RelicUsage { Equipable, Quest }

/// <summary>Emplacements d'équipement. Armor = le corps (torse). Les nouveaux emplacements sont ajoutés à la fin (sauvegardes).</summary>
public enum EquipSlot { Weapon, Armor, Relic, Head, Hands, Legs, Feet, Accessory, Shield }

public enum LocationType { City, Wild, Dungeon }

public enum EncyclopediaCategory { Characters, Monsters, Locations, Weapons, Relics }

/// <summary>Effets déclenchés par un dialogue ou une récompense de quête.</summary>
public enum ActionType
{
    SetFlag, ClearFlag, Recruit, GiveItem, TakeItem, GiveGold, TakeGold, GiveXp,
    StartBattle, StartQuest, CompleteQuest, HealParty, Teleport,
    // Variables, karma, amitié
    SetVariable, AddVariable, AddKarma, SetKarma, AddFriendship, SetFriendship,
    // Temps
    AdvanceTime, WaitUntilHour,
    // Divers
    ShowMessage, LeaveParty, MoveNpc, RevealLocation, HideLocation,
    // Campement
    JoinCamp, LeaveCamp, SetCampRank, SetCampTask, AddCampResource, BuildCampBuilding,
    // Quêtes à étapes
    SetQuestStage, FailQuest,
}

/// <summary>Conditions (affichage d'un PNJ, choix de dialogue, accès à un lieu...).</summary>
public enum ConditionType
{
    FlagSet, FlagNotSet, QuestNotStarted, QuestActive, QuestCompleted,
    HasItem, InParty, NotInParty, GoldAtLeast, LevelAtLeast,
    // Valeurs
    Variable, Karma, Friendship, Gold, Level, PartySize,
    // Qui parle, qui est incarné (le héros choisi au départ)
    Speaker, IsHero,
    // Temps
    HourBetween, Day, Period, WeekDay, Month,
    // Monde
    AtLocation, Visited, MetNpc, Chance,
    // Campement
    CampMember, CampRank, CampTask, CampResource, CampBuilt,
    // Quêtes à étapes
    QuestAtStage, QuestStageReached, QuestEnding, QuestFailed,
    // Groupes de conditions
    AnyOf, AllOf,
}

/// <summary>Comparaison d'une valeur (variable, karma, amitié, jour...).</summary>
public enum CompareOp { AtLeast, AtMost, Equal, NotEqual, Greater, Less }

public enum ObjectiveType { TalkTo, Defeat, Reach, Bring }

public enum QuestStatus { NotStarted, Active, Completed, Failed }

/// <summary>Quels combats peuvent se déclencher en voyageant.</summary>
public enum TravelEncounterMode { None, RandomOnly, FixedOnly, Both }

/// <summary>Conséquence d'une défaite.</summary>
public enum DefeatRule { ReturnToLastCity, GameOver }

/// <summary>Règle de fuite (les boss empêchent toujours la fuite).</summary>
public enum FleeRule { AlwaysSucceed, SpeedBased, Never }

/// <summary>Moment où une réplique de combat est dite (du point de vue de celui qui parle).</summary>
public enum BattleTrigger
{
    /// <summary>Au début du combat.</summary>
    Start,
    /// <summary>Au début du tour n° Quantité.</summary>
    Turn,
    /// <summary>Quand ses PV passent sous Quantité % (une fois).</summary>
    HpBelow,
    /// <summary>Quand il est mis K.O.</summary>
    Down,
    /// <summary>Quand il met un adversaire K.O.</summary>
    Kill,
    /// <summary>Quand l'équipe gagne.</summary>
    Victory,
    /// <summary>Quand l'équipe perd.</summary>
    Defeat,
}
