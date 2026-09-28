using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Data;

/// <summary>
/// Contenu d'exemple (fantasy médiévale), utilisé tant qu'aucun Data/content.json n'est fourni.
/// Il se modifie aussi entièrement depuis l'éditeur du mode développeur.
/// </summary>
internal static class SampleContent
{
    public static GameContent Build() => new()
    {
        Title = "Chroniques de Valdor",
        Skills = [.. Skills()],
        Items = [.. Items()],
        Characters = [.. Characters()],
        Monsters = [.. Monsters()],
        Locations = [.. Locations()],
        Npcs = [.. Npcs()],
        Dialogues = [.. Dialogues()],
        Quests = [.. Quests()],
        Start = new()
        {
            LocationId = "havrefort",
            Gold = 100,
            Inventory = [new("potion", 3), new("ether", 1)],
            IntroDialogueId = "intro",
            Description = "Tu arrives à Havrefort, capitale de Valdor, où le Capitaine Hardin cherche des héros.",
        },
        ExtraStarts =
        [
            new()
            {
                Id = "exile", Name = "L'exilé de Sombrebois",
                Description = "Banni de la capitale, tu te réveilles au cœur de la forêt, presque sans rien. Ta réputation te précède.",
                LocationId = "foret_sombrebois", Gold = 15, Inventory = [new("potion", 1)],
                IntroDialogueId = "intro_exile", Hour = 5,
                Actions =
                [
                    new(ActionType.SetFlag, "exile"),
                    new(ActionType.SetKarma, "@heros", -10),
                    new(ActionType.SetVariable, "reputation", -20),
                ],
            },
        ],
        World = new() { CountryName = "Valdor" },
        Camp = Camp(),
        Variables =
        [
            new()
            {
                Id = "reputation", Name = "Réputation", Visible = true, Min = -100, Max = 100,
                Description = "Ce que les gens de Valdor pensent de l'équipe.",
            },
        ],
    };

    // ------------------------------------------------------------------ Campement

    private static CampSettings Camp() => new()
    {
        LeaderTitle = "Chef",
        Ranks =
        [
            new() { Id = "recrue", Name = "Recrue", Level = 0 },
            new() { Id = "soldat", Name = "Soldat", Level = 1 },
            new() { Id = "lieutenant", Name = "Lieutenant", Level = 2, Max = 1 },
        ],
        Tasks =
        [
            new()
            {
                Id = "rondes", Name = "Rondes", Icon = "ronde", DurationMinutes = 360,
                Description = "Surveiller les abords du camp.",
                Outcomes =
                [
                    new() { Weight = 5, Text = "%membre% fait sa ronde : rien à signaler." },
                    new()
                    {
                        Weight = 2, Text = "%membre% repousse des loups qui rôdaient autour du camp.",
                        Actions = [new(ActionType.AddVariable, "reputation", 1), new(ActionType.AddFriendship, "@membre", 1)],
                    },
                    new() { Weight = 1, Text = "%membre% s'est endormi pendant sa garde...", Actions = [new(ActionType.AddFriendship, "@membre", -1)] },
                ],
            },
            new()
            {
                Id = "chasse", Name = "Chasse", Icon = "chasse", DurationMinutes = 240, MinRankLevel = 1,
                Description = "Rapporter de quoi manger.",
                Outcomes =
                [
                    new()
                    {
                        Weight = 3, Text = "%membre% rapporte du gibier.",
                        Actions = [new(ActionType.GiveItem, "gibier", 1), new(ActionType.AddCampResource, "nourriture", 3)],
                    },
                    new() { Weight = 2, Text = "%membre% rentre bredouille." },
                    new() { Weight = 1, Text = "%membre% a trouvé une vieille bourse dans les bois.", Actions = [new(ActionType.GiveGold, amount: 15)] },
                ],
            },
            new()
            {
                Id = "cueillette", Name = "Cueillette", Icon = "cueillette", DurationMinutes = 180,
                Description = "Ramasser des herbes médicinales.",
                Outcomes =
                [
                    new() { Weight = 3, Text = "%membre% revient avec des herbes.", Actions = [new(ActionType.GiveItem, "herbes", 1)] },
                    new() { Weight = 2, Text = "%membre% ramène des baies.", Actions = [new(ActionType.AddCampResource, "nourriture", 1)] },
                    new() { Weight = 1, Text = "%membre% n'a rien trouvé d'utile." },
                ],
            },
            new()
            {
                Id = "bucheronnage", Name = "Bûcheronnage", Icon = "bois", DurationMinutes = 240,
                Description = "Couper du bois pour le feu et les constructions.",
                Outcomes =
                [
                    new() { Weight = 4, Text = "%membre% rapporte une brassée de bois.", Actions = [new(ActionType.AddCampResource, "bois", 3)] },
                    new() { Weight = 1, Text = "%membre% s'est blessé avec la hache.", Actions = [new(ActionType.AddCampResource, "bois", 1), new(ActionType.AddFriendship, "@membre", -1)] },
                ],
            },
            new()
            {
                Id = "forge", Name = "Forge", Icon = "forge", DurationMinutes = 480, MinRankLevel = 1, MaxWorkers = 1,
                Description = "Forger des armes pour le camp (il faut un atelier).",
                Conditions = [new(ConditionType.CampBuilt, "atelier")],
                Outcomes =
                [
                    new() { Weight = 2, Text = "%membre% vend quelques lames.", Actions = [new(ActionType.AddCampResource, "bois", -2), new(ActionType.GiveGold, amount: 25)] },
                    new() { Weight = 1, Text = "%membre% a raté sa trempe." },
                ],
            },
            new()
            {
                Id = "commandement", Name = "Diriger la garde", Icon = "garde", DurationMinutes = 480, MinRankLevel = 2, MaxWorkers = 1,
                Description = "Organiser les tours de garde : le camp gagne en réputation.",
                Outcomes = [new() { Text = "%membre% organise la garde d'une main de fer.", Actions = [new(ActionType.AddVariable, "reputation", 2)] }],
            },
            new()
            {
                Id = "repos", Name = "Veillée", Icon = "repos", DurationMinutes = 600,
                Description = "Partager le feu et les histoires.",
                Outcomes = [new() { Text = "%membre% raconte des histoires au coin du feu.", Actions = [new(ActionType.AddFriendship, "@membre", 2)] }],
            },
        ],
        Resources =
        [
            new() { Id = "nourriture", Name = "Nourriture", Icon = "cuisine", Initial = 10, Max = 60, DailyPerMember = 1, ShortageFriendshipLoss = 3 },
            new() { Id = "bois", Name = "Bois", Icon = "bois", Initial = 5, Max = 80 },
        ],
        Buildings =
        [
            new()
            {
                Id = "palissade", Name = "Palissade", Icon = "garde",
                Description = "Une enceinte de pieux : le camp est plus sûr et sa réputation grandit.",
                Costs = [new("bois", 15)],
                OnBuilt = [new(ActionType.AddVariable, "reputation", 3)],
            },
            new()
            {
                Id = "atelier", Name = "Atelier", Icon = "forge", GoldCost = 30,
                Description = "Un établi et une enclume : débloque la tâche « Forge ».",
                Costs = [new("bois", 10)],
            },
        ],
    };

