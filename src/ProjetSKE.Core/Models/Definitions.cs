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
    /// <summary>Montant fixe ajouté (dégâts, soin, PV rendus à la résurrection).</summary>
    public int FlatAmount { get; set; }
    /// <summary>Élément (feu, glace, sacré... texte libre) : les faiblesses et résistances des cibles s'appliquent.</summary>
    public string Element { get; set; } = "";
    /// <summary>Nombre de coups portés à chaque cible.</summary>
    public int Hits { get; set; } = 1;
    /// <summary>Chance de toucher (%), pour les attaques.</summary>
    public int Accuracy { get; set; } = 100;
    public int CritChance { get; set; }
    public double CritMultiplier { get; set; } = 1.5;
    /// <summary>PV que coûte la compétence au lanceur (il garde toujours au moins 1 PV).</summary>
    public int HpCost { get; set; }
    /// <summary>Tours à attendre avant de la réutiliser (0 = aucun).</summary>
    public int Cooldown { get; set; }
    /// <summary>Part des dégâts rendue en PV au lanceur (%).</summary>
    public int DrainPercent { get; set; }
    /// <summary>Effets durables (poison, bonus, étourdissement...).</summary>
    public List<SkillEffect> Effects { get; set; } = [];
    /// <summary>Texte du journal à la place du texte automatique (%lanceur%, %cible%, %sort%).</summary>
    public string UseText { get; set; } = "";
    /// <summary>Pouvoir auquel appartient la compétence (vide = aucun) : un PJ qui a ce pouvoir l'apprend.</summary>
    public string? PowerId { get; set; }
    /// <summary>Niveau auquel un PJ qui a le pouvoir apprend cette compétence.</summary>
    public int PowerLevel { get; set; } = 1;
}

/// <summary>
/// Pouvoir : une famille de compétences (« Pyromancie », « Épée »...). Un PJ qui a le pouvoir apprend toutes
/// ses compétences, chacune à son niveau. Sur la fiche du PJ ou par l'effet « Pouvoir : donner ».
/// </summary>
public sealed class PowerDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Effet durable d'une compétence.</summary>
public sealed class SkillEffect
{
    public EffectType Type { get; set; } = EffectType.Poison;
    /// <summary>Statistique visée (bonus / malus).</summary>
    public StatKind Stat { get; set; } = StatKind.Attack;
    /// <summary>PV par tour (poison, régénération), % (bonus, malus) ou points absorbés (bouclier).</summary>
    public int Amount { get; set; } = 10;
    public int Turns { get; set; } = 3;
    public int Chance { get; set; } = 100;
    /// <summary>S'applique au lanceur plutôt qu'aux cibles.</summary>
    public bool OnSelf { get; set; }
}

/// <summary>Faiblesse ou résistance à un élément : 100 = normal, 200 = faiblesse, 50 = résistance, 0 = immunité, négatif = absorbe (soigne).</summary>
public sealed class ElementModifier
{
    public string Element { get; set; } = "";
    public int Percent { get; set; } = 100;

    public ElementModifier() { }
    public ElementModifier(string element, int percent) { Element = element; Percent = percent; }
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
    /// <summary>Pièce d'armure : où elle se porte (tête, corps, mains, jambes, pieds, accessoire, bouclier).</summary>
    public EquipSlot ArmorSlot { get; set; } = EquipSlot.Armor;

    /// <summary>Emplacements possibles pour une pièce d'armure.</summary>
    public static readonly EquipSlot[] ArmorSlots =
        [EquipSlot.Head, EquipSlot.Armor, EquipSlot.Hands, EquipSlot.Legs, EquipSlot.Feet, EquipSlot.Accessory, EquipSlot.Shield];

    [JsonIgnore] public bool IsConsumable => Type == ItemType.Consumable;

    [JsonIgnore]
    public EquipSlot? Slot => Type switch
    {
        ItemType.Weapon => EquipSlot.Weapon,
        ItemType.Armor => ArmorSlots.Contains(ArmorSlot) ? ArmorSlot : EquipSlot.Armor,
        ItemType.Relic when RelicUsage == RelicUsage.Equipable => EquipSlot.Relic,
        _ => null,
    };

