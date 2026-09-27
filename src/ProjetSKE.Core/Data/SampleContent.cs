using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Data;

/// <summary>
/// Contenu d'exemple (fantasy médiévale). Tout se modifie ici :
/// ajouter un personnage, un monstre, un lieu ou un dialogue = ajouter une entrée dans la liste.
/// </summary>
internal static class SampleContent
{
    public static GameDatabase Build() => new(
        Skills(),
        Items(),
        Characters(),
        Monsters(),
        Locations(),
        Dialogues(),
        startLocationId: "havrefort",
        startGold: 100,
        startInventory: new Dictionary<string, int> { ["potion"] = 3, ["ether"] = 1 },
        introDialogueId: "intro");

    // ------------------------------------------------------------------ Compétences

    private static IEnumerable<SkillDef> Skills() =>
    [
        // Héros
        new() { Id = "frappe", Name = "Frappe", Description = "Attaque de base.", Power = 1.0 },
        new() { Id = "coup_puissant", Name = "Coup puissant", Description = "Un coup lourd.", ManaCost = 4, Power = 1.6 },
        new() { Id = "tourbillon", Name = "Tourbillon", Description = "Frappe tous les ennemis.", ManaCost = 8, Power = 0.8, Target = SkillTarget.AllEnemies },
        new() { Id = "trait_arcanique", Name = "Trait arcanique", Description = "Petit projectile magique.", Kind = SkillKind.Magical, Power = 0.9 },
        new() { Id = "boule_feu", Name = "Boule de feu", Description = "Brûle un ennemi.", Kind = SkillKind.Magical, ManaCost = 5, Power = 1.6 },
        new() { Id = "blizzard", Name = "Blizzard", Description = "Gèle tous les ennemis.", Kind = SkillKind.Magical, ManaCost = 10, Power = 1.0, Target = SkillTarget.AllEnemies },
        new() { Id = "attaque_sournoise", Name = "Attaque sournoise", Description = "Vise les points faibles.", ManaCost = 3, Power = 1.8 },
        new() { Id = "pluie_dagues", Name = "Pluie de dagues", Description = "Touche tous les ennemis.", ManaCost = 6, Power = 0.7, Target = SkillTarget.AllEnemies },
        new() { Id = "soin", Name = "Soin", Description = "Soigne un allié.", Kind = SkillKind.Heal, ManaCost = 4, Power = 1.2, Target = SkillTarget.SingleAlly },
        new() { Id = "priere", Name = "Prière", Description = "Soigne toute l'équipe.", Kind = SkillKind.Heal, ManaCost = 10, Power = 0.7, Target = SkillTarget.AllAllies },

        // Monstres
        new() { Id = "morsure", Name = "Morsure" },
        new() { Id = "griffe", Name = "Griffes", Power = 1.2 },
        new() { Id = "coup_massue", Name = "Coup de massue", Power = 1.1 },
        new() { Id = "entaille", Name = "Entaille" },
        new() { Id = "crachat_venin", Name = "Crachat de venin", Kind = SkillKind.Magical, Power = 1.0, ManaCost = 3 },
        new() { Id = "coup_os", Name = "Coup d'os", Power = 1.1 },
        new() { Id = "charge_brutale", Name = "Charge brutale", Power = 1.5, ManaCost = 5 },
        new() { Id = "rayon_necrotique", Name = "Rayon nécrotique", Kind = SkillKind.Magical, Power = 1.3 },
        new() { Id = "vague_morte", Name = "Vague de mort", Kind = SkillKind.Magical, Power = 0.8, ManaCost = 8, Target = SkillTarget.AllEnemies },
    ];

    // ------------------------------------------------------------------ Objets

