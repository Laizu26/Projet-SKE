using System.Text.Json.Serialization;

namespace ProjetSKE.Core.Models;

// Contenu du jeu. Tout est modifiable (éditeur du mode développeur) et sérialisable en JSON.

public sealed class SkillDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public SkillKind Kind { get; set; } = SkillKind.Physical;
    public SkillTarget Target { get; set; } = SkillTarget.SingleEnemy;
    public int ManaCost { get; set; }
    /// <summary>Multiplicateur appliqué à la stat d'attaque ou de magie.</summary>
    public double Power { get; set; } = 1.0;
}

public sealed class ItemDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ItemType Type { get; set; }
    public int Price { get; set; }
    public StatBlock Bonus { get; set; } = new();
    public int HealHp { get; set; }
    public int HealMana { get; set; }
    /// <summary>Objet unique : on ne peut en posséder qu'un seul exemplaire.</summary>
    public bool IsUnique { get; set; }
    public RelicUsage RelicUsage { get; set; } = RelicUsage.Equipable;

    [JsonIgnore] public bool IsConsumable => Type == ItemType.Consumable;

    [JsonIgnore]
    public EquipSlot? Slot => Type switch
    {
        ItemType.Weapon => EquipSlot.Weapon,
        ItemType.Armor => EquipSlot.Armor,
        ItemType.Relic when RelicUsage == RelicUsage.Equipable => EquipSlot.Relic,
        _ => null,
    };

    [JsonIgnore] public bool IsEquipable => Slot is not null;

    [JsonIgnore]
    public bool IsSellable => Price > 0 && Type != ItemType.Quest
        && !(Type == ItemType.Relic && RelicUsage == RelicUsage.Quest);
}

public sealed class SkillUnlock
{
    public int Level { get; set; } = 1;
    public string SkillId { get; set; } = "";

    public SkillUnlock() { }
    public SkillUnlock(int level, string skillId) { Level = level; SkillId = skillId; }
}

/// <summary>Personnage jouable (PJ) : héros de départ ou compagnon recrutable.</summary>
public sealed class CharacterDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Classe / titre affiché, ex : "Chevalier errant".</summary>
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public StatBlock BaseStats { get; set; } = new();
    public StatBlock GrowthPerLevel { get; set; } = new();
    public List<SkillUnlock> Skills { get; set; } = [];
    public string? StartingWeaponId { get; set; }
    public string? StartingArmorId { get; set; }
    public string? StartingRelicId { get; set; }
    /// <summary>Proposé sur l'écran de sélection de départ.</summary>
    public bool IsStarter { get; set; }
    /// <summary>Karma de départ (vide = valeur par défaut des réglages de karma).</summary>
    public int? BaseKarma { get; set; }
    /// <summary>Amitié de départ envers les autres (vide = valeur par défaut des réglages d'amitié).</summary>
    public int? BaseFriendship { get; set; }
}

public sealed class ItemDrop
{
    public string ItemId { get; set; } = "";
    /// <summary>Probabilité de 0 à 1.</summary>
    public double Chance { get; set; } = 0.1;

    public ItemDrop() { }
    public ItemDrop(string itemId, double chance) { ItemId = itemId; Chance = chance; }
}

public sealed class MonsterDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public StatBlock Stats { get; set; } = new();
    public List<string> SkillIds { get; set; } = [];
    public int Xp { get; set; }
    public int Gold { get; set; }
    public List<ItemDrop> Drops { get; set; } = [];
    public bool IsBoss { get; set; }
}

public sealed class EncounterGroup
{
    public List<string> MonsterIds { get; set; } = [];
    public int Weight { get; set; } = 1;

    public EncounterGroup() { }
    public EncounterGroup(List<string> monsterIds, int weight = 1) { MonsterIds = monsterIds; Weight = weight; }
}

/// <summary>Combat placé à un endroit précis, joué une seule fois.</summary>
public sealed class FixedBattleDef
{
    public string Id { get; set; } = "";
    public List<string> MonsterIds { get; set; } = [];
    public string? IntroDialogueId { get; set; }