    [JsonIgnore] public bool IsEquipable => Slot is not null;

    [JsonIgnore]
    public bool IsSellable => Price > 0 && Type != ItemType.Quest
        && !(Type == ItemType.Relic && RelicUsage == RelicUsage.Quest);
}

/// <summary>
/// Passif : un effet permanent d'un personnage (« Sang-froid », « Rage du désespoir »...). Il peut ne marcher
/// que sous conditions (ex : Folie ≥ 50, de nuit, sous un flag) — « @soi » y désigne le porteur du passif.
/// </summary>
public sealed class PassiveDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Le passif n'agit que si ces conditions sont remplies (vide = toujours).</summary>
    public List<Condition> Conditions { get; set; } = [];
    /// <summary>Bonus fixes aux stats (peuvent être négatifs).</summary>
    public StatBlock Bonus { get; set; } = new();
    /// <summary>Bonus en % des stats (ex : ATQ 20 = +20 %).</summary>
    public StatBlock Percent { get; set; } = new();
    /// <summary>Faiblesses et résistances ajoutées.</summary>
    public List<ElementModifier> Resistances { get; set; } = [];
    /// <summary>Effets posés sur le porteur au début de chaque combat (régénération, bouclier, bonus...).</summary>
    public List<SkillEffect> BattleStart { get; set; } = [];
    /// <summary>PV rendus (ou perdus si négatif) au porteur au début de chacun de ses tours.</summary>
    public int HpPerTurn { get; set; }
    public int ManaPerTurn { get; set; }
    /// <summary>Bonus d'XP et d'or gagnés en combat, en % (cumulés sur l'équipe).</summary>
    public int XpPercent { get; set; }
    public int GoldPercent { get; set; }
}

/// <summary>Passif d'un personnage, obtenu à un niveau.</summary>
public sealed class PassiveUnlock
{
    public int Level { get; set; } = 1;
    public string PassiveId { get; set; } = "";

    public PassiveUnlock() { }
    public PassiveUnlock(int level, string passiveId) { Level = level; PassiveId = passiveId; }
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
    /// <summary>Classe (rôle en jeu), ex : « Chevalier ».</summary>
    public string Class { get; set; } = "";
    /// <summary>Titre (surnom, rang), ex : « le Chevalier errant ».</summary>
    public string Title { get; set; } = "";

    /// <summary>Classe et titre réunis pour l'affichage (« Chevalier · Chevalier errant »).</summary>
    [JsonIgnore]
    public string ClassAndTitle => string.Join(" · ", new[] { Class, Title }.Where(x => x.Length > 0));
    public string Description { get; set; } = "";
    public StatBlock BaseStats { get; set; } = new();
    public StatBlock GrowthPerLevel { get; set; } = new();
    public List<SkillUnlock> Skills { get; set; } = [];
    /// <summary>Pouvoirs du personnage : il apprend toutes leurs compétences (chacune à son niveau).</summary>
    public List<string> PowerIds { get; set; } = [];
    /// <summary>Passifs du personnage, chacun obtenu à un niveau.</summary>
    public List<PassiveUnlock> Passives { get; set; } = [];
    public string? StartingWeaponId { get; set; }
    public string? StartingArmorId { get; set; }
    public string? StartingRelicId { get; set; }
    /// <summary>Autres pièces portées au départ (tête, mains, jambes, pieds, accessoire, bouclier).</summary>
    public List<string> StartingGearIds { get; set; } = [];
    /// <summary>Proposé sur l'écran de sélection de départ.</summary>
    public bool IsStarter { get; set; }
    /// <summary>Départ de partie de ce héros (vide = départ principal).</summary>
    public string? StartId { get; set; }
    /// <summary>Quand on joue ce héros : PJ qui l'accompagnent dès le début (en plus de ceux du départ).</summary>
    public List<string> StartCompanions { get; set; } = [];
    /// <summary>Quand on joue ce héros : effets au lancement de la partie (or, objets, flags, karma, amitiés, quêtes...).</summary>
    public List<GameAction> StartActions { get; set; } = [];
    /// <summary>Karma de départ (vide = valeur par défaut des réglages de karma).</summary>
    public int? BaseKarma { get; set; }
    /// <summary>Valeur de départ des jauges (folie...) : id de jauge → valeur (absente = valeur par défaut de la jauge).</summary>
    public Dictionary<string, int> BaseGauges { get; set; } = [];
    /// <summary>Amitié de départ envers les autres (vide = valeur par défaut des réglages d'amitié).</summary>
    public int? BaseFriendship { get; set; }
    /// <summary>Répliques de combat du personnage.</summary>
    public List<BattleLine> BattleLines { get; set; } = [];
    public List<ElementModifier> Resistances { get; set; } = [];
    public string? PortraitId { get; set; }
}