    // ------------------------------------------------------------------ Compétences

    private static IEnumerable<SkillDef> Skills() =>
    [
        // Héros
        new() { Id = "frappe", Name = "Frappe", Description = "Attaque de base.", Power = 1.0 },
        new() { Id = "coup_puissant", Name = "Coup puissant", Description = "Un coup lourd.", ManaCost = 4, Power = 1.6 },
        new() { Id = "tourbillon", Name = "Tourbillon", Description = "Frappe tous les ennemis.", ManaCost = 8, Power = 0.8, Target = SkillTarget.AllEnemies },
        new() { Id = "trait_arcanique", Name = "Trait arcanique", Description = "Petit projectile magique.", Kind = SkillKind.Magical, Power = 0.9 },
        new() { Id = "boule_feu", Name = "Boule de feu", Description = "Brûle un ennemi.", Kind = SkillKind.Magical, ManaCost = 5, Power = 1.6, Element = "feu" },
        new()
        {
            Id = "blizzard", Name = "Blizzard", Description = "Gèle tous les ennemis et les ralentit.", Kind = SkillKind.Magical, ManaCost = 10, Power = 1.0,
            Target = SkillTarget.AllEnemies, Element = "glace",
            Effects = [new() { Type = EffectType.StatDown, Stat = StatKind.Speed, Amount = 30, Turns = 2 }],
        },
        new() { Id = "attaque_sournoise", Name = "Attaque sournoise", Description = "Vise les points faibles (souvent critique).", ManaCost = 3, Power = 1.4, CritChance = 40, CritMultiplier = 2 },
        new() { Id = "pluie_dagues", Name = "Pluie de dagues", Description = "Trois dagues sur chaque ennemi, pas toutes au but.", ManaCost = 6, Power = 0.35, Hits = 3, Accuracy = 75, Target = SkillTarget.AllEnemies },
        new()
        {
            Id = "cri_guerre", Name = "Cri de guerre", Description = "ATQ +30 % pendant 3 tours.", Kind = SkillKind.Status, Target = SkillTarget.Self, Cooldown = 3,
            Effects = [new() { Type = EffectType.StatUp, Stat = StatKind.Attack, Amount = 30, Turns = 3 }],
            UseText = "%lanceur% pousse un cri de guerre !",
        },
        new() { Id = "soin", Name = "Soin", Description = "Soigne un allié.", Kind = SkillKind.Heal, ManaCost = 4, Power = 1.2, Target = SkillTarget.SingleAlly },
        new() { Id = "priere", Name = "Prière", Description = "Soigne toute l'équipe.", Kind = SkillKind.Heal, ManaCost = 10, Power = 0.7, Target = SkillTarget.AllAllies },
        new()
        {
            Id = "egide", Name = "Égide", Description = "Bouclier de 30 points sur un allié et purification.", Kind = SkillKind.Status, ManaCost = 6,
            Target = SkillTarget.SingleAlly, Cooldown = 2,
            Effects = [new() { Type = EffectType.Cleanse }, new() { Type = EffectType.Shield, Amount = 30, Turns = 3 }],
        },
        new() { Id = "resurrection", Name = "Résurrection", Description = "Relève un allié K.O.", Kind = SkillKind.Revive, ManaCost = 15, Power = 1.0, FlatAmount = 20, Target = SkillTarget.SingleAlly, Cooldown = 4 },

        // Monstres
        new() { Id = "morsure", Name = "Morsure" },
        new() { Id = "griffe", Name = "Griffes", Power = 1.2 },
        new() { Id = "coup_massue", Name = "Coup de massue", Power = 1.1 },
        new() { Id = "entaille", Name = "Entaille" },
        new()
        {
            Id = "crachat_venin", Name = "Crachat de venin", Kind = SkillKind.Magical, Power = 0.7, ManaCost = 3, Element = "poison",
            Effects = [new() { Type = EffectType.Poison, Amount = 5, Turns = 3, Chance = 60 }],
        },
        new() { Id = "coup_os", Name = "Coup d'os", Power = 1.1 },
        new()
        {
            Id = "charge_brutale", Name = "Charge brutale", Power = 1.5, ManaCost = 5, Cooldown = 2,
            Effects = [new() { Type = EffectType.Stun, Turns = 1, Chance = 30 }],
        },
        new() { Id = "rayon_necrotique", Name = "Rayon nécrotique", Kind = SkillKind.Magical, Power = 1.3 },
        new() { Id = "vague_morte", Name = "Vague de mort", Kind = SkillKind.Magical, Power = 0.8, ManaCost = 8, Target = SkillTarget.AllEnemies },
    ];