    public FixedBattleDef() { }
    public FixedBattleDef(string id, List<string> monsterIds, string? introDialogueId = null)
    {
        Id = id;
        MonsterIds = monsterIds;
        IntroDialogueId = introDialogueId;
    }
}

public sealed class LocationDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public LocationType Type { get; set; }
    public List<string> ConnectedIds { get; set; } = [];
    public List<string> ShopItemIds { get; set; } = [];
    public int InnPrice { get; set; } = 10;
    public List<EncounterGroup> RandomEncounters { get; set; } = [];
    /// <summary>Probabilité (0 à 1) d'une rencontre aléatoire en arrivant ici.</summary>
    public double EncounterChance { get; set; }
    public FixedBattleDef? FixedBattle { get; set; }
    public string? FirstVisitDialogueId { get; set; }
    /// <summary>Conditions pour pouvoir s'y rendre (lieu bloqué sinon).</summary>
    public List<Condition> AccessConditions { get; set; } = [];
    public string LockedMessage { get; set; } = "";
    /// <summary>Position sur la carte hexagonale du royaume (vide = placement automatique).</summary>
    public int? HexQ { get; set; }
    public int? HexR { get; set; }
    /// <summary>Le lieu n'apparaît sur la carte que si ces conditions sont remplies (lieu secret, découvert plus tard).</summary>
    public List<Condition> VisibleConditions { get; set; } = [];
    /// <summary>Durée du voyage pour venir ici, en minutes (vide = durée par défaut des réglages du temps).</summary>
    public int? TravelMinutes { get; set; }

    [JsonIgnore] public bool IsCity => Type == LocationType.City;
}

/// <summary>Personnage non joueur : un habitant placé dans un lieu, avec ses dialogues.</summary>
public sealed class NpcDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string LocationId { get; set; } = "";
    /// <summary>Emplois du temps / déplacements : le premier placement dont les conditions passent l'emporte sur le lieu habituel.</summary>
    public List<NpcPlacement> Placements { get; set; } = [];
    /// <summary>Amitié de départ envers l'équipe (vide = valeur par défaut des réglages d'amitié).</summary>
    public int? BaseFriendship { get; set; }
    /// <summary>Le PNJ n'apparaît que si ces conditions sont remplies.</summary>
    public List<Condition> VisibleConditions { get; set; } = [];
    /// <summary>Dialogues selon l'avancement : le premier dont les conditions passent est joué.</summary>
    public List<NpcDialogue> ConditionalDialogues { get; set; } = [];
    public string? DefaultDialogueId { get; set; }
}

/// <summary>Le PNJ se trouve à ce lieu quand les conditions sont remplies (ex : la nuit, à l'auberge).</summary>
public sealed class NpcPlacement
{
    public string LocationId { get; set; } = "";
    public List<Condition> Conditions { get; set; } = [];
}

public sealed class NpcDialogue
{
    public string DialogueId { get; set; } = "";
    public List<Condition> Conditions { get; set; } = [];

    public NpcDialogue() { }
    public NpcDialogue(string dialogueId, params Condition[] conditions) { DialogueId = dialogueId; Conditions = [.. conditions]; }
}

/// <summary>
/// Une condition (« si »). Elle peut être inversée (« sauf si »), comparer une valeur (variable, karma, amitié...)
/// ou regrouper d'autres conditions (« au moins une de » / « toutes »), pour construire n'importe quelle logique.
/// </summary>
public sealed class Condition
{
    public ConditionType Type { get; set; }
    /// <summary>Identifiant visé (flag, quête, objet, PJ, variable, lieu, nom de période...).</summary>
    public string Arg { get; set; } = "";
    /// <summary>Second identifiant : pour l'amitié, envers qui (« @equipe », « @parle » ou un PJ).</summary>
    public string Arg2 { get; set; } = "";
    public int Amount { get; set; } = 1;
    /// <summary>Seconde valeur : heure de fin pour « entre deux heures ».</summary>
    public int Amount2 { get; set; }
    public CompareOp Op { get; set; } = CompareOp.AtLeast;
    /// <summary>Inverse la condition (« sauf si »).</summary>
    public bool Negate { get; set; }
    /// <summary>Sous-conditions des groupes « au moins une de » et « toutes ».</summary>
    public List<Condition>? Children { get; set; }