/// <summary>Réplique dite pendant un combat (provocation, cri de douleur, dernier mot...).</summary>
public sealed class BattleLine
{
    public BattleTrigger Trigger { get; set; } = BattleTrigger.Start;
    /// <summary>Tour (déclencheur « Tour ») ou pourcentage de PV (« PV sous »).</summary>
    public int Amount { get; set; } = 50;
    /// <summary>Qui parle (vide = le personnage ou monstre lui-même).</summary>
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>Chance en % que la réplique soit dite quand le moment arrive.</summary>
    public int Chance { get; set; } = 100;
    public List<Condition> Conditions { get; set; } = [];
    /// <summary>Effets (flag, karma, variable...) appliqués quand la réplique est dite.</summary>
    public List<GameAction> Actions { get; set; } = [];
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
    /// <summary>Répliques de combat du monstre.</summary>
    public List<BattleLine> BattleLines { get; set; } = [];
    public List<ElementModifier> Resistances { get; set; } = [];
    public string? PortraitId { get; set; }
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
    /// <summary>Dialogue joué après une victoire / après une défaite (sauf game over).</summary>
    public string? VictoryDialogueId { get; set; }
    public string? DefeatDialogueId { get; set; }
    /// <summary>Répliques propres à ce combat (narration ou personnage nommé).</summary>
    public List<BattleLine> BattleLines { get; set; } = [];

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
    /// <summary>
    /// Lieu qui contient celui-ci (vide = lieu de la carte du royaume). Comme des salons dans une catégorie :
    /// une taverne dans une ville, une salle dans un donjon... Un sous-lieu peut lui-même en contenir d'autres.
    /// </summary>
    public string? ParentId { get; set; }
    /// <summary>Caché au début de la partie : n'apparaît qu'une fois révélé par l'effet « Lieu : révéler ».</summary>
    public bool HiddenAtStart { get; set; }

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
    /// <summary>Caché au début de la partie : n'apparaît qu'avec l'effet « PNJ : montrer ».</summary>
    public bool HiddenAtStart { get; set; }
    /// <summary>Le PNJ n'apparaît que si ces conditions sont remplies.</summary>
    public List<Condition> VisibleConditions { get; set; } = [];
    /// <summary>Dialogues selon l'avancement : le premier dont les conditions passent est joué.</summary>
    public List<NpcDialogue> ConditionalDialogues { get; set; } = [];
    public string? DefaultDialogueId { get; set; }
    public string? PortraitId { get; set; }
    /// <summary>Membre du campement dès le début de la partie (avec ce grade ; vide = grade le plus bas).</summary>
    public bool StartsInCamp { get; set; }
    public string? StartRankId { get; set; }
    /// <summary>Fiche de combat (vide = le PNJ ne se bat pas). Avec elle, on peut le combattre comme un monstre.</summary>
    public NpcCombat? Combat { get; set; }
}

/// <summary>
/// Un PNJ qui sait se battre : stats, compétences, récompenses et répliques, comme un monstre.
/// On le combat avec l'effet « Combat : lancer » (dans un dialogue, une quête...) ou il attaque de lui-même
/// l'équipe qui arrive dans son lieu. Une fois vaincu, le flag « pnj_vaincu:{id} » est posé.
/// </summary>
public sealed class NpcCombat
{
    public StatBlock Stats { get; set; } = new();
    public List<string> SkillIds { get; set; } = [];
    public int Xp { get; set; }
    public int Gold { get; set; }
    public List<ItemDrop> Drops { get; set; } = [];
    public bool IsBoss { get; set; }
    public List<BattleLine> BattleLines { get; set; } = [];
    public List<ElementModifier> Resistances { get; set; } = [];
    /// <summary>Monstres qui se battent à ses côtés.</summary>
    public List<string> AllyIds { get; set; } = [];