    // ------------------------------------------------------------------ Objets

    private static IEnumerable<ItemDef> Items() =>
    [
        new() { Id = "potion", Name = "Potion", Description = "Rend 50 PV.", Type = ItemType.Consumable, Price = 20, HealHp = 50 },
        new() { Id = "grande_potion", Name = "Grande potion", Description = "Rend 150 PV.", Type = ItemType.Consumable, Price = 60, HealHp = 150 },
        new() { Id = "gibier", Name = "Gibier", Description = "Viande fraîche. Rend 25 PV.", Type = ItemType.Consumable, Price = 8, HealHp = 25 },
        new() { Id = "herbes", Name = "Herbes médicinales", Description = "Rend 15 PV et 10 PM.", Type = ItemType.Consumable, Price = 12, HealHp = 15, HealMana = 10 },
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
        new() { Id = "casque_fer", Name = "Casque de fer", Description = "Bosselé mais fiable.", Type = ItemType.Armor, ArmorSlot = EquipSlot.Head, Price = 35, Bonus = new(Defense: 2) },
        new() { Id = "gants_cuir", Name = "Gants de cuir", Description = "Une meilleure prise sur l'arme.", Type = ItemType.Armor, ArmorSlot = EquipSlot.Hands, Price = 25, Bonus = new(Attack: 1, Defense: 1) },
        new() { Id = "jambieres", Name = "Jambières", Description = "Protègent les genoux.", Type = ItemType.Armor, ArmorSlot = EquipSlot.Legs, Price = 30, Bonus = new(Defense: 2) },
        new() { Id = "bottes_voyage", Name = "Bottes de voyage", Description = "Légères et solides.", Type = ItemType.Armor, ArmorSlot = EquipSlot.Feet, Price = 30, Bonus = new(Speed: 2) },
        new() { Id = "amulette_cuivre", Name = "Amulette de cuivre", Description = "Un porte-bonheur de marché.", Type = ItemType.Armor, ArmorSlot = EquipSlot.Accessory, Price = 50, Bonus = new(MaxHp: 8, Magic: 1) },
        new() { Id = "bouclier_bois", Name = "Bouclier de bois", Description = "Arrête les coups, pas les flèches enflammées.", Type = ItemType.Armor, ArmorSlot = EquipSlot.Shield, Price = 40, Bonus = new(Defense: 3, Speed: -1) },

        new() { Id = "amulette_valdor", Name = "Amulette de Valdor", Description = "Relique royale. Donne vigueur et vivacité.", Type = ItemType.Relic, IsUnique = true, Bonus = new(MaxHp: 20, Speed: 3) },
        new() { Id = "anneau_sombrebois", Name = "Anneau de Sombrebois", Description = "Relique sylvestre au pouvoir étrange.", Type = ItemType.Relic, IsUnique = true, Bonus = new(Attack: 3, Magic: 3) },
        new() { Id = "fragment_couronne", Name = "Fragment de la Couronne", Description = "Morceau de la couronne perdue de Valdor.", Type = ItemType.Relic, RelicUsage = RelicUsage.Quest, IsUnique = true },
    ];

    // ------------------------------------------------------------------ Personnages