    public Condition() { }
    public Condition(ConditionType type, string arg = "", int amount = 1) { Type = type; Arg = arg; Amount = amount; }
}

public sealed class GameAction
{
    public ActionType Type { get; set; }
    /// <summary>Identifiant visé (flag, objet, personnage, quête, lieu, ou monstres séparés par des virgules).</summary>
    public string Arg { get; set; } = "";
    /// <summary>Second argument : envers qui (amitié), lieu (déplacer un PNJ), texte (message).</summary>
    public string Arg2 { get; set; } = "";
    public int Amount { get; set; } = 1;

    public GameAction() { }
    public GameAction(ActionType type, string arg = "", int amount = 1) { Type = type; Arg = arg; Amount = amount; }
}

public sealed class DialogueChoice
{
    public string Text { get; set; } = "";
    public string? NextId { get; set; }
    public List<GameAction> Actions { get; set; } = [];
    /// <summary>Le choix n'est proposé que si ces conditions sont remplies.</summary>
    public List<Condition> Conditions { get; set; } = [];
    /// <summary>Si les conditions ne sont pas remplies : afficher le choix grisé (au lieu de le cacher).</summary>
    public bool ShowLocked { get; set; }
    /// <summary>Texte affiché sous un choix grisé (ex : « Karma trop bas »).</summary>
    public string LockedText { get; set; } = "";
}

/// <summary>Aiguillage : après la réplique, va à NextId si les conditions sont remplies (le premier qui passe gagne).</summary>
public sealed class DialogueBranch
{
    public List<Condition> Conditions { get; set; } = [];
    /// <summary>Réplique visée : « etiquette », « dialogue:etiquette » (autre dialogue) ou vide (fin).</summary>
    public string? NextId { get; set; }
}

/// <summary>Autre version d'une réplique, jouée à la place si les conditions sont remplies.</summary>
public sealed class TextVariant
{
    public List<Condition> Conditions { get; set; } = [];
    /// <summary>Vide = même personnage que la réplique d'origine.</summary>
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class DialogueNode
{
    public string Id { get; set; } = "";
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>
    /// Nœud suivant quand il n'y a pas de choix. Null = fin du dialogue.
    /// « dialogue:etiquette » continue dans un autre dialogue (les histoires peuvent se croiser).
    /// </summary>
    public string? NextId { get; set; }
    public List<DialogueChoice> Choices { get; set; } = [];
    public List<GameAction> Actions { get; set; } = [];
    /// <summary>Aiguillages testés avant NextId (le premier qui passe gagne).</summary>
    public List<DialogueBranch> Branches { get; set; } = [];
    /// <summary>Versions alternatives du texte selon la situation (karma, amitié, qui parle, heure...).</summary>
    public List<TextVariant> Variants { get; set; } = [];
}

public sealed class DialogueDef
{
    public string Id { get; set; } = "";
    /// <summary>Nom lisible dans l'éditeur.</summary>
    public string Name { get; set; } = "";
    public List<DialogueNode> Nodes { get; set; } = [];