    /// <summary>Attaque l'équipe quand elle arrive dans son lieu (si les conditions passent).</summary>
    public bool Attacks { get; set; }
    public List<Condition> AttackConditions { get; set; } = [];
    /// <summary>Attaque encore après avoir été vaincu (sinon une seule victoire suffit).</summary>
    public bool AttacksAgain { get; set; }
    /// <summary>Dialogue avant son attaque, puis après une victoire / une défaite de l'équipe.</summary>
    public string? AttackDialogueId { get; set; }
    public string? VictoryDialogueId { get; set; }
    public string? DefeatDialogueId { get; set; }

    /// <summary>Le PNJ vu comme un adversaire de combat (même identifiant que le PNJ).</summary>
    public MonsterDef AsMonster(NpcDef npc) => new()
    {
        Id = npc.Id, Name = npc.Name, Description = npc.Description, PortraitId = npc.PortraitId,
        Stats = Stats, SkillIds = SkillIds, Xp = Xp, Gold = Gold, Drops = Drops, IsBoss = IsBoss,
        BattleLines = BattleLines, Resistances = Resistances,
    };
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
    /// <summary>Identifiant du choix dans sa réplique (condition « A choisi ») ; vide = son numéro (1, 2, 3...).</summary>
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string? NextId { get; set; }
    public List<GameAction> Actions { get; set; } = [];
    /// <summary>Le choix n'est proposé que si ces conditions sont remplies.</summary>
    public List<Condition> Conditions { get; set; } = [];
    /// <summary>Si les conditions ne sont pas remplies : afficher le choix grisé (au lieu de le cacher).</summary>
    public bool ShowLocked { get; set; }
    /// <summary>Texte affiché sous un choix grisé (ex : « Karma trop bas »).</summary>
    public string LockedText { get; set; } = "";
    /// <summary>Choix-narration : une action décrite (« Tu t'éloignes sans un mot. ») plutôt qu'une réplique.</summary>
    public bool Narration { get; set; }
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
    /// <summary>Clé d'un choix de cette réplique : son identifiant, sinon son numéro (à partir de 1).</summary>
    public string ChoiceKey(DialogueChoice choice) => choice.Id.Length > 0 ? choice.Id : (Choices.IndexOf(choice) + 1).ToString();

    public string Id { get; set; } = "";
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>
    /// Nœud suivant quand il n'y a pas de choix. Null = fin du dialogue.
    /// « dialogue:etiquette » continue dans un autre dialogue (les histoires peuvent se croiser).
    /// </summary>
    public string? NextId { get; set; }
    /// <summary>
    /// Réplique d'un PJ : si ce PJ est le héros joué, elle est proposée au joueur comme un choix (avec les autres
    /// choix de la réplique, s'il y en a) ; sinon (un compagnon), il la dit simplement et la suite s'enchaîne.
    /// </summary>
    public bool HeroChoice { get; set; }
    public List<DialogueChoice> Choices { get; set; } = [];
    public List<GameAction> Actions { get; set; } = [];
    /// <summary>Aiguillages testés avant NextId (le premier qui passe gagne).</summary>
    public List<DialogueBranch> Branches { get; set; } = [];
    /// <summary>Versions alternatives du texte selon la situation (karma, amitié, qui parle, heure...).</summary>
    public List<TextVariant> Variants { get; set; } = [];
    /// <summary>Portrait affiché (vide = celui du PJ/PNJ dont le nom est « Qui parle »).</summary>
    public string? PortraitId { get; set; }
    /// <summary>La réplique n'est jouée que si ces conditions sont remplies (vide = toujours).</summary>
    public List<Condition> Conditions { get; set; } = [];
    /// <summary>Si les conditions ne sont pas remplies : aller à cette réplique (vide = passer à la suite normale).</summary>
    public string? ElseId { get; set; }
}