    private static IEnumerable<CharacterDef> Characters() =>
    [
        new()
        {
            Id = "aldric", Name = "Aldric", Class = "Chevalier", Title = "Chevalier errant", IsStarter = true,
            Description = "Chevalier sans seigneur, parti sur les routes pour sauver Valdor.",
            BaseStats = new(MaxHp: 120, MaxMana: 20, Attack: 14, Defense: 10, Magic: 4, Speed: 8),
            GrowthPerLevel = new(MaxHp: 12, MaxMana: 2, Attack: 2, Defense: 2, Magic: 0, Speed: 1),
            Skills = [new(1, "frappe"), new(1, "coup_puissant"), new(3, "cri_guerre"), new(4, "tourbillon")],
            StartingWeaponId = "epee_courte", StartingArmorId = "armure_cuir", StartingGearIds = ["bouclier_bois"],
        },
        new()
        {
            Id = "lyra", Name = "Lyra", Class = "Mage", Title = "Mage de Brume",
            Description = "Jeune mage qui s'ennuie à mourir à Bourg-de-Brume.",
            BaseStats = new(MaxHp: 70, MaxMana: 50, Attack: 5, Defense: 5, Magic: 16, Speed: 10),
            GrowthPerLevel = new(MaxHp: 7, MaxMana: 5, Attack: 1, Defense: 1, Magic: 3, Speed: 1),
            Skills = [new(1, "trait_arcanique"), new(1, "boule_feu"), new(5, "blizzard")],
            StartingWeaponId = "baton_chene", StartingArmorId = "robe_mage",
        },
        new()
        {
            Id = "tobin", Name = "Tobin", Class = "Voleur", Title = "Voleur des bois", IsStarter = true, StartId = "exile",
            Description = "Détrousseur de la forêt de Sombrebois, plus bavard que dangereux.",
            BaseStats = new(MaxHp: 85, MaxMana: 20, Attack: 12, Defense: 6, Magic: 3, Speed: 16),
            GrowthPerLevel = new(MaxHp: 9, MaxMana: 2, Attack: 2, Defense: 1, Magic: 0, Speed: 2),
            Skills = [new(1, "frappe"), new(1, "attaque_sournoise"), new(4, "pluie_dagues")],
            StartingWeaponId = "dague", StartingArmorId = "armure_cuir",
        },
        new()
        {
            Id = "maelle", Name = "Sœur Maëlle", Class = "Clerc", Title = "Sœur de l'Aube",
            Description = "Prêtresse de la chapelle de Havrefort. Les morts la craignent.",
            BaseStats = new(MaxHp: 90, MaxMana: 40, Attack: 8, Defense: 8, Magic: 12, Speed: 7),
            GrowthPerLevel = new(MaxHp: 10, MaxMana: 4, Attack: 1, Defense: 2, Magic: 2, Speed: 1),
            Skills = [new(1, "frappe"), new(1, "soin"), new(2, "egide"), new(4, "priere"), new(5, "resurrection")],
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
            Drops = [new("ether", 0.2), new("anneau_sombrebois", 0.03)], Resistances = [new("feu", 150), new("poison", 0)] },
        new() { Id = "harpie", Name = "Harpie", Description = "Hante le Col des Corbeaux.",
            Stats = new(MaxHp: 45, Attack: 12, Defense: 4, Speed: 15), SkillIds = ["griffe"], Xp = 12, Gold = 9 },
        new() { Id = "squelette", Name = "Squelette", Description = "Garde éternel de la Crypte oubliée.",
            Stats = new(MaxHp: 50, Attack: 12, Defense: 7, Speed: 6), SkillIds = ["coup_os"], Xp = 13, Gold = 10,
            Drops = [new("potion", 0.25)], Resistances = [new("glace", 50), new("poison", 0)] },
        new() { Id = "garrick", Name = "Garrick le Balafré", Description = "Chef des bandits du Col des Corbeaux.", IsBoss = true,
            Stats = new(MaxHp: 180, MaxMana: 30, Attack: 15, Defense: 8, Speed: 10), SkillIds = ["entaille", "charge_brutale"], Xp = 60, Gold = 120,
            Drops = [new("epee_longue", 0.5), new("grande_potion", 1.0)] },
        new() { Id = "morvath", Name = "Roi-Liche Morvath", Description = "Ancien roi de Valdor, revenu d'entre les morts.", IsBoss = true,
            Stats = new(MaxHp: 400, MaxMana: 80, Attack: 14, Defense: 10, Magic: 20, Speed: 9), SkillIds = ["rayon_necrotique", "vague_morte", "coup_os"], Xp = 200, Gold = 300,
            Drops = [new("fragment_couronne", 1.0), new("amulette_valdor", 1.0)],
            Resistances = [new("feu", 150), new("glace", 50), new("poison", -100)],
            BattleLines =
            [
                new() { Trigger = BattleTrigger.Start, Text = "Agenouillez-vous devant votre roi !" },
                new() { Trigger = BattleTrigger.HpBelow, Amount = 50, Text = "Impossible... Mes os se fendent ?!" },
                new() { Trigger = BattleTrigger.Down, Text = "Valdor... était... à moi..." },
            ] },
    ];

    // ------------------------------------------------------------------ Raccourcis

    private static Condition IfFlag(string flag) => new(ConditionType.FlagSet, flag);
    private static Condition IfQuestActive(string id) => new(ConditionType.QuestActive, id);
    private static Condition IfQuestDone(string id) => new(ConditionType.QuestCompleted, id);
    private static GameAction Recruit(string id) => new(ActionType.Recruit, id);
    private static GameAction StartQuest(string id) => new(ActionType.StartQuest, id);
    private static GameAction Karma(int amount, string who = "") => new(ActionType.AddKarma, who, amount);
    private static GameAction Friendship(string who, int amount) => new(ActionType.AddFriendship, who, amount);

    // ------------------------------------------------------------------ Lieux