    [JsonIgnore] public string? StartId => Nodes.Count > 0 ? Nodes[0].Id : null;
}

public sealed class QuestObjective
{
    public ObjectiveType Type { get; set; }
    /// <summary>PNJ (parler), monstre (vaincre), lieu (aller) ou objet (apporter).</summary>
    public string TargetId { get; set; } = "";
    public int Count { get; set; } = 1;
    /// <summary>Apporter : PNJ à qui remettre l'objet (vide = il suffit de le posséder).</summary>
    public string? NpcId { get; set; }
    /// <summary>Apporter : l'objet est retiré du sac une fois remis.</summary>
    public bool ConsumeItems { get; set; } = true;
    /// <summary>Texte affiché au joueur (sinon généré automatiquement).</summary>
    public string Description { get; set; } = "";
}

/// <summary>Quête : objectifs à accomplir dans l'ordre, puis récompenses.</summary>
public sealed class QuestDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<QuestObjective> Objectives { get; set; } = [];
    public List<GameAction> Rewards { get; set; } = [];
    /// <summary>Quête secrète : n'apparaît pas dans le journal des quêtes.</summary>
    public bool Hidden { get; set; }
}

public sealed class ItemStack
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; } = 1;

    public ItemStack() { }
    public ItemStack(string itemId, int count) { ItemId = itemId; Count = count; }
}

public sealed class StartSettings
{
    public string LocationId { get; set; } = "";
    public int Gold { get; set; } = 100;
    public List<ItemStack> Inventory { get; set; } = [];
    public string? IntroDialogueId { get; set; }
}

/// <summary>Formules d'équilibrage.</summary>
public sealed class BalanceSettings
{
    /// <summary>XP pour passer au niveau suivant = XpPerLevel × niveau actuel.</summary>
    public int XpPerLevel { get; set; } = 25;
    public int MaxLevel { get; set; } = 99;
    /// <summary>Prix de revente en % du prix d'achat.</summary>
    public int SellPercent { get; set; } = 50;
    /// <summary>Dégâts physiques = ATQ × puissance − DEF × ce facteur.</summary>
    public double PhysicalDefenseFactor { get; set; } = 0.5;
    /// <summary>Dégâts magiques = MAG × puissance × ce multiplicateur − DEF × facteur magique.</summary>
    public double MagicMultiplier { get; set; } = 1.2;
    public double MagicDefenseFactor { get; set; } = 0.25;
    /// <summary>Soin = MAG × puissance × ce multiplicateur + bonus fixe.</summary>
    public double HealMultiplier { get; set; } = 2.0;
    public int HealFlat { get; set; } = 5;
    /// <summary>Variation aléatoire des dégâts en % (10 = entre 90 % et 110 %).</summary>
    public int DamageVariancePercent { get; set; } = 10;
}

/// <summary>Variable libre du scénario (réputation, dette, compteur...), modifiable par les effets et testable par les conditions.</summary>
public sealed class VariableDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Initial { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
    /// <summary>Affichée au joueur dans le campement.</summary>
    public bool Visible { get; set; }
}

/// <summary>Palier nommé d'une échelle (ex : karma ≥ 50 → « Héroïque »).</summary>
public sealed class ScaleTier
{
    public string Name { get; set; } = "";
    public int Min { get; set; }

    public ScaleTier() { }
    public ScaleTier(string name, int min) { Name = name; Min = min; }
}

/// <summary>Réglages d'une échelle de valeur (karma, amitié).</summary>
public sealed class ScaleSettings
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public int Default { get; set; }
    public int Min { get; set; } = -100;
    public int Max { get; set; } = 100;
    /// <summary>Afficher la valeur au joueur (sinon elle reste cachée mais agit quand même).</summary>
    public bool Visible { get; set; } = true;
    public List<ScaleTier> Tiers { get; set; } = [];

    /// <summary>Nom du palier atteint par une valeur (le plus haut dont le minimum est atteint).</summary>
    public string TierName(int value) =>
        Tiers.Where(t => value >= t.Min).OrderByDescending(t => t.Min).FirstOrDefault()?.Name ?? "";
}

/// <summary>Moment de la journée (« Aube » à partir de 5 h...).</summary>
public sealed class DayPeriod
{
    public string Name { get; set; } = "";
    public int FromHour { get; set; }

    public DayPeriod() { }
    public DayPeriod(string name, int fromHour) { Name = name; FromHour = fromHour; }
}