public sealed class DialogueDef
{
    public string Id { get; set; } = "";
    /// <summary>Nom lisible dans l'éditeur (jamais montré au joueur).</summary>
    public string Name { get; set; } = "";
    /// <summary>Titre montré au joueur en haut du dialogue (vide = aucun titre).</summary>
    public string DisplayTitle { get; set; } = "";
    /// <summary>
    /// PJ présents dans la scène, même s'ils ne sont pas dans le groupe : pendant ce dialogue ils peuvent parler,
    /// comptent comme présents pour les conditions, reçoivent les effets (karma, folie...) et sont affichés.
    /// </summary>
    public List<string> ScenePjIds { get; set; } = [];
    /// <summary>Classique (boîte de dialogue sur le jeu) ou cinématique (plein écran noir, texte au milieu).</summary>
    public DialogueStyle Style { get; set; } = DialogueStyle.Classic;
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

/// <summary>Sortie d'une étape : si les conditions passent, la quête part vers l'étape visée (bifurcation).</summary>
public sealed class QuestExit
{
    /// <summary>Nom lisible du chemin (éditeur, historique), ex : « A épargné le bandit ».</summary>
    public string Label { get; set; } = "";
    public List<Condition> Conditions { get; set; } = [];
    public string NextStageId { get; set; } = "";
    /// <summary>Effets appliqués en prenant ce chemin.</summary>
    public List<GameAction> Actions { get; set; } = [];
}

/// <summary>
/// Étape d'une quête narrative. On y entre (effets sur le monde), on remplit ses objectifs (dans l'ordre),
/// puis la première sortie dont les conditions passent mène à l'étape suivante. Une étape « fin » termine la quête.
/// </summary>
public sealed class QuestStage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Texte du journal pour cette étape (balises %pj%... possibles).</summary>
    public string Journal { get; set; } = "";
    public List<QuestObjective> Objectives { get; set; } = [];
    /// <summary>Effets en entrant dans l'étape (le monde change : lieux révélés, PNJ déplacés, flags...).</summary>
    public List<GameAction> OnEnter { get; set; } = [];
    /// <summary>Chemins possibles, testés dans l'ordre une fois les objectifs remplis.</summary>
    public List<QuestExit> Exits { get; set; } = [];
    /// <summary>Étape finale : la quête se termine ici (réussie, ou échouée).</summary>
    public bool IsEnding { get; set; }
    public bool Failure { get; set; }
}

/// <summary>
/// Partie d'une quête : les parties se font en parallèle, dans n'importe quel ordre, chacune avec son état
/// (pas commencée, en cours, terminée, échouée). La quête est réussie quand toutes ses parties obligatoires le sont.
/// </summary>
public sealed class QuestPart
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Texte du journal pour cette partie.</summary>
    public string Journal { get; set; } = "";
    /// <summary>Objectifs, dans l'ordre (vide = la partie se termine par un effet « Quête : terminer une partie »).</summary>
    public List<QuestObjective> Objectives { get; set; } = [];
    /// <summary>La partie commence quand ces conditions sont remplies (vide = dès le début de la quête).</summary>
    public List<Condition> StartConditions { get; set; } = [];
    /// <summary>La partie échoue si ces conditions deviennent vraies pendant qu'elle est en cours.</summary>
    public List<Condition> FailConditions { get; set; } = [];
    /// <summary>Facultative : pas nécessaire pour réussir la quête (et son échec ne fait pas échouer la quête).</summary>
    public bool Optional { get; set; }
    /// <summary>Effets quand la partie est terminée.</summary>
    public List<GameAction> Rewards { get; set; } = [];
}