    private static IEnumerable<ItemDef> Items() =>
    [
        new() { Id = "potion", Name = "Potion", Description = "Rend 50 PV.", Type = ItemType.Consumable, Price = 20, HealHp = 50 },
        new() { Id = "grande_potion", Name = "Grande potion", Description = "Rend 150 PV.", Type = ItemType.Consumable, Price = 60, HealHp = 150 },
        new() { Id = "ether", Name = "Éther", Description = "Rend 30 PM.", Type = ItemType.Consumable, Price = 40, HealMana = 30 },

        new() { Id = "epee_courte", Name = "Épée courte", Description = "Lame simple et fiable.", Type = ItemType.Weapon, Price = 50, Bonus = new(Attack: 4) },
        new() { Id = "epee_longue", Name = "Épée longue", Description = "Lame de chevalier.", Type = ItemType.Weapon, Price = 180, Bonus = new(Attack: 9) },
        new() { Id = "dague", Name = "Dague", Description = "Légère et rapide.", Type = ItemType.Weapon, Price = 40, Bonus = new(Attack: 3, Speed: 2) },
        new() { Id = "dague_ombre", Name = "Dague de l'ombre", Description = "Forgée pour les voleurs.", Type = ItemType.Weapon, Price = 190, Bonus = new(Attack: 7, Speed: 4) },
        new() { Id = "baton_chene", Name = "Bâton de chêne", Description = "Canalise la magie.", Type = ItemType.Weapon, Price = 50, Bonus = new(Attack: 2, Magic: 4) },
        new() { Id = "baton_runique", Name = "Bâton runique", Description = "Gravé de runes anciennes.", Type = ItemType.Weapon, Price = 200, Bonus = new(MaxMana: 10, Magic: 7) },
        new() { Id = "masse", Name = "Masse", Description = "Arme des clercs.", Type = ItemType.Weapon, Price = 60, Bonus = new(Attack: 5) },

        new() { Id = "armure_cuir", Name = "Armure de cuir", Description = "Protection légère.", Type = ItemType.Armor, Price = 40, Bonus = new(Defense: 3) },
        new() { Id = "cotte_mailles", Name = "Cotte de mailles", Description = "Solide mais lourde.", Type = ItemType.Armor, Price = 160, Bonus = new(Defense: 7, Speed: -1) },
        new() { Id = "robe_mage", Name = "Robe de mage", Description = "Tissée de fils enchantés.", Type = ItemType.Armor, Price = 45, Bonus = new(MaxMana: 8, Defense: 1) },

        new() { Id = "amulette_valdor", Name = "Amulette de Valdor", Description = "Relique royale. Donne vigueur et vivacité.", Type = ItemType.Relic, IsUnique = true, Bonus = new(MaxHp: 20, Speed: 3) },
        new() { Id = "anneau_sombrebois", Name = "Anneau de Sombrebois", Description = "Relique sylvestre au pouvoir étrange.", Type = ItemType.Relic, IsUnique = true, Bonus = new(Attack: 3, Magic: 3) },
        new() { Id = "fragment_couronne", Name = "Fragment de la Couronne", Description = "Morceau de la couronne perdue de Valdor.", Type = ItemType.Relic, RelicUsage = RelicUsage.Quest, IsUnique = true },
    ];

    // ------------------------------------------------------------------ Personnages

    private static IEnumerable<CharacterDef> Characters() =>
    [
        new()
        {
            Id = "aldric", Name = "Aldric", Title = "Chevalier errant", IsStarter = true,
            Description = "Chevalier sans seigneur, parti sur les routes pour sauver Valdor.",
            BaseStats = new(MaxHp: 120, MaxMana: 20, Attack: 14, Defense: 10, Magic: 4, Speed: 8),
            GrowthPerLevel = new(MaxHp: 12, MaxMana: 2, Attack: 2, Defense: 2, Magic: 0, Speed: 1),
            Skills = [new(1, "frappe"), new(1, "coup_puissant"), new(4, "tourbillon")],
            StartingWeaponId = "epee_courte", StartingArmorId = "armure_cuir",
        },
        new()
        {
            Id = "lyra", Name = "Lyra", Title = "Mage de Brume",
            Description = "Jeune mage qui s'ennuie à mourir à Bourg-de-Brume.",
            BaseStats = new(MaxHp: 70, MaxMana: 50, Attack: 5, Defense: 5, Magic: 16, Speed: 10),
            GrowthPerLevel = new(MaxHp: 7, MaxMana: 5, Attack: 1, Defense: 1, Magic: 3, Speed: 1),
            Skills = [new(1, "trait_arcanique"), new(1, "boule_feu"), new(5, "blizzard")],
            StartingWeaponId = "baton_chene", StartingArmorId = "robe_mage",
        },
        new()
        {
            Id = "tobin", Name = "Tobin", Title = "Voleur des bois",
            Description = "Détrousseur de la forêt de Sombrebois, plus bavard que dangereux.",
            BaseStats = new(MaxHp: 85, MaxMana: 20, Attack: 12, Defense: 6, Magic: 3, Speed: 16),
            GrowthPerLevel = new(MaxHp: 9, MaxMana: 2, Attack: 2, Defense: 1, Magic: 0, Speed: 2),
            Skills = [new(1, "frappe"), new(1, "attaque_sournoise"), new(4, "pluie_dagues")],
            StartingWeaponId = "dague", StartingArmorId = "armure_cuir",
        },
        new()
        {
            Id = "maelle", Name = "Sœur Maëlle", Title = "Clerc",
            Description = "Prêtresse de la chapelle de Havrefort. Les morts la craignent.",
            BaseStats = new(MaxHp: 90, MaxMana: 40, Attack: 8, Defense: 8, Magic: 12, Speed: 7),
            GrowthPerLevel = new(MaxHp: 10, MaxMana: 4, Attack: 1, Defense: 2, Magic: 2, Speed: 1),
            Skills = [new(1, "frappe"), new(1, "soin"), new(4, "priere")],
            StartingWeaponId = "masse", StartingArmorId = "robe_mage",
        },
    ];

