namespace ProjetSKE.Core.Models;

// Définitions statiques du contenu du jeu (ne sont pas sauvegardées).

public sealed record SkillDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public SkillKind Kind { get; init; } = SkillKind.Physical;
    public SkillTarget Target { get; init; } = SkillTarget.SingleEnemy;
    public int ManaCost { get; init; }
    /// <summary>Multiplicateur appliqué à la stat d'attaque ou de magie.</summary>
    public double Power { get; init; } = 1.0;

    public bool TargetsEnemies => Target is SkillTarget.SingleEnemy or SkillTarget.AllEnemies;
    public bool TargetsAll => Target is SkillTarget.AllEnemies or SkillTarget.AllAllies;
}

public sealed record ItemDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public ItemType Type { get; init; }
    public int Price { get; init; }
    public StatBlock Bonus { get; init; } = StatBlock.Zero;
    public int HealHp { get; init; }
    public int HealMana { get; init; }
    /// <summary>Objet unique : on ne peut en posséder qu'un seul exemplaire.</summary>
    public bool IsUnique { get; init; }
    public RelicUsage RelicUsage { get; init; } = RelicUsage.Equipable;

    public bool IsConsumable => Type == ItemType.Consumable;

    public EquipSlot? Slot => Type switch
    {
        ItemType.Weapon => EquipSlot.Weapon,
        ItemType.Armor => EquipSlot.Armor,
        ItemType.Relic when RelicUsage == RelicUsage.Equipable => EquipSlot.Relic,
        _ => null,
    };

    public bool IsEquipable => Slot is not null;

    public bool IsSellable => Price > 0 && Type != ItemType.Quest
        && !(Type == ItemType.Relic && RelicUsage == RelicUsage.Quest);

    public int SellPrice => Price / 2;
}

public sealed record SkillUnlock(int Level, string SkillId);

public sealed record CharacterDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Classe / titre affiché, ex : "Chevalier errant".</summary>
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public required StatBlock BaseStats { get; init; }
    public StatBlock GrowthPerLevel { get; init; } = StatBlock.Zero;
    public IReadOnlyList<SkillUnlock> Skills { get; init; } = [];
    public string? StartingWeaponId { get; init; }
    public string? StartingArmorId { get; init; }
    /// <summary>Proposé sur l'écran de sélection de départ.</summary>
    public bool IsStarter { get; init; }
}

public sealed record ItemDrop(string ItemId, double Chance);

public sealed record MonsterDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public required StatBlock Stats { get; init; }
    public IReadOnlyList<string> SkillIds { get; init; } = [];
    public int Xp { get; init; }
    public int Gold { get; init; }
    public IReadOnlyList<ItemDrop> Drops { get; init; } = [];
    public bool IsBoss { get; init; }
}

public sealed record EncounterGroup(IReadOnlyList<string> MonsterIds, int Weight = 1);

/// <summary>Combat placé à un endroit précis, joué une seule fois.</summary>
public sealed record FixedBattleDef(string Id, IReadOnlyList<string> MonsterIds, string? IntroDialogueId = null);

/// <summary>Habitant avec qui on peut parler. Caché si le flag indiqué est posé.</summary>
public sealed record NpcDef(string Name, string DialogueId, string? HiddenIfFlag = null, string? RequiresFlag = null);

public sealed record LocationDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public LocationType Type { get; init; }
    public IReadOnlyList<string> ConnectedIds { get; init; } = [];
    public IReadOnlyList<string> ShopItemIds { get; init; } = [];
    public int InnPrice { get; init; } = 10;
    public IReadOnlyList<EncounterGroup> RandomEncounters { get; init; } = [];
    /// <summary>Probabilité (0 à 1) d'une rencontre aléatoire en arrivant ici.</summary>
    public double EncounterChance { get; init; }
    public FixedBattleDef? FixedBattle { get; init; }
    public string? FirstVisitDialogueId { get; init; }
    public IReadOnlyList<NpcDef> Npcs { get; init; } = [];

    public bool IsCity => Type == LocationType.City;
}

public sealed record DialogueAction(DialogueActionType Type, string Arg = "", int Amount = 1);

public sealed record DialogueChoice
{
    public required string Text { get; init; }
    public string? NextId { get; init; }
    public IReadOnlyList<DialogueAction> Actions { get; init; } = [];
}

public sealed record DialogueNode
{
    public required string Id { get; init; }
    public string Speaker { get; init; } = "";
    public required string Text { get; init; }
    /// <summary>Nœud suivant quand il n'y a pas de choix. Null = fin du dialogue.</summary>
    public string? NextId { get; init; }
    public IReadOnlyList<DialogueChoice> Choices { get; init; } = [];
    public IReadOnlyList<DialogueAction> Actions { get; init; } = [];
}

public sealed record DialogueDef
{
    public required string Id { get; init; }
    public required IReadOnlyList<DialogueNode> Nodes { get; init; }
    public string StartId => Nodes[0].Id;
}