/// <summary>
/// Quête. Trois façons de la construire : une simple liste d'objectifs (dans l'ordre, puis récompenses),
/// ou des étapes avec embranchements et plusieurs fins (<see cref="Stages"/>).
/// </summary>
public sealed class QuestDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<QuestObjective> Objectives { get; set; } = [];
    /// <summary>Récompenses de fin (quête simple, ou fin réussie d'une quête à étapes).</summary>
    public List<GameAction> Rewards { get; set; } = [];
    /// <summary>Étapes de la quête narrative (vide = quête simple). La première est le point de départ.</summary>
    public List<QuestStage> Stages { get; set; } = [];
    /// <summary>Parties en parallèle (quête en plusieurs parties). Ignoré si la quête a des étapes.</summary>
    public List<QuestPart> Parts { get; set; } = [];
    /// <summary>La quête démarre toute seule dès que ces conditions sont remplies (vide = seulement par un effet).</summary>
    public List<Condition> AutoStart { get; set; } = [];

    [JsonIgnore] public bool IsStaged => Stages.Count > 0;
    [JsonIgnore] public bool HasParts => Parts.Count > 0 && !IsStaged;
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

/// <summary>Un départ de partie (origine, prologue). Le contenu peut en proposer plusieurs.</summary>
public sealed class StartSettings
{
    public string Id { get; set; } = "principal";
    public string Name { get; set; } = "Départ principal";
    public string Description { get; set; } = "";
    /// <summary>Ancien réglage : héros liés à ce départ (remplacé par le départ choisi sur chaque héros, gardé pour les anciens contenus).</summary>
    public List<string> HeroIds { get; set; } = [];
    public string LocationId { get; set; } = "";
    public int Gold { get; set; } = 100;
    public List<ItemStack> Inventory { get; set; } = [];
    public string? IntroDialogueId { get; set; }
    /// <summary>Autres dialogues d'introduction, joués à la suite (dans l'ordre).</summary>
    public List<string> MoreIntroDialogueIds { get; set; } = [];

    /// <summary>Tous les dialogues d'introduction, dans l'ordre.</summary>
    [JsonIgnore]
    public IEnumerable<string> IntroDialogues =>
        (IntroDialogueId is { Length: > 0 } first ? new[] { first } : []).Concat(MoreIntroDialogueIds);
    /// <summary>PJ qui accompagnent le héros dès le début.</summary>
    public List<string> Companions { get; set; } = [];
    /// <summary>Effets au lancement (flags, variables, karma, quêtes, camp...) : le monde de départ.</summary>
    public List<GameAction> Actions { get; set; } = [];
    /// <summary>Date de départ propre à ce départ (vide = celle des réglages du temps).</summary>
    public int? Day { get; set; }
    public int? Hour { get; set; }
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

/// <summary>
/// Image de la banque d'images : seul le lien est enregistré (l'image reste sur son site).
/// Le cadrage (point central + zoom) s'adapte à toutes les formes de cadre (portrait, carré...).
/// </summary>
public sealed class PortraitDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Adresse de l'image (https).</summary>
    public string Url { get; set; } = "";
    /// <summary>Largeur / hauteur de l'image (mesurée à l'import ; 0 = inconnu).</summary>
    public double Aspect { get; set; }
    /// <summary>Point de l'image placé au centre du cadre (0 à 1).</summary>
    public double FocusX { get; set; } = 0.5;
    public double FocusY { get; set; } = 0.35;
    /// <summary>Zoom (1 = l'image couvre juste le cadre).</summary>
    public double Zoom { get; set; } = 1;
}

/// <summary>Grade de la hiérarchie du campement (niveau plus haut = plus gradé).</summary>
public sealed class CampRankDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
    /// <summary>Nombre de places (0 = illimité).</summary>
    public int Max { get; set; }
}

/// <summary>Résultat possible d'une tâche, tiré au sort à la fin de chaque cycle.</summary>
public sealed class CampOutcome
{
    /// <summary>Texte du journal du camp (%membre% = celui qui fait la tâche).</summary>
    public string Text { get; set; } = "";
    /// <summary>Poids du tirage (plus = plus fréquent).</summary>
    public int Weight { get; set; } = 1;
    public List<Condition> Conditions { get; set; } = [];
    /// <summary>Effets ; « @membre » désigne celui qui fait la tâche (ex : amitié de @membre +2).</summary>
    public List<GameAction> Actions { get; set; } = [];
}