    private static IEnumerable<LocationDef> Locations() =>
    [
        new()
        {
            Id = "havrefort", Name = "Havrefort", Type = LocationType.City,
            Description = "Capitale fortifiée du royaume de Valdor.",
            ConnectedIds = ["route_roi"],
            ShopItemIds = ["potion", "ether", "epee_courte", "epee_longue", "dague", "baton_chene", "masse", "armure_cuir", "cotte_mailles", "robe_mage", "casque_fer", "gants_cuir", "jambieres", "bottes_voyage", "amulette_cuivre", "bouclier_bois"],
            InnPrice = 10,
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
        },
        new()
        {
            Id = "foret_sombrebois", Name = "Forêt de Sombrebois", Type = LocationType.Wild,
            Description = "Forêt épaisse où la lumière peine à passer.",
            ConnectedIds = ["route_roi", "crypte"],
            EncounterChance = 0.5,
            RandomEncounters = [new(["araignee"], 3), new(["loup", "loup", "loup"], 1), new(["bandit"], 2), new(["araignee", "loup"], 1)],
            FirstVisitDialogueId = "rencontre_tobin",
        },
        new()
        {
            Id = "col_corbeaux", Name = "Col des Corbeaux", Type = LocationType.Wild,
            Description = "Passage montagneux battu par les vents.",
            ConnectedIds = ["bourg_brume"],
            EncounterChance = 0.45,
            RandomEncounters = [new(["harpie"], 3), new(["harpie", "harpie"], 1), new(["bandit", "bandit"], 2)],
            FixedBattle = new("garrick", ["garrick", "bandit"], "intro_garrick")
            {
                BattleLines =
                [
                    new() { Trigger = BattleTrigger.Turn, Amount = 3, Speaker = "Bandit", Text = "Chef, ils sont coriaces !" },
                    new() { Trigger = BattleTrigger.Victory, Text = "Les derniers bandits s'enfuient dans la montagne." },
                ],
            },
        },
        new()
        {
            Id = "crypte", Name = "Crypte oubliée", Type = LocationType.Dungeon,
            Description = "Tombeau des anciens rois. Quelque chose s'y est réveillé.",
            ConnectedIds = ["foret_sombrebois"],
            EncounterChance = 0.6,
            RandomEncounters = [new(["squelette"], 3), new(["squelette", "squelette"], 2), new(["squelette", "araignee"], 1)],
            FixedBattle = new("morvath", ["morvath"], "intro_morvath"),
            AccessConditions = [IfFlag("crypte_ouverte")],
            LockedMessage = "Une grille scellée bloque l'entrée. Le Capitaine Hardin en a peut-être la clé.",
        },
    ];

    // ------------------------------------------------------------------ PNJ

    private static IEnumerable<NpcDef> Npcs() =>
    [
        new()
        {
            Id = "capitaine_hardin", Name = "Capitaine Hardin", LocationId = "havrefort",
            Description = "Commandant de la garde de Havrefort.",
            ConditionalDialogues =
            [
                new("capitaine_fin", IfQuestDone("quete_crypte")),
                new("capitaine_attente", IfQuestActive("quete_crypte")),
            ],
            DefaultDialogueId = "capitaine",
        },
        new()
        {
            Id = "soeur_maelle", Name = "Sœur Maëlle", LocationId = "havrefort",
            Description = "Prêtresse de la chapelle.",
            VisibleConditions = [new(ConditionType.NotInParty, "maelle")],
            DefaultDialogueId = "recruter_maelle",
        },
        new()
        {
            Id = "fermier_joss", Name = "Fermier Joss", LocationId = "havrefort",
            Description = "Éleveur de moutons inquiet.",
            ConditionalDialogues =
            [
                new("joss_merci", IfQuestDone("chasse_loups")),
                new("joss_attente", IfQuestActive("chasse_loups")),
            ],
            DefaultDialogueId = "joss_quete",
        },
        new()
        {
            Id = "lyra_npc", Name = "Lyra", LocationId = "bourg_brume",
            Description = "Jeune mage qui s'ennuie sur la place du bourg.",
            VisibleConditions = [new(ConditionType.NotInParty, "lyra")],
            DefaultDialogueId = "rencontre_lyra",
        },
        new()
        {
            Id = "aubergiste", Name = "Aubergiste", LocationId = "bourg_brume",
            Description = "Connaît toutes les rumeurs du pays.",
            DefaultDialogueId = "rumeurs",
        },
        new()
        {
            Id = "herboriste", Name = "Mère Ortie", LocationId = "bourg_brume",
            Description = "Herboriste du bourg.",
            ConditionalDialogues =
            [
                new("herboriste_merci", IfQuestDone("remede")),
                new("herboriste_attente", IfQuestActive("remede")),
            ],
            DefaultDialogueId = "herboriste_quete",
        },
        new()
        {
            Id = "tobin_npc", Name = "Tobin", LocationId = "foret_sombrebois",
            Description = "Voleur perché dans les arbres.",
            VisibleConditions = [new(ConditionType.NotInParty, "tobin")],
            DefaultDialogueId = "rencontre_tobin",
        },
        new()
        {
            Id = "ravisseur", Name = "Chef des ravisseurs", LocationId = "foret_sombrebois",
            Description = "Un brigand nerveux qui garde un marchand ligoté.",
            VisibleConditions = [new(ConditionType.QuestAtStage, "rancon") { Arg2 = "ravisseurs" }],
            DefaultDialogueId = "rancon_ravisseur",
        },
        new()
        {
            Id = "olric", Name = "Olric le marchand", LocationId = "havrefort",
            Description = "Marchand de Havrefort, enlevé puis libéré.",
            VisibleConditions = [new(ConditionType.QuestEnding, "rancon")],
            DefaultDialogueId = "olric_retour",
        },
        new()
        {
            Id = "bran", Name = "Bran", Description = "Vieux chasseur bourru, fidèle au camp.",
            StartsInCamp = true, StartRankId = "soldat", BaseFriendship = 10,
            DefaultDialogueId = "camp_bran",
        },
        new()
        {
            Id = "mara", Name = "Mara", Description = "Jeune sentinelle, pressée de faire ses preuves.",
            StartsInCamp = true, StartRankId = "recrue",
            DefaultDialogueId = "camp_mara",
        },
    ];