    // ------------------------------------------------------------------ Monstres

    private static IEnumerable<MonsterDef> Monsters() =>
    [
        new() { Id = "loup", Name = "Loup gris", Description = "Chasse en meute sur la Route du Roi.",
            Stats = new(MaxHp: 40, Attack: 9, Defense: 3, Speed: 11), SkillIds = ["morsure"], Xp = 8, Gold = 5,
            Drops = [new("potion", 0.2)] },
        new() { Id = "gobelin", Name = "Gobelin", Description = "Petit, laid et armé d'une massue.",
            Stats = new(MaxHp: 35, Attack: 8, Defense: 4, Speed: 9), SkillIds = ["coup_massue"], Xp = 7, Gold = 8 },
        new() { Id = "bandit", Name = "Bandit", Description = "Détrousseur des grands chemins.",
            Stats = new(MaxHp: 55, Attack: 11, Defense: 5, Speed: 10), SkillIds = ["entaille"], Xp = 12, Gold = 15,
            Drops = [new("potion", 0.3), new("dague", 0.05)] },
        new() { Id = "araignee", Name = "Araignée géante", Description = "Tisse ses toiles au cœur de Sombrebois.",
            Stats = new(MaxHp: 60, MaxMana: 12, Attack: 10, Defense: 4, Magic: 8, Speed: 12), SkillIds = ["morsure", "crachat_venin"], Xp = 14, Gold = 6,
            Drops = [new("ether", 0.2), new("anneau_sombrebois", 0.03)] },
        new() { Id = "harpie", Name = "Harpie", Description = "Hante le Col des Corbeaux.",
            Stats = new(MaxHp: 45, Attack: 12, Defense: 4, Speed: 15), SkillIds = ["griffe"], Xp = 12, Gold = 9 },
        new() { Id = "squelette", Name = "Squelette", Description = "Garde éternel de la Crypte oubliée.",
            Stats = new(MaxHp: 50, Attack: 12, Defense: 7, Speed: 6), SkillIds = ["coup_os"], Xp = 13, Gold = 10,
            Drops = [new("potion", 0.25)] },
        new() { Id = "garrick", Name = "Garrick le Balafré", Description = "Chef des bandits du Col des Corbeaux.", IsBoss = true,
            Stats = new(MaxHp: 180, MaxMana: 30, Attack: 15, Defense: 8, Speed: 10), SkillIds = ["entaille", "charge_brutale"], Xp = 60, Gold = 120,
            Drops = [new("epee_longue", 0.5), new("grande_potion", 1.0)] },
        new() { Id = "morvath", Name = "Roi-Liche Morvath", Description = "Ancien roi de Valdor, revenu d'entre les morts.", IsBoss = true,
            Stats = new(MaxHp: 400, MaxMana: 80, Attack: 14, Defense: 10, Magic: 20, Speed: 9), SkillIds = ["rayon_necrotique", "vague_morte", "coup_os"], Xp = 200, Gold = 300,
            Drops = [new("fragment_couronne", 1.0), new("amulette_valdor", 1.0)] },
    ];

    // ------------------------------------------------------------------ Lieux

