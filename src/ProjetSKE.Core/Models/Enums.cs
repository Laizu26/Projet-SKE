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
    // Quêtes en plusieurs parties
    StartQuestPart, CompleteQuestPart, FailQuestPart,
    // Histoire : jouer un dialogue (depuis une quête, un départ, un combat...)
    StartDialogue,
    // Interface : débloquer / verrouiller un onglet ou une commande ; finir le prologue
    UnlockFeature, LockFeature, EndTutorial,
    // Jauges de personnage (folie...) : comme le karma
    AddGauge, SetGauge,
    // Passifs : donner / retirer à un personnage
    GivePassive, RemovePassive,
    // Pouvoirs : donner / retirer (le PJ apprend toutes les compétences du pouvoir)
    GivePower, RemovePower,
}

/// <summary>
/// Parties de l'interface qu'on peut verrouiller puis débloquer petit à petit (prologue, ou n'importe quand).
/// Les nouveaux éléments sont ajoutés à la fin (sauvegardes).
/// </summary>
public enum UiFeature
{
    // Onglets
    TabCamp, TabMap, TabQuests, TabEncyclopedia, TabShop, TabJournal, TabMenu,
    // Carte
    WorldMap, Explore,
    // Combat
    BattleSkills, BattleItems, BattleFlee, BattleDefend,
    // Menus du camp
    CampManagement, CampResources, CampPeople, CampTeam, CampBag, CampPlaces,
}

/// <summary>Noms courts des parties de l'interface (mode texte : « [debloquer carte] »), en plus des noms anglais.</summary>
public static class UiFeatures
{
    public static readonly (string Word, UiFeature Feature)[] Words =
    [
        ("camp", UiFeature.TabCamp), ("carte", UiFeature.TabMap), ("quetes", UiFeature.TabQuests),
        ("encyclopedie", UiFeature.TabEncyclopedia), ("boutique", UiFeature.TabShop), ("journal", UiFeature.TabJournal),
        ("menu", UiFeature.TabMenu), ("royaume", UiFeature.WorldMap), ("explorer", UiFeature.Explore),
        ("competences", UiFeature.BattleSkills), ("objets", UiFeature.BattleItems), ("fuite", UiFeature.BattleFlee),
        ("defense", UiFeature.BattleDefend), ("gestion", UiFeature.CampManagement), ("ressources", UiFeature.CampResources),
        ("persos", UiFeature.CampPeople), ("equipe", UiFeature.CampTeam), ("sac", UiFeature.CampBag), ("lieux", UiFeature.CampPlaces),
    ];

    /// <summary>Lit une partie de l'interface : nom court (« carte ») ou nom anglais (« TabMap »).</summary>
    public static bool TryParse(string? text, out UiFeature feature)
    {
        var t = (text ?? "").Trim();
        foreach (var (word, f) in Words)
            if (string.Equals(word, t, StringComparison.OrdinalIgnoreCase)) { feature = f; return true; }
        return Enum.TryParse(t, ignoreCase: true, out feature) && Enum.IsDefined(feature);
    }
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
    // Choix déjà fait dans un dialogue
    ChoiceMade,
    // Temps
    HourBetween, Day, Period, WeekDay, Month,
    // Monde
    AtLocation, Visited, MetNpc, Chance,
    // Campement
    CampMember, CampRank, CampTask, CampResource, CampBuilt,
    // Quêtes à étapes
    QuestAtStage, QuestStageReached, QuestEnding, QuestFailed,
    // Quêtes en plusieurs parties
    QuestPartNotStarted, QuestPartActive, QuestPartCompleted, QuestPartFailed,
    // Groupes de conditions
    AnyOf, AllOf,
    // Jauges de personnage (folie...)
    Gauge,
    // A un passif (actif)
    HasPassive,
    // A un pouvoir
    HasPower,
}

/// <summary>Affichage d'un dialogue : classique (boîte en bas, le jeu visible derrière) ou cinématique (plein écran noir).</summary>
public enum DialogueStyle { Classic, Cinematic }

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