    // ------------------------------------------------------------------ Quêtes

    private static IEnumerable<QuestDef> Quests() =>
    [
        new()
        {
            Id = "rumeurs", Name = "Les rumeurs de Havrefort",
            Description = "Trois pistes à suivre, dans l'ordre que tu veux.",
            Parts =
            [
                new() { Id = "marchand", Name = "Écouter le marchand", Objectives = [new() { Type = ObjectiveType.TalkTo, TargetId = "olric" }] },
                new() { Id = "fermier", Name = "Écouter le fermier", Objectives = [new() { Type = ObjectiveType.TalkTo, TargetId = "fermier_joss" }] },
                new()
                {
                    Id = "route", Name = "Vérifier la route", Journal = "On parle de silhouettes sur la Route du Roi.",
                    StartConditions = [new(ConditionType.QuestPartCompleted, "rumeurs") { Arg2 = "marchand" }],
                    Objectives = [new() { Type = ObjectiveType.Reach, TargetId = "route_roi" }],
                },
                new() { Id = "secret", Name = "Le secret de la chapelle", Optional = true, Objectives = [new() { Type = ObjectiveType.TalkTo, TargetId = "soeur_maelle" }] },
            ],
            Rewards = [new(ActionType.GiveXp, amount: 40), new(ActionType.GiveGold, amount: 30)],
        },
        new()
        {
            Id = "rancon", Name = "La rançon du marchand",
            Description = "Olric, un marchand de Havrefort, a été enlevé sur la route.",
            AutoStart = [new(ConditionType.Visited, "bourg_brume")],
            Rewards = [new(ActionType.GiveXp, amount: 40)],
            Stages =
            [
                new()
                {
                    Id = "enquete", Name = "Des rumeurs",
                    Journal = "On dit qu'un marchand a été enlevé. L'aubergiste de Bourg-de-Brume en sait sûrement plus.",
                    Objectives = [new() { Type = ObjectiveType.TalkTo, TargetId = "aubergiste", Description = "Interroger l'aubergiste" }],
                    Exits = [new() { Label = "Piste trouvée", NextStageId = "ravisseurs" }],
                },
                new()
                {
                    Id = "ravisseurs", Name = "Le camp des ravisseurs",
                    Journal = "Les ravisseurs se cachent dans la forêt de Sombrebois. Payer la rançon, ou attaquer ?",
                    Objectives = [new() { Type = ObjectiveType.Reach, TargetId = "foret_sombrebois" }],
                    Exits =
                    [
                        new() { Label = "Rançon payée", Conditions = [IfFlag("rancon_payee")], NextStageId = "paix" },
                        new() { Label = "Assaut", Conditions = [IfFlag("rancon_assaut")], NextStageId = "assaut" },
                        new() { Label = "Abandon", Conditions = [IfFlag("rancon_abandon")], NextStageId = "abandon" },
                    ],
                },
                new()
                {
                    Id = "assaut", Name = "L'assaut",
                    Journal = "Plus de discussion : il faut vaincre les ravisseurs.",
                    Objectives = [new() { Type = ObjectiveType.Defeat, TargetId = "bandit", Count = 2 }],
                    Exits = [new() { Label = "Victoire", NextStageId = "libere" }],
                },
                new()
                {
                    Id = "paix", Name = "Libéré contre rançon", IsEnding = true,
                    OnEnter = [Karma(5), new(ActionType.AddVariable, "reputation", 5)],
                },
                new()
                {
                    Id = "libere", Name = "Libéré par la force", IsEnding = true,
                    OnEnter = [new(ActionType.AddVariable, "reputation", 10), new(ActionType.GiveGold, amount: 30)],
                },
                new()
                {
                    Id = "abandon", Name = "Olric abandonné", IsEnding = true, Failure = true,
                    OnEnter = [Karma(-10), new(ActionType.AddVariable, "reputation", -10)],
                },
            ],
        },
        new()
        {
            Id = "quete_crypte", Name = "La Crypte oubliée",
            Description = "Le Capitaine Hardin demande de mettre fin aux morts-vivants de la Crypte oubliée.",
            Objectives =
            [
                new() { Type = ObjectiveType.Reach, TargetId = "crypte" },
                new() { Type = ObjectiveType.Defeat, TargetId = "morvath" },
                new() { Type = ObjectiveType.TalkTo, TargetId = "capitaine_hardin", Description = "Faire son rapport au Capitaine Hardin" },
            ],
            Rewards = [new(ActionType.GiveGold, amount: 300), new(ActionType.GiveXp, amount: 100), new(ActionType.SetFlag, "royaume_sauve")],
        },
        new()
        {
            Id = "chasse_loups", Name = "Chasse aux loups",
            Description = "Le Fermier Joss perd ses moutons à cause des loups de la Route du Roi.",
            Objectives =
            [
                new() { Type = ObjectiveType.Defeat, TargetId = "loup", Count = 3 },
                new() { Type = ObjectiveType.TalkTo, TargetId = "fermier_joss" },
            ],
            Rewards = [new(ActionType.GiveGold, amount: 60), new(ActionType.GiveItem, "potion", 2), new(ActionType.AddVariable, "reputation", 10)],
        },
        new()
        {
            Id = "remede", Name = "Le remède de Mère Ortie",
            Description = "L'herboriste a besoin d'un Éther pour préparer un remède.",
            Objectives = [new() { Type = ObjectiveType.Bring, TargetId = "ether", NpcId = "herboriste" }],
            Rewards = [new(ActionType.GiveItem, "grande_potion", 1), new(ActionType.GiveXp, amount: 30)],
        },
    ];