    private static IEnumerable<LocationDef> Locations() =>
    [
        new()
        {
            Id = "havrefort", Name = "Havrefort", Type = LocationType.City,
            Description = "Capitale fortifiée du royaume de Valdor.",
            ConnectedIds = ["route_roi"],
            ShopItemIds = ["potion", "ether", "epee_courte", "epee_longue", "dague", "baton_chene", "masse", "armure_cuir", "cotte_mailles", "robe_mage"],
            InnPrice = 10,
            Npcs =
            [
                new("Capitaine Hardin", "capitaine"),
                new("Sœur Maëlle", "recruter_maelle", HiddenIfFlag: "recruited:maelle"),
            ],
        },
        new()
        {
            Id = "route_roi", Name = "Route du Roi", Type = LocationType.Wild,
            Description = "La grande route qui relie la capitale au reste du pays.",
            ConnectedIds = ["havrefort", "bourg_brume", "foret_sombrebois"],
            EncounterChance = 0.35,
            RandomEncounters = [new(["loup"], 3), new(["loup", "loup"], 2), new(["gobelin"], 3), new(["gobelin", "loup"], 1)],
        },
        new()
        {
            Id = "bourg_brume", Name = "Bourg-de-Brume", Type = LocationType.City,
            Description = "Village paisible noyé dans la brume matinale.",
            ConnectedIds = ["route_roi", "col_corbeaux"],
            ShopItemIds = ["potion", "grande_potion", "ether", "baton_runique", "dague_ombre", "robe_mage"],
            InnPrice = 8,
            FirstVisitDialogueId = "rencontre_lyra",
            Npcs =
            [
                new("Lyra", "rencontre_lyra", HiddenIfFlag: "recruited:lyra"),
                new("Aubergiste", "rumeurs"),
            ],
        },
        new()
        {
            Id = "foret_sombrebois", Name = "Forêt de Sombrebois", Type = LocationType.Wild,
            Description = "Forêt épaisse où la lumière peine à passer.",
            ConnectedIds = ["route_roi", "crypte"],
            EncounterChance = 0.5,
            RandomEncounters = [new(["araignee"], 3), new(["loup", "loup", "loup"], 1), new(["bandit"], 2), new(["araignee", "loup"], 1)],
            FirstVisitDialogueId = "rencontre_tobin",
            Npcs = [new("Tobin", "rencontre_tobin", HiddenIfFlag: "recruited:tobin")],
        },
        new()
        {
            Id = "col_corbeaux", Name = "Col des Corbeaux", Type = LocationType.Wild,
            Description = "Passage montagneux battu par les vents.",
            ConnectedIds = ["bourg_brume"],
            EncounterChance = 0.45,
            RandomEncounters = [new(["harpie"], 3), new(["harpie", "harpie"], 1), new(["bandit", "bandit"], 2)],
            FixedBattle = new("garrick", ["garrick", "bandit"], "intro_garrick"),
        },
        new()
        {
            Id = "crypte", Name = "Crypte oubliée", Type = LocationType.Dungeon,
            Description = "Tombeau des anciens rois. Quelque chose s'y est réveillé.",
            ConnectedIds = ["foret_sombrebois"],
            EncounterChance = 0.6,
            RandomEncounters = [new(["squelette"], 3), new(["squelette", "squelette"], 2), new(["squelette", "araignee"], 1)],
            FixedBattle = new("morvath", ["morvath"], "intro_morvath"),
        },
    ];

    // ------------------------------------------------------------------ Dialogues

    private static DialogueAction Recruit(string id) => new(DialogueActionType.Recruit, id);

