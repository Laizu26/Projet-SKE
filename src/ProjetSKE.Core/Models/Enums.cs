namespace ProjetSKE.Core.Models;

public enum SkillKind { Physical, Magical, Heal }

public enum SkillTarget { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self }

public enum ItemType { Consumable, Weapon, Armor, Relic, Quest }

/// <summary>Une relique est un objet unique : soit équipable (bonus passif), soit objet de quête.</summary>
public enum RelicUsage { Equipable, Quest }

public enum EquipSlot { Weapon, Armor, Relic }

public enum LocationType { City, Wild, Dungeon }

public enum EncyclopediaCategory { Characters, Monsters, Locations, Weapons, Relics }

/// <summary>Effets déclenchés par un dialogue ou une récompense de quête.</summary>
public enum ActionType
{
    SetFlag, ClearFlag, Recruit, GiveItem, TakeItem, GiveGold, TakeGold, GiveXp,
    StartBattle, StartQuest, CompleteQuest, HealParty, Teleport,
}

/// <summary>Conditions (affichage d'un PNJ, choix de dialogue, accès à un lieu...).</summary>
public enum ConditionType
{
    FlagSet, FlagNotSet, QuestNotStarted, QuestActive, QuestCompleted,
    HasItem, InParty, NotInParty, GoldAtLeast, LevelAtLeast,
}

public enum ObjectiveType { TalkTo, Defeat, Reach, Bring }

public enum QuestStatus { NotStarted, Active, Completed }

/// <summary>Quels combats peuvent se déclencher en voyageant.</summary>
public enum TravelEncounterMode { None, RandomOnly, FixedOnly, Both }

/// <summary>Conséquence d'une défaite.</summary>
public enum DefeatRule { ReturnToLastCity, GameOver }

/// <summary>Règle de fuite (les boss empêchent toujours la fuite).</summary>
public enum FleeRule { AlwaysSucceed, SpeedBased, Never }