/// <summary>Tâche du campement (rondes, chasse...), répétée tant que le membre y est affecté.</summary>
public sealed class CampTaskDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Icône (mot-clé : ronde, chasse, peche, cueillette, bois, forge, garde, entrainement, repos, cuisine, eclaireur, commerce, mine, construction, soin).</summary>
    public string Icon { get; set; } = "ronde";
    /// <summary>Durée d'un cycle, en minutes de jeu.</summary>
    public int DurationMinutes { get; set; } = 240;
    /// <summary>Grade minimum (niveau) pour être affecté.</summary>
    public int MinRankLevel { get; set; }
    /// <summary>Nombre maximum de membres affectés (0 = illimité).</summary>
    public int MaxWorkers { get; set; }
    /// <summary>La tâche n'est proposée que si ces conditions sont remplies.</summary>
    public List<Condition> Conditions { get; set; } = [];
    public List<CampOutcome> Outcomes { get; set; } = [];
}

/// <summary>Ressource stockée au camp (nourriture, bois...), produite par les tâches et consommée chaque jour.</summary>
public sealed class CampResourceDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Icône (mot-clé, comme les tâches : chasse, bois, cuisine...).</summary>
    public string Icon { get; set; } = "cuisine";
    public int Initial { get; set; }
    /// <summary>Stock maximum (0 = illimité).</summary>
    public int Max { get; set; }
    /// <summary>Quantité consommée chaque jour par habitant du camp (0 = aucune).</summary>
    public int DailyPerMember { get; set; }
    /// <summary>En cas de pénurie : amitié perdue par chaque habitant, pour chaque jour sans cette ressource.</summary>
    public int ShortageFriendshipLoss { get; set; } = 2;
}

/// <summary>Coût d'une construction en ressources du camp.</summary>
public sealed class ResourceCost
{
    public string ResourceId { get; set; } = "";
    public int Amount { get; set; } = 1;

    public ResourceCost() { }
    public ResourceCost(string resourceId, int amount) { ResourceId = resourceId; Amount = amount; }
}

/// <summary>Lieu du camp (bâtiment, zone) à construire : il peut débloquer des tâches et changer le monde.</summary>
public sealed class CampBuildingDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "construction";
    public int GoldCost { get; set; }
    public List<ResourceCost> Costs { get; set; } = [];
    /// <summary>Constructible seulement si ces conditions sont remplies.</summary>
    public List<Condition> Conditions { get; set; } = [];
    /// <summary>Effets une fois construit (flags, variables, lieux révélés...).</summary>
    public List<GameAction> OnBuilt { get; set; } = [];
    /// <summary>Déjà construit au début de la partie.</summary>
    public bool BuiltAtStart { get; set; }
}

/// <summary>Réglages du campement : hiérarchie, tâches, ressources et lieux.</summary>
public sealed class CampSettings
{
    public bool Enabled { get; set; } = true;
    /// <summary>Titre du héros à la tête du camp.</summary>
    public string LeaderTitle { get; set; } = "Chef";
    public List<CampRankDef> Ranks { get; set; } = [];
    public List<CampTaskDef> Tasks { get; set; } = [];
    public List<CampResourceDef> Resources { get; set; } = [];
    public List<CampBuildingDef> Buildings { get; set; } = [];
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

/// <summary>
/// Jauge propre à chaque personnage, comme le karma : folie, peur, corruption... Elle évolue avec les effets
/// « Jauge : ajouter / fixer » et fait réagir dialogues et PNJ avec la condition « Jauge ».
/// </summary>
public sealed class CharacterGaugeDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Default { get; set; }
    public int Min { get; set; }
    public int Max { get; set; } = 100;
    /// <summary>Afficher la valeur au joueur (sinon elle reste cachée mais agit quand même).</summary>
    public bool Visible { get; set; } = true;
    public List<ScaleTier> Tiers { get; set; } = [];

    public string TierName(int value) =>
        Tiers.Where(t => value >= t.Min).OrderByDescending(t => t.Min).FirstOrDefault()?.Name ?? "";