    private static IEnumerable<DialogueDef> Dialogues() =>
    [
        new()
        {
            Id = "intro",
            Nodes =
            [
                new() { Id = "1", Speaker = "", Text = "Le royaume de Valdor vacille. Depuis la disparition de la couronne, les morts ne dorment plus.", NextId = "2" },
                new() { Id = "2", Speaker = "", Text = "Tu arrives à Havrefort, la capitale, avec ton épée et quelques pièces en poche.", NextId = "3" },
                new() { Id = "3", Speaker = "", Text = "Le Capitaine Hardin cherche des volontaires. C'est peut-être le début de ton aventure." },
            ],
        },
        new()
        {
            Id = "capitaine",
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "Capitaine Hardin",
                    Text = "Des morts-vivants sortent de la Crypte oubliée, au-delà de la forêt de Sombrebois. Il faut quelqu'un pour y mettre fin.",
                    Choices =
                    [
                        new() { Text = "J'irai.", NextId = "oui" },
                        new() { Text = "Pas maintenant.", NextId = "non" },
                    ],
                },
                new()
                {
                    Id = "oui", Speaker = "Capitaine Hardin",
                    Text = "Brave. Prends ceci pour la route. Et passe voir Sœur Maëlle à la chapelle : les morts la craignent.",
                    Actions = [new(DialogueActionType.GiveGold, Amount: 50), new(DialogueActionType.SetFlag, "quete_crypte")],
                    NextId = "fin",
                },
                new() { Id = "non", Speaker = "Capitaine Hardin", Text = "Reviens quand tu seras prêt." },
                new() { Id = "fin", Speaker = "", Text = "Nouvel objectif : purifier la Crypte oubliée." },
            ],
        },
        new()
        {
            Id = "recruter_maelle",
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "Sœur Maëlle",
                    Text = "Tu comptes descendre dans la crypte ? Seul ? Laisse-moi t'accompagner, ma lumière te sera utile.",
                    Choices =
                    [
                        new() { Text = "Rejoins-moi.", NextId = "oui", Actions = [Recruit("maelle")] },
                        new() { Text = "Plus tard.", NextId = "non" },
                    ],
                },
                new() { Id = "oui", Speaker = "Sœur Maëlle", Text = "Que la lumière nous guide." },
                new() { Id = "non", Speaker = "Sœur Maëlle", Text = "Je prierai pour toi. Tu sais où me trouver." },
            ],
        },
        new()
        {
            Id = "rencontre_lyra",
            Nodes =
            [
                new() { Id = "1", Speaker = "", Text = "Sur la place du bourg, une jeune femme fait danser des flammes au bout de ses doigts.", NextId = "2" },
                new()
                {
                    Id = "2", Speaker = "Lyra",
                    Text = "Un aventurier ! Enfin ! Je m'ennuie à mourir ici. Tu m'emmènes ?",
                    Choices =
                    [
                        new() { Text = "Rejoins-moi.", NextId = "oui", Actions = [Recruit("lyra")] },
                        new() { Text = "Non merci.", NextId = "non" },
                    ],
                },
                new() { Id = "oui", Speaker = "Lyra", Text = "Génial ! Je prends mes affaires. Enfin, mon bâton." },
                new() { Id = "non", Speaker = "Lyra", Text = "Tant pis. Si tu changes d'avis, je suis sur la place." },
            ],
        },
        new()
        {
            Id = "rencontre_tobin",
            Nodes =
            [
                new() { Id = "1", Speaker = "", Text = "Une silhouette tombe d'un arbre juste devant toi, dague à la main.", NextId = "2" },
                new()
                {
                    Id = "2", Speaker = "Tobin",
                    Text = "Ta bourse ou... bon, vu ton épée, oublie. Tu vas vers la crypte ? J'ai toujours rêvé de piller une tombe royale.",
                    Choices =
                    [
                        new() { Text = "Rejoins-moi.", NextId = "oui", Actions = [Recruit("tobin")] },
                        new() { Text = "Passe ton chemin.", NextId = "non" },
                    ],
                },
                new() { Id = "oui", Speaker = "Tobin", Text = "Marché conclu. Je prends 10 % du butin. Bon, 5 %." },
                new() { Id = "non", Speaker = "", Text = "Tobin disparaît dans les fourrés en ricanant." },
            ],
        },
        new()
        {
            Id = "rumeurs",
            Nodes =
            [
                new() { Id = "1", Speaker = "Aubergiste", Text = "On raconte qu'un bandit balafré tient le Col des Corbeaux. Personne ne passe sans payer.", NextId = "2" },
                new() { Id = "2", Speaker = "Aubergiste", Text = "Et dans la forêt, méfie-toi des araignées. Certaines portent de drôles de bijoux dans leur toile." },
            ],
        },
        new()
        {
            Id = "intro_garrick",
            Nodes = [new() { Id = "1", Speaker = "Garrick le Balafré", Text = "Personne ne passe le col sans payer. Et toi, tu vas payer cher !" }],
        },
        new()
        {
            Id = "intro_morvath",
            Nodes =
            [
                new() { Id = "1", Speaker = "", Text = "Au fond de la crypte, une silhouette couronnée se lève de son trône d'os.", NextId = "2" },
                new() { Id = "2", Speaker = "Roi-Liche Morvath", Text = "Encore un héros... Ma collection d'os s'agrandit." },
            ],
        },
    ];
}