    // ------------------------------------------------------------------ Dialogues

    private static DialogueNode Line(string id, string speaker, string text, string? next = null) =>
        new() { Id = id, Speaker = speaker, Text = text, NextId = next };

    private static IEnumerable<DialogueDef> Dialogues() =>
    [
        new()
        {
            Id = "intro", Name = "Introduction",
            Nodes =
            [
                Line("1", "", "Le royaume de Valdor vacille. Depuis la disparition de la couronne, les morts ne dorment plus.", "2"),
                Line("2", "", "Tu arrives à Havrefort, la capitale, avec ton épée et quelques pièces en poche.", "3"),
                Line("3", "", "Le Capitaine Hardin cherche des volontaires. C'est peut-être le début de ton aventure."),
            ],
        },
        new()
        {
            Id = "capitaine", Name = "Capitaine : la crypte",
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
                    Text = "Brave. Voici la clé de la grille et de quoi t'équiper. Passe voir Sœur Maëlle à la chapelle : les morts la craignent.",
                    Actions = [new(ActionType.GiveGold, amount: 50), new(ActionType.SetFlag, "crypte_ouverte"), StartQuest("quete_crypte")],
                },
                Line("non", "Capitaine Hardin", "Reviens quand tu seras prêt."),
            ],
        },
        new()
        {
            Id = "capitaine_attente", Name = "Capitaine : en attente",
            Nodes = [Line("1", "Capitaine Hardin", "La crypte est toujours infestée. Courage, et reviens me faire ton rapport.")],
        },
        new()
        {
            Id = "capitaine_fin", Name = "Capitaine : victoire",
            Nodes = [Line("1", "Capitaine Hardin", "Tu as vaincu Morvath ! Tout Valdor te doit une fière chandelle, héros.")],
        },
        new()
        {
            Id = "recruter_maelle", Name = "Recruter Maëlle",
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
                Line("oui", "Sœur Maëlle", "Que la lumière nous guide."),
                Line("non", "Sœur Maëlle", "Je prierai pour toi. Tu sais où me trouver."),
            ],
        },
        new()
        {
            Id = "rencontre_lyra", Name = "Rencontre avec Lyra",
            Nodes =
            [
                Line("1", "", "Sur la place du bourg, une jeune femme fait danser des flammes au bout de ses doigts.", "2"),
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
                Line("oui", "Lyra", "Génial ! Je prends mes affaires. Enfin, mon bâton."),
                Line("non", "Lyra", "Tant pis. Si tu changes d'avis, je suis sur la place."),
            ],
        },
        new()
        {
            Id = "rencontre_tobin", Name = "Rencontre avec Tobin",
            Nodes =
            [
                Line("1", "", "Une silhouette tombe d'un arbre juste devant toi, dague à la main.", "2"),
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
                Line("oui", "Tobin", "Marché conclu. Je prends 10 % du butin. Bon, 5 %."),
                Line("non", "", "Tobin disparaît dans les fourrés en ricanant."),
            ],
        },
        new()
        {
            Id = "rumeurs", Name = "Rumeurs de l'auberge",
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "Aubergiste", NextId = "2",
                    Text = "On raconte qu'un bandit balafré tient le Col des Corbeaux. Personne ne passe sans payer.",
                    Variants =
                    [
                        new()
                        {
                            Conditions = [new(ConditionType.Speaker, "lyra")],
                            Text = "Lyra ! Toujours le nez dans tes grimoires ? Méfie-toi du bandit balafré du Col des Corbeaux, il n'aime pas les mages.",
                        },
                    ],
                },
                Line("2", "Aubergiste", "Et dans la forêt, méfie-toi des araignées. Certaines portent de drôles de bijoux dans leur toile."),
            ],
        },
        new()
        {
            Id = "joss_quete", Name = "Joss : les loups",
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "Fermier Joss",
                    Text = "Les loups de la Route du Roi me volent mes moutons ! Tu pourrais en abattre trois ?",
                    Choices =
                    [
                        new() { Text = "Compte sur moi.", NextId = "oui", Actions = [StartQuest("chasse_loups"), Karma(3), Friendship("fermier_joss", 10)] },
                        new() { Text = "Désolé, pas le temps.", NextId = "non", Actions = [Karma(-2), Friendship("fermier_joss", -5)] },
                    ],
                },
                new()
                {
                    Id = "oui", Speaker = "Fermier Joss", Text = "Merci ! Reviens me voir quand ce sera fait.",
                    Variants =
                    [
                        new()
                        {
                            Conditions = [new(ConditionType.Karma, amount: 15)],
                            Text = "On m'avait dit que %pj% avait bon cœur. Merci, reviens me voir quand ce sera fait !",
                        },
                    ],
                },
                Line("non", "Fermier Joss", "Mes pauvres moutons..."),
            ],
        },
        new()
        {
            Id = "joss_attente", Name = "Joss : en attente",
            Nodes = [Line("1", "Fermier Joss", "Alors, ces loups ? Il en reste encore sur la route, je les entends hurler.")],
        },
        new()
        {
            Id = "joss_merci", Name = "Joss : merci",
            Nodes = [Line("1", "Fermier Joss", "Plus un loup à l'horizon ! Mes moutons et moi te remercions.")],
        },
        new()
        {
            Id = "herboriste_quete", Name = "Mère Ortie : le remède",
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "Mère Ortie",
                    Text = "Il me manque un Éther pour mon remède. Tu m'en rapporterais un ?",
                    Choices =
                    [
                        new() { Text = "D'accord.", NextId = "oui", Actions = [StartQuest("remede")] },
                        new() { Text = "Non.", NextId = null },
                    ],
                },
                Line("oui", "Mère Ortie", "Merci, mon petit. Ça se vend dans les boutiques, si tu n'en as pas."),
            ],
        },
        new()
        {
            Id = "herboriste_attente", Name = "Mère Ortie : en attente",
            Nodes = [Line("1", "Mère Ortie", "Toujours pas d'Éther ? Mon chaudron refroidit...")],
        },
        new()
        {
            Id = "herboriste_merci", Name = "Mère Ortie : merci",
            Nodes = [Line("1", "Mère Ortie", "Le remède est prêt. Tiens, garde ceci pour tes blessures.")],
        },
        new()
        {
            Id = "intro_garrick", Name = "Garrick",
            Nodes = [Line("1", "Garrick le Balafré", "Personne ne passe le col sans payer. Et toi, tu vas payer cher !")],
        },
        new()
        {
            Id = "intro_exile", Name = "Introduction : l'exilé",
            Nodes =
            [
                Line("1", "", "L'aube filtre à travers les arbres de Sombrebois. Tes poignets portent encore la marque des fers.", "2"),
                Line("2", "", "Havrefort t'a banni. Mais Valdor est vaste, et chacun peut changer son destin."),
            ],
        },
        new()
        {
            Id = "rancon_ravisseur", Name = "Rançon : le ravisseur",
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "Chef des ravisseurs", Text = "Pas un pas de plus ! 50 pièces et le marchand est à toi.",
                    Choices =
                    [
                        new()
                        {
                            Text = "Voici l'or.", NextId = "paye", ShowLocked = true, LockedText = "Il te faut 50 pièces",
                            Conditions = [new(ConditionType.GoldAtLeast, amount: 50)],
                            Actions = [new(ActionType.TakeGold, amount: 50), new(ActionType.SetFlag, "rancon_payee")],
                        },
                        new()
                        {
                            Text = "Rends-le, ou tu le regretteras.", NextId = "assaut",
                            Actions = [new(ActionType.SetFlag, "rancon_assaut"), new(ActionType.StartBattle, "bandit,bandit")],
                        },
                        new() { Text = "Ce marchand ne me concerne pas.", NextId = "abandon", Actions = [new(ActionType.SetFlag, "rancon_abandon")] },
                    ],
                },
                Line("paye", "Chef des ravisseurs", "Un plaisir de faire affaire. Filez, avant que je change d'avis."),
                Line("assaut", "Chef des ravisseurs", "Les gars, à moi !"),
                Line("abandon", "", "Tu tournes les talons. Derrière toi, le marchand appelle à l'aide..."),
            ],
        },
        new()
        {
            Id = "olric_retour", Name = "Olric : retour",
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "Olric le marchand", Text = "Tu m'as sauvé la vie, %pj%. Ma boutique t'est ouverte.",
                    Variants =
                    [
                        new()
                        {
                            Conditions = [new(ConditionType.QuestEnding, "rancon") { Arg2 = "paix" }],
                            Text = "Tu as payé ma rançon de ta poche, %pj%... Je te rembourserai, c'est promis.",
                        },
                        new()
                        {
                            Conditions = [new(ConditionType.QuestFailed, "rancon")],
                            Text = "Tu m'as laissé à ces brigands, %pj%. Je ne l'oublierai pas.",
                        },
                    ],
                },
            ],
        },
        new()
        {
            Id = "camp_bran", Name = "Camp : Bran",
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "Bran", Text = "Le gibier se fait rare, %pj%. Mais tant que je tiendrai un arc, le camp mangera.",
                    Variants =
                    [
                        new()
                        {
                            Conditions = [new(ConditionType.Friendship, "bran", 30)],
                            Text = "Ah, %pj% ! Assieds-toi, je t'ai gardé le meilleur morceau.",
                        },
                    ],
                },
            ],
        },
        new()
        {
            Id = "camp_mara", Name = "Camp : Mara",
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "Mara", Text = "Chef ! Donne-moi une vraie mission, je suis prête !",
                    Choices =
                    [
                        new()
                        {
                            Text = "Tu es promue soldat.", NextId = "promue",
                            Conditions = [new(ConditionType.CampRank, "mara", 0) { Op = CompareOp.Equal }],
                            Actions = [new(ActionType.SetCampRank, "mara") { Arg2 = "soldat" }, new(ActionType.AddFriendship, "mara", 15)],
                        },
                        new() { Text = "Patience.", NextId = "patience" },
                    ],
                },
                Line("promue", "Mara", "Merci, %pj% ! Je ne te décevrai pas."),
                Line("patience", "Mara", "Toujours patience..."),
            ],
        },
        new()
        {
            Id = "intro_morvath", Name = "Morvath",
            Nodes =
            [
                Line("1", "", "Au fond de la crypte, une silhouette couronnée se lève de son trône d'os.", "2"),
                Line("2", "Roi-Liche Morvath", "Encore un héros... Ma collection d'os s'agrandit."),
            ],
        },
    ];
}