    public int Clamp(int value) => Math.Clamp(value, Math.Min(Min, Max), Math.Max(Min, Max));
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
    /// <summary>Afficher l'onglet Quêtes (journal des quêtes). Sans lui, les quêtes avancent quand même (messages à l'écran).</summary>
    public bool ShowQuestTab { get; set; }
    /// <summary>Textes de l'interface remplacés (clé → texte). Voir <see cref="Data.Vocabulary"/>.</summary>
    public Dictionary<string, string> Texts { get; set; } = [];
    /// <summary>L'écran se fissure quand le héros perd ses PV, puis éclate à la défaite.</summary>
    public CrackSettings Cracks { get; set; } = new();
}

/// <summary>Écran qui se fissure selon les PV du héros (personnage principal), puis éclate au game over.</summary>
public sealed class CrackSettings
{
    public bool Enabled { get; set; } = true;
    /// <summary>Seuils en % de PV du héros : sous chaque seuil, l'écran se fissure un peu plus.</summary>
    public List<int> Thresholds { get; set; } = [30, 15, 5];
    /// <summary>Force des fissures (0,5 = discrètes, 1 = normal, 2 = très visibles).</summary>
    public double Intensity { get; set; } = 1;
    /// <summary>Secousse et vibration à chaque nouvelle fissure.</summary>
    public bool Shake { get; set; } = true;
    /// <summary>À la défaite, l'écran éclate en morceaux avant l'écran de fin.</summary>
    public bool Shatter { get; set; } = true;
    public string GameOverTitle { get; set; } = "GAME OVER";
    public string GameOverText { get; set; } = "Ta route s'arrête ici.";

    /// <summary>Niveau de fissure (0 = intact, jusqu'au nombre de seuils) pour un pourcentage de PV.</summary>
    public int Level(int hpPercent) => !Enabled ? 0 : Thresholds.Count(t => hpPercent < t);

    public int MaxLevel => Enabled ? Thresholds.Count(t => t > 0) : 0;
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
    /// <summary>Autres départs proposés au joueur (en plus du départ principal).</summary>
    public List<StartSettings> ExtraStarts { get; set; } = [];
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
    public List<PortraitDef> Portraits { get; set; } = [];
    public CampSettings Camp { get; set; } = new();
    public TutorialSettings Tutorial { get; set; } = new();
    /// <summary>Jauges propres à chaque personnage (folie...), en plus du karma.</summary>
    public List<CharacterGaugeDef> Gauges { get; set; } = [];
    /// <summary>Passifs, attribués aux personnages (fiche du PJ ou effet « Passif : donner »).</summary>
    public List<PassiveDef> Passives { get; set; } = [];
    /// <summary>Pouvoirs : familles de compétences données d'un coup à un PJ.</summary>
    public List<PowerDef> Powers { get; set; } = [];
}

/// <summary>
/// Prologue (tutoriel) : une courte partie jouée avant la sélection des héros, proposée à chaque nouvelle partie.
/// Tout se règle dans le mode dev : qui on incarne (ou personne), où, les dialogues, et ce qui est verrouillé
/// au début puis débloqué petit à petit par les effets « Interface : débloquer ». L'effet « Prologue : terminer »
/// mène à la sélection des héros. Rien du prologue n'est gardé dans la vraie partie.
/// </summary>
public sealed class TutorialSettings
{
    public bool Enabled { get; set; }
    public string Name { get; set; } = "Prologue";
    /// <summary>Texte de la question posée au joueur avant la sélection des héros.</summary>
    public string Proposal { get; set; } = "Veux-tu jouer le prologue avant de choisir ton héros ?";
    /// <summary>Personnage joué pendant le prologue (vide = personne : pas d'équipe, donc pas de combat).</summary>
    public string? HeroId { get; set; }
    /// <summary>Lieu, or, objets, compagnons, dialogues d'ouverture, effets, date : comme un départ.</summary>
    public StartSettings Start { get; set; } = new() { Id = "prologue", Name = "Prologue", Gold = 0 };
    /// <summary>Verrouillé au début du prologue (débloqué ensuite par des effets).</summary>
    public List<UiFeature> LockedAtStart { get; set; } =
        [UiFeature.TabCamp, UiFeature.TabQuests, UiFeature.TabEncyclopedia, UiFeature.TabShop, UiFeature.TabJournal, UiFeature.WorldMap, UiFeature.Explore];
}