/// <summary>Échelle de temps et calendrier du monde.</summary>
public sealed class TimeSettings
{
    public bool Enabled { get; set; } = true;
    public int HoursPerDay { get; set; } = 24;
    public int DaysPerMonth { get; set; } = 30;
    /// <summary>Noms des jours de la semaine (vide = pas de semaine). La longueur fixe la durée de la semaine.</summary>
    public List<string> WeekDays { get; set; } = ["Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"];
    /// <summary>Noms des mois (vide = pas de mois). La longueur fixe la durée de l'année.</summary>
    public List<string> Months { get; set; } =
        ["Givrelune", "Neigelune", "Pluielune", "Germelune", "Fleurlune", "Soleillune",
         "Moissonlune", "Blondelune", "Vendangelune", "Brumelune", "Ventlune", "Sombrelune"];
    public List<DayPeriod> Periods { get; set; } =
        [new("Nuit", 0), new("Aube", 5), new("Matin", 8), new("Après-midi", 12), new("Crépuscule", 18), new("Nuit", 21)];
    /// <summary>Libellé de l'année (ex : « An »), et année de départ.</summary>
    public string YearLabel { get; set; } = "An";
    public int StartYear { get; set; } = 1;
    /// <summary>Jour de départ (1 = premier jour du calendrier) et heure de départ.</summary>
    public int StartDay { get; set; } = 1;
    public int StartHour { get; set; } = 8;
    /// <summary>Durées en minutes.</summary>
    public int TravelMinutes { get; set; } = 240;
    public int ExploreMinutes { get; set; } = 60;
    public int BattleMinutes { get; set; } = 20;
    public int TalkMinutes { get; set; } = 10;
    /// <summary>Heure du réveil après une nuit à l'auberge.</summary>
    public int InnWakeHour { get; set; } = 7;
}

/// <summary>Le monde : noms, monnaie et tout le vocabulaire affiché (rien n'est figé).</summary>
public sealed class WorldSettings
{
    public string CountryName { get; set; } = "Royaume";
    /// <summary>Quand l'équipe compte plusieurs PJ : demander lequel parle au PNJ.</summary>
    public bool AskSpeaker { get; set; } = true;
    /// <summary>Textes de l'interface remplacés (clé → texte). Voir <see cref="Data.Vocabulary"/>.</summary>
    public Dictionary<string, string> Texts { get; set; } = [];
}

/// <summary>Tout le contenu d'un jeu : c'est ce fichier que l'éditeur exporte.</summary>
public sealed class GameContent
{
    public int Version { get; set; } = 1;
    public string Title { get; set; } = "Projet SKE";
    public List<SkillDef> Skills { get; set; } = [];
    public List<ItemDef> Items { get; set; } = [];
    public List<CharacterDef> Characters { get; set; } = [];
    public List<MonsterDef> Monsters { get; set; } = [];
    public List<LocationDef> Locations { get; set; } = [];
    public List<NpcDef> Npcs { get; set; } = [];
    public List<DialogueDef> Dialogues { get; set; } = [];
    public List<QuestDef> Quests { get; set; } = [];
    public StartSettings Start { get; set; } = new();
    public BalanceSettings Balance { get; set; } = new();
    public WorldSettings World { get; set; } = new();
    public TimeSettings Time { get; set; } = new();
    public ScaleSettings Karma { get; set; } = new()
    {
        Name = "Karma",
        Tiers = [new("Infâme", -100), new("Mauvais", -50), new("Douteux", -15), new("Neutre", -14), new("Bon", 15), new("Vertueux", 50), new("Héroïque", 90)],
    };
    public ScaleSettings Friendship { get; set; } = new()
    {
        Name = "Amitié",
        Tiers = [new("Ennemi", -100), new("Hostile", -50), new("Méfiant", -15), new("Neutre", -14), new("Amical", 15), new("Ami", 50), new("Inséparable", 90)],
    };
    public List<VariableDef> Variables { get; set; } = [];
}
