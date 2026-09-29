using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using Condition = ProjetSKE.Core.Models.Condition;
using ProjetSKE.App.Ui;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Dev;

/// <summary>Listes de chaque catégorie de contenu.</summary>
public static class Editors
{
    private static GameContent C => DevState.Draft;

    public static Page CharacterList() => new EntityListPage<CharacterDef>(
        "PJ", C.Characters, x => x.Id, x => x.Name,
        (id, name) => new CharacterDef
        {
            Id = id, Name = name,
            BaseStats = new(MaxHp: 100, MaxMana: 20, Attack: 10, Defense: 8, Magic: 5, Speed: 8),
            GrowthPerLevel = new(MaxHp: 10, MaxMana: 2, Attack: 2, Defense: 1, Magic: 1, Speed: 1),
            Skills = C.Skills.Any(s => s.Id == "frappe") ? [new(1, "frappe")] : [],
        },
        x => new CharacterEditor(x),
        subtitle: x => (x.IsStarter ? "départ · " : "") + x.ClassAndTitle,
        help: "Personnages jouables. « Proposé au départ » = choisissable en début de partie ; les autres se recrutent par un dialogue (effet « Recruter un PJ »).");

    public static Page NpcList() => new EntityListPage<NpcDef>(
        "PNJ", C.Npcs, x => x.Id, x => x.Name,
        (id, name) => new NpcDef { Id = id, Name = name, LocationId = C.Start.LocationId },
        x => new NpcEditor(x),
        subtitle: x => C.Locations.FirstOrDefault(l => l.Id == x.LocationId)?.Name ?? "sans lieu",
        help: "Habitants placés dans un lieu. Leur dialogue peut changer selon l'avancement (quêtes, flags).");

    public static Page DialogueList() => new EntityListPage<DialogueDef>(
        "Histoire", C.Dialogues, x => x.Id, x => x.Name.Length > 0 ? x.Name : x.Id,
        (id, name) => new DialogueDef
        {
            Id = id, Name = name,
            Nodes = [new DialogueNode { Id = "debut", Speaker = "", Text = "..." }],
        },
        x => new DialogueEditor(x),
        subtitle: x => $"{x.Nodes.Count} réplique(s)",
        help: "Dialogues : répliques, choix, et effets (recruter, donner un objet, lancer une quête ou un combat...).");

    public static Page QuestList() => new EntityListPage<QuestDef>(
        "Quêtes", C.Quests, x => x.Id, x => x.Name,
        (id, name) => new QuestDef { Id = id, Name = name },
        x => new QuestEditor(x),
        subtitle: x => $"{x.Objectives.Count} objectif(s)",
        help: "Une quête démarre par l'effet « Démarrer une quête » (dans un dialogue). Ses objectifs se font dans l'ordre.");

    public static Page ItemList() => new EntityListPage<ItemDef>(
        "Objets", C.Items, x => x.Id, x => x.Name,
        (id, name) => new ItemDef { Id = id, Name = name, Type = ItemType.Consumable, Price = 10 },
        x => new ItemEditor(x, relics: false),
        subtitle: x => DevState.Name(x.Type),
        filter: x => x.Type != ItemType.Relic);

    public static Page RelicList() => new EntityListPage<ItemDef>(
        "Reliques", C.Items, x => x.Id, x => x.Name,
        (id, name) => new ItemDef { Id = id, Name = name, Type = ItemType.Relic, IsUnique = true },
        x => new ItemEditor(x, relics: true),
        subtitle: x => DevState.Name(x.RelicUsage),
        filter: x => x.Type == ItemType.Relic,
        help: "Objets uniques : équipables (bonus permanent) ou objets de quête.");

    public static Page MonsterList() => new EntityListPage<MonsterDef>(
        "Monstres", C.Monsters, x => x.Id, x => x.Name,
        (id, name) => new MonsterDef
        {
            Id = id, Name = name, Xp = 10, Gold = 5,
            Stats = new(MaxHp: 40, Attack: 8, Defense: 3, Speed: 8),
            SkillIds = C.Skills.Any(s => s.Id == "morsure") ? ["morsure"] : [],
        },
        x => new MonsterEditor(x),
        subtitle: x => (x.IsBoss ? "boss · " : "") + $"PV {x.Stats.MaxHp}");

    public static Page SkillList() => new EntityListPage<SkillDef>(
        "Compétences", C.Skills, x => x.Id, x => x.Name,
        (id, name) => new SkillDef { Id = id, Name = name },
        x => new SkillEditor(x),
        subtitle: x => DevState.Name(x.Kind) + (x.ManaCost > 0 ? $" · {x.ManaCost} PM" : "")
            + (x.Element.Length > 0 ? $" · {x.Element}" : "") + (x.Effects.Count > 0 ? $" · {x.Effects.Count} effet(s)" : "")
            + (x.Cooldown > 0 ? $" · recharge {x.Cooldown}" : ""));

    public static Page LocationList() => new EntityListPage<LocationDef>(
        "Lieux et carte", C.Locations, x => x.Id, x => x.Name,
        (id, name) => new LocationDef { Id = id, Name = name, Type = LocationType.Wild },
        x => new LocationEditor(x),
        subtitle: x => DevState.Name(x.Type) + $" · {x.ConnectedIds.Count} lien(s)",
        help: "La carte est une liste de lieux reliés entre eux.");
}

// ====================================================================== PJ

public sealed class CharacterEditor : EditorPage
{
    private readonly CharacterDef _x;
    public CharacterEditor(CharacterDef x) { _x = x; Render(); }
    protected override string PageTitle => "PJ : " + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(Editors.CharacterList());
    protected override Action Delete => () => DevState.Draft.Characters.Remove(_x);

    protected override void Build(Form f)
    {
        f.Note("Identifiant : " + _x.Id);
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Classe (ex : Chevalier, Mage)", _x.Class, v => _x.Class = v);
        f.TextField("Titre (ex : le Chevalier errant)", _x.Title, v => _x.Title = v);
        f.TextField("Description", _x.Description, v => _x.Description = v, multiline: true);
        f.BoolField("Proposé au départ", _x.IsStarter, v => _x.IsStarter = v, rerender: true);
        if (_x.IsStarter)
        {
            f.RefField("Départ de partie (aucun = départ principal)", _x.StartId,
                new[] { DevState.Draft.Start }.Concat(DevState.Draft.ExtraStarts).Select(s => (s.Id, s.Name)), v => _x.StartId = v);
            f.Note("Quand on joue ce héros : ceux qui l'accompagnent dès le début, et sa situation de départ (en plus de ce que donne le départ).");
            f.IdList("Compagnons au départ", _x.StartCompanions, DevState.Characters.Where(c => c.Id != _x.Id));
            f.Actions("Effets au lancement (quand on joue ce héros)", _x.StartActions);
        }
        f.StatsField("Stats de base (niveau 1)", _x.BaseStats);
        f.StatsField("Gain par niveau", _x.GrowthPerLevel);
        f.Header("Équipement de départ");
        f.RefField("Arme", _x.StartingWeaponId, DevState.Items(i => i.Type == ItemType.Weapon), v => _x.StartingWeaponId = v);
        f.RefField("Corps", _x.StartingArmorId, DevState.Items(i => i.Slot == EquipSlot.Armor), v => _x.StartingArmorId = v);
        f.RefField("Relique", _x.StartingRelicId, DevState.Items(i => i.Slot == EquipSlot.Relic), v => _x.StartingRelicId = v);
        f.IdList("Autres pièces (tête, mains, jambes, pieds, accessoire, bouclier)", _x.StartingGearIds,
            DevState.Items(i => i.Slot is { } sl && sl != EquipSlot.Weapon && sl != EquipSlot.Armor && sl != EquipSlot.Relic));
        f.ObjectList("Compétences", _x.Skills, () => new SkillUnlock(1, ""), (sf, s, _) =>
        {
            sf.RefField("Compétence", s.SkillId, DevState.Skills, v => s.SkillId = v ?? "", allowNone: false);
            sf.IntField("Apprise au niveau", s.Level, v => s.Level = v);
        }, "+ Compétence");
        f.Header("Personnalité");
        f.RefField("Portrait (banque d'images)", _x.PortraitId, DevState.Portraits, v => _x.PortraitId = v);
        Form.OptionalInt(f, "Karma de départ", _x.BaseKarma, v => _x.BaseKarma = v, $"par défaut : {DevState.Draft.Karma.Default}");
        Form.OptionalInt(f, "Amitié de départ envers les autres", _x.BaseFriendship, v => _x.BaseFriendship = v, $"par défaut : {DevState.Draft.Friendship.Default}");
        f.Resistances("Faiblesses et résistances", _x.Resistances);
        f.BattleLines("Répliques de combat", _x.BattleLines);
    }
}

// ====================================================================== PNJ

public sealed class NpcEditor : EditorPage
{
    private readonly NpcDef _x;
    public NpcEditor(NpcDef x) { _x = x; Render(); }
    protected override string PageTitle => "PNJ : " + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(Editors.NpcList());
    protected override Action Delete => () => DevState.Draft.Npcs.Remove(_x);

    protected override void Build(Form f)
    {
        f.Note("Identifiant : " + _x.Id);
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Description (encyclopédie)", _x.Description, v => _x.Description = v, multiline: true);
        f.RefField("Portrait (banque d'images)", _x.PortraitId, DevState.Portraits, v => _x.PortraitId = v);
        f.BoolField("Vit au campement dès le début", _x.StartsInCamp, v => _x.StartsInCamp = v, rerender: true);
        if (_x.StartsInCamp)
            f.RefField("Grade de départ (aucun = le plus bas)", _x.StartRankId, DevState.CampRanks, v => _x.StartRankId = v);
        f.RefField(_x.StartsInCamp ? "Lieu habituel (aucun = seulement au camp)" : "Lieu habituel", _x.LocationId, DevState.Locations, v => _x.LocationId = v ?? "", allowNone: _x.StartsInCamp);
        f.Note("Emploi du temps : le premier placement dont les conditions passent (heure, jour, flag...) remplace le lieu habituel.");
        f.ObjectList("Placements", _x.Placements, () => new NpcPlacement { LocationId = _x.LocationId }, (pf, p, _) =>
        {
            pf.RefField("Lieu", p.LocationId, DevState.Locations, v => p.LocationId = v ?? "", allowNone: false);
            pf.Conditions("Quand", p.Conditions);
        }, "+ Placement");
        Form.OptionalInt(f, "Amitié de départ envers l'équipe", _x.BaseFriendship, v => _x.BaseFriendship = v, $"par défaut : {DevState.Draft.Friendship.Default}");
        f.RefField("Dialogue par défaut", _x.DefaultDialogueId, DevState.Dialogues, v => _x.DefaultDialogueId = v);
        f.Note("Dialogues selon la situation : le premier dont les conditions sont remplies est joué, sinon le dialogue par défaut. "
            + "Condition « Qui parle » = dialogue spécial selon le PJ qui s'adresse au PNJ ; « Amitié », « Karma », « Entre deux heures »... pour le reste.");
        f.ObjectList("Dialogues conditionnels", _x.ConditionalDialogues, () => new NpcDialogue(), (sf, d, _) =>
        {
            sf.RefField("Dialogue", d.DialogueId, DevState.Dialogues, v => d.DialogueId = v ?? "", allowNone: false);
            sf.Conditions("Si", d.Conditions);
        }, "+ Dialogue conditionnel");
        f.Conditions("Visible seulement si", _x.VisibleConditions);

        f.Header("Combat");
        f.BoolField("Ce PNJ peut se battre", _x.Combat is not null, v =>
        {
            if (!v) { _x.Combat = null; return; }
            // Point de départ : les stats et compétences du premier monstre, à ajuster.
            var model = DevState.Draft.Monsters.FirstOrDefault();
            _x.Combat = new NpcCombat
            {
                Stats = model is null ? new StatBlock(MaxHp: 60, Attack: 10, Defense: 6, Magic: 6, Speed: 8)
                    : new StatBlock(model.Stats.MaxHp, model.Stats.MaxMana, model.Stats.Attack, model.Stats.Defense, model.Stats.Magic, model.Stats.Speed),
                SkillIds = model is null ? [] : [.. model.SkillIds],
                Xp = model?.Xp ?? 20,
                Gold = model?.Gold ?? 10,
            };
        }, rerender: true);
        if (_x.Combat is not { } c) return;
        f.Note($"On le combat avec l'effet « Combat : lancer » (il est dans la liste des adversaires sous « PNJ · {_x.Name} »), "
            + $"ou il attaque de lui-même (ci-dessous). Une fois vaincu, le flag « {GameSession.NpcBeatenFlag(_x.Id)} » est posé.");
        f.BoolField("Boss (fuite impossible)", c.IsBoss, v => c.IsBoss = v);
        f.StatsField("Stats", c.Stats);
        f.IntField("XP donnée", c.Xp, v => c.Xp = v);
        f.IntField("Or donné", c.Gold, v => c.Gold = v);
        f.IdList("Compétences (choisies au hasard en combat)", c.SkillIds, DevState.Skills);
        f.IdList("Se bat avec (alliés)", c.AllyIds, DevState.Monsters.Where(m => m.Id != _x.Id));
        f.ObjectList("Butin", c.Drops, () => new ItemDrop("", 0.1), (df, d, _) =>
        {
            df.RefField("Objet", d.ItemId, DevState.Items(), v => d.ItemId = v ?? "", allowNone: false);
            df.DoubleField("Chance (0 à 1, ex : 0.25 = 25 %)", d.Chance, v => d.Chance = v);
        }, "+ Butin");
        f.Resistances("Faiblesses et résistances", c.Resistances);
        f.BattleLines("Répliques de combat", c.BattleLines);
        f.RefField("Dialogue après une victoire de l'équipe", c.VictoryDialogueId, DevState.Dialogues, v => c.VictoryDialogueId = v);
        f.RefField("Dialogue après une défaite de l'équipe", c.DefeatDialogueId, DevState.Dialogues, v => c.DefeatDialogueId = v);

        f.BoolField("Attaque l'équipe quand elle arrive dans son lieu", c.Attacks, v => c.Attacks = v, rerender: true);
        if (!c.Attacks) return;
        f.Conditions("Attaque seulement si", c.AttackConditions);
        f.RefField("Dialogue avant l'attaque", c.AttackDialogueId, DevState.Dialogues, v => c.AttackDialogueId = v);
        f.BoolField("Attaque encore après avoir été vaincu", c.AttacksAgain, v => c.AttacksAgain = v);
    }
}

// ====================================================================== Histoire

// ====================================================================== Quêtes

public sealed class QuestEditor : EditorPage
{
    private readonly QuestDef _x;
    public QuestEditor(QuestDef x) { _x = x; Render(); }
    protected override string PageTitle => "Quête : " + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(Editors.QuestList());
    protected override Action Delete => () => DevState.Draft.Quests.Remove(_x);

    protected override void Build(Form f)
    {
        f.Note($"Identifiant : {_x.Id} — démarre par l'effet « Démarrer une quête », ou toute seule (ci-dessous).");
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Description", _x.Description, v => _x.Description = v, multiline: true);
        f.BoolField("Quête secrète (cachée du journal)", _x.Hidden, v => _x.Hidden = v);
        f.Conditions("Démarre toute seule quand", _x.AutoStart);

        // Forme de la quête : objectifs à la suite, étapes à embranchements, ou parties en parallèle.
        var mode = _x.IsStaged ? "etapes" : _x.HasParts ? "parties" : "simple";
        f.RefField("Forme de la quête", mode,
        [
            ("simple", "Simple : des objectifs à la suite"),
            ("etapes", "À étapes : embranchements, plusieurs fins"),
            ("parties", "En parties : plusieurs parties en parallèle, chacune son état"),
        ], v => SetMode(v ?? "simple"), allowNone: false, rerender: true);

        if (_x.IsStaged) BuildStages(f);
        else if (_x.HasParts) BuildParts(f);
        else f.Objectives("Objectifs (dans l'ordre ; aucun = la quête se termine seulement par l'effet « Quête : terminer »)", _x.Objectives);

        f.Actions(_x.IsStaged ? "Récompenses (toute fin réussie)" : _x.HasParts ? "Récompenses (quand la quête est réussie)" : "Récompenses", _x.Rewards);
    }

    /// <summary>Change la forme de la quête en gardant les objectifs déjà écrits.</summary>
    private void SetMode(string mode)
    {
        var objectives = _x.IsStaged ? _x.Stages[0].Objectives : _x.HasParts ? _x.Parts[0].Objectives : _x.Objectives;
        objectives = [.. objectives];
        _x.Objectives.Clear();
        _x.Stages.Clear();
        _x.Parts.Clear();
        switch (mode)
        {
            case "etapes": _x.Stages.Add(new QuestStage { Id = "debut", Name = "Début", Objectives = objectives }); break;
            case "parties": _x.Parts.Add(new QuestPart { Id = "partie1", Name = "Première partie", Objectives = objectives }); break;
            default: _x.Objectives.AddRange(objectives); break;
        }
    }

    private void BuildParts(Form f)
    {
        f.Note("Les parties se font en parallèle, dans n'importe quel ordre. Chacune a son état : pas commencée, en cours, terminée ou échouée. "
            + "Une partie commence avec la quête, ou quand ses conditions sont remplies (ou par l'effet « Quête : démarrer une partie »). "
            + "La quête est réussie quand toutes les parties obligatoires sont terminées, et échoue si l'une d'elles échoue. "
            + "Les conditions « Partie de quête : ... » permettent de lancer d'autres quêtes, dialogues, choix... selon l'état de chaque partie.");
        f.Header($"Parties ({_x.Parts.Count})");
        foreach (var part in _x.Parts)
        {
            var p = part;
            var details = new List<string> { p.Id, $"{p.Objectives.Count} objectif(s)" };
            details.Add(p.StartConditions.Count == 0 ? "dès le début" : "commence si " + string.Join(" et ", p.StartConditions.Select(DevState.Describe)));
            if (p.FailConditions.Count > 0) details.Add("échoue si " + string.Join(" et ", p.FailConditions.Select(DevState.Describe)));
            if (p.Optional) details.Add("facultative");
            f.Add(Panel(Row(
                Stack(Txt(p.Name.Length > 0 ? p.Name : p.Id, 15, Theme.Text, bold: true), Muted(string.Join(" · ", details), 11)),
                Form.SmallButton("Modifier", () => SkeApp.GoTo(new QuestPartEditor(_x, p))))));
        }
        f.Add(Btn("+ Partie", () =>
        {
            var part = new QuestPart { Id = DevState.NewId("partie", _x.Parts.Select(o => o.Id)), Name = "Nouvelle partie" };
            _x.Parts.Add(part);
            DevState.Touch();
            SkeApp.GoTo(new QuestPartEditor(_x, part));
        }));
    }

    private void BuildStages(Form f)
    {
        f.Note("La quête commence à la première étape. Dans chaque étape : remplir ses objectifs, puis le premier chemin dont les conditions "
            + "passent mène à l'étape suivante (un choix de dialogue qui pose un flag suffit à bifurquer). Une étape « fin » termine la quête, "
            + "réussie ou échouée. Chaque étape peut changer le monde en y entrant.");
        f.Header("Carte de la quête");
        for (var i = 0; i < _x.Stages.Count; i++)
        {
            var stage = _x.Stages[i];
            var st = stage;
            var title = $"{i + 1}. {(stage.Name.Length > 0 ? stage.Name : stage.Id)}";
            if (stage.IsEnding) title += stage.Failure ? "   ✗ FIN (échec)" : "   ✓ FIN";
            var box = Stack(Txt(title, 15, stage.IsEnding ? (stage.Failure ? Theme.Danger : Theme.Good) : Theme.Text, bold: true),
                Muted($"{stage.Id} · {stage.Objectives.Count} objectif(s) · {stage.OnEnter.Count} effet(s) à l'entrée", 11));
            foreach (var exit in stage.Exits)
            {
                var target = _x.Stages.FirstOrDefault(o => o.Id == exit.NextStageId);
                var cond = exit.Conditions.Count == 0 ? "toujours" : "si " + string.Join(" et ", exit.Conditions.Select(DevState.Describe));
                box.Add(Txt($"   → {(exit.Label.Length > 0 ? exit.Label + " : " : "")}{target?.Name ?? "⚠ " + exit.NextStageId} ({cond})", 12, Theme.Stone600));
            }
            if (!stage.IsEnding && stage.Exits.Count == 0) box.Add(Muted("   → aucune suite : la quête est réussie ici", 11));
            box.Add(Form.SmallButton("Modifier l'étape", () => SkeApp.GoTo(new QuestStageEditor(_x, st))));
            f.Add(Panel(box));
        }
        f.Add(Btn("+ Étape", () =>
        {
            var stage = new QuestStage { Id = DevState.NewId("etape", _x.Stages.Select(o => o.Id)), Name = "Nouvelle étape" };
            _x.Stages.Add(stage);
            DevState.Touch();
            SkeApp.GoTo(new QuestStageEditor(_x, stage));
        }));
    }
}

public sealed class QuestPartEditor : EditorPage
{
    private readonly QuestDef _quest;
    private readonly QuestPart _x;
    public QuestPartEditor(QuestDef quest, QuestPart x) { _quest = quest; _x = x; Render(); }
    protected override string PageTitle => $"{_quest.Name} › {_x.Name}";
    protected override void GoBack() => SkeApp.GoTo(new QuestEditor(_quest));
    protected override Action Delete => () => _quest.Parts.Remove(_x);

    protected override void Build(Form f)
    {
        f.TextField("Identifiant (pour les conditions et effets)", _x.Id, v => _x.Id = v);
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Journal (texte de la partie)", _x.Journal, v => _x.Journal = v, multiline: true);
        f.BoolField("Facultative (pas nécessaire pour réussir la quête)", _x.Optional, v => _x.Optional = v);
        f.Conditions("Commence quand (vide = dès le début de la quête)", _x.StartConditions);
        f.Objectives("Objectifs (dans l'ordre ; vide = terminée par un effet)", _x.Objectives);
        f.Conditions("Échoue si (pendant qu'elle est en cours)", _x.FailConditions);
        f.Actions("Effets quand la partie est terminée", _x.Rewards);
    }
}

public sealed class QuestStageEditor : EditorPage
{
    private readonly QuestDef _quest;
    private readonly QuestStage _x;
    public QuestStageEditor(QuestDef quest, QuestStage x) { _quest = quest; _x = x; Render(); }
    protected override string PageTitle => $"{_quest.Name} › {_x.Name}";
    protected override void GoBack() => SkeApp.GoTo(new QuestEditor(_quest));
    protected override Action? Delete => _quest.Stages.Count > 1 ? () => _quest.Stages.Remove(_x) : null;

    protected override void Build(Form f)
    {
        f.Note($"Identifiant : {_x.Id} — conditions : « Quête à l'étape », « Étape déjà passée », « Quête finie par » ; effet : « Quête : aller à l'étape ».");
        f.TextField("Nom de l'étape (affiché au joueur)", _x.Name, v => _x.Name = v);
        f.TextField("Récit du journal", _x.Journal, v => _x.Journal = v, multiline: true);
        f.BoolField("Étape finale (termine la quête)", _x.IsEnding, v => { _x.IsEnding = v; if (v) _x.Exits.Clear(); }, rerender: true);
        if (_x.IsEnding) f.BoolField("Fin en échec", _x.Failure, v => _x.Failure = v);
        f.Actions("Effets en entrant (le monde change)", _x.OnEnter);
        if (_x.IsEnding) return;
        f.Objectives("Objectifs de l'étape (dans l'ordre)", _x.Objectives);
        var stages = _quest.Stages.Where(o => o != _x).Select(o => (o.Id, (o.Name.Length > 0 ? o.Name : o.Id) + (o.IsEnding ? " (fin)" : "")));
        f.ObjectList("Chemins (testés dans l'ordre)", _x.Exits, () => new QuestExit(), (ef, e, _) =>
        {
            ef.TextField("Nom du chemin", e.Label, v => e.Label = v);
            ef.RefField("Mène à l'étape", e.NextStageId, stages, v => e.NextStageId = v ?? "", allowNone: false);
            ef.Conditions("Si (vide = toujours)", e.Conditions);
            ef.Actions("Effets en prenant ce chemin", e.Actions);
        }, "+ Chemin");
    }
}

// ====================================================================== Objets et reliques

public sealed class ItemEditor : EditorPage
{
    private readonly ItemDef _x;
    private readonly bool _relics;
    public ItemEditor(ItemDef x, bool relics) { _x = x; _relics = relics; Render(); }
    protected override string PageTitle => (_relics ? "Relique : " : "Objet : ") + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(_relics ? Editors.RelicList() : Editors.ItemList());
    protected override Action Delete => () => DevState.Draft.Items.Remove(_x);

    protected override void Build(Form f)
    {
        f.Note("Identifiant : " + _x.Id);
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Description", _x.Description, v => _x.Description = v, multiline: true);
        f.EnumField("Type", _x.Type, v => _x.Type = v, DevState.Name, rerender: true);
        if (_x.Type == ItemType.Relic)
            f.EnumField("Usage de la relique", _x.RelicUsage, v => _x.RelicUsage = v, DevState.Name, rerender: true);
        if (_x.Type == ItemType.Armor)
            f.RefField("Se porte sur", _x.ArmorSlot.ToString(), ItemDef.ArmorSlots.Select(x => (x.ToString(), SlotName(x))),
                v => { if (Enum.TryParse<EquipSlot>(v, out var slot)) _x.ArmorSlot = slot; }, allowNone: false);
        f.BoolField("Objet unique (un seul exemplaire)", _x.IsUnique, v => _x.IsUnique = v);
        f.IntField("Prix en boutique (0 = invendable)", _x.Price, v => _x.Price = v);
        if (_x.IsConsumable)
        {
            f.IntField("PV rendus", _x.HealHp, v => _x.HealHp = v);
            f.IntField("PM rendus", _x.HealMana, v => _x.HealMana = v);
        }
        if (_x.IsEquipable) f.StatsField("Bonus une fois équipé", _x.Bonus);
    }
}

// ====================================================================== Monstres

public sealed class MonsterEditor : EditorPage
{
    private readonly MonsterDef _x;
    public MonsterEditor(MonsterDef x) { _x = x; Render(); }
    protected override string PageTitle => "Monstre : " + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(Editors.MonsterList());
    protected override Action Delete => () => DevState.Draft.Monsters.Remove(_x);

    protected override void Build(Form f)
    {
        f.Note("Identifiant : " + _x.Id);
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Description (encyclopédie)", _x.Description, v => _x.Description = v, multiline: true);
        f.BoolField("Boss (fuite impossible)", _x.IsBoss, v => _x.IsBoss = v);
        f.RefField("Portrait (banque d'images)", _x.PortraitId, DevState.Portraits, v => _x.PortraitId = v);
        f.StatsField("Stats", _x.Stats);
        f.IntField("XP donnée", _x.Xp, v => _x.Xp = v);
        f.IntField("Or donné", _x.Gold, v => _x.Gold = v);
        f.IdList("Compétences (choisies au hasard en combat)", _x.SkillIds, DevState.Skills);
        f.ObjectList("Butin", _x.Drops, () => new ItemDrop("", 0.1), (df, d, _) =>
        {
            df.RefField("Objet", d.ItemId, DevState.Items(), v => d.ItemId = v ?? "", allowNone: false);
            df.DoubleField("Chance (0 à 1, ex : 0.25 = 25 %)", d.Chance, v => d.Chance = v);
        }, "+ Butin");
        f.Resistances("Faiblesses et résistances", _x.Resistances);
        f.BattleLines("Répliques de combat", _x.BattleLines);
    }
}

// ====================================================================== Compétences

public sealed class SkillEditor : EditorPage
{
    private readonly SkillDef _x;
    public SkillEditor(SkillDef x) { _x = x; Render(); }
    protected override string PageTitle => "Compétence : " + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(Editors.SkillList());
    protected override Action Delete => () => DevState.Draft.Skills.Remove(_x);

    protected override void Build(Form f)
    {
        f.Note("Identifiant : " + _x.Id);
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Description", _x.Description, v => _x.Description = v);
        f.EnumField("Type", _x.Kind, v => _x.Kind = v, DevState.Name, rerender: true);
        f.EnumField("Cible", _x.Target, v => _x.Target = v, DevState.Name);
        var offensive = _x.Kind is SkillKind.Physical or SkillKind.Magical;

        f.Header("Coûts");
        f.IntField("Coût en PM", _x.ManaCost, v => _x.ManaCost = v);
        f.IntField("Coût en PV (le lanceur garde 1 PV)", _x.HpCost, v => _x.HpCost = v);
        f.IntField("Recharge (tours avant de la réutiliser, 0 = aucune)", _x.Cooldown, v => _x.Cooldown = v);

        if (_x.Kind != SkillKind.Status)
        {
            f.Header(offensive ? "Dégâts" : _x.Kind == SkillKind.Revive ? "PV rendus à la résurrection" : "Soin");
            f.DoubleField(offensive ? "Puissance (× ATQ ou MAG ; 1 = normal)" : "Puissance (× MAG ; 0 = seulement le montant fixe)", _x.Power, v => _x.Power = v);
            f.IntField("Montant fixe ajouté", _x.FlatAmount, v => _x.FlatAmount = v);
        }
        if (offensive)
        {
            f.TextField("Élément (feu, glace... vide = aucun)", _x.Element, v => _x.Element = v.Trim());
            f.IntField("Nombre de coups", _x.Hits, v => _x.Hits = v);
            f.IntField("Précision (%)", _x.Accuracy, v => _x.Accuracy = v);
            f.IntField("Chance de critique (%)", _x.CritChance, v => _x.CritChance = v);
            f.DoubleField("Multiplicateur de critique", _x.CritMultiplier, v => _x.CritMultiplier = v);
            f.IntField("Vol de vie (% des dégâts rendus au lanceur)", _x.DrainPercent, v => _x.DrainPercent = v);
        }

        f.Note("Effets durables : ils s'appliquent aux cibles (ou au lanceur) et comptent en tours de celui qui les subit. "
            + "Un même effet de la même compétence est rafraîchi, pas cumulé.");
        f.ObjectList("Effets", _x.Effects, () => new SkillEffect(), (ef, e, _) =>
        {
            ef.EnumField("Effet", e.Type, v => e.Type = v, DevState.Name, rerender: true);
            if (e.Type is EffectType.StatUp or EffectType.StatDown) ef.EnumField("Statistique", e.Stat, v => e.Stat = v, DevState.Name);
            if (e.Type != EffectType.Cleanse && e.Type != EffectType.Stun)
                ef.IntField(e.Type switch
                {
                    EffectType.Poison or EffectType.Regen => "PV par tour",
                    EffectType.Shield => "Points absorbés",
                    _ => "Pourcentage",
                }, e.Amount, v => e.Amount = v);
            if (e.Type != EffectType.Cleanse) ef.IntField("Durée (tours)", e.Turns, v => e.Turns = v);
            ef.IntField("Chance (%)", e.Chance, v => e.Chance = v);
            ef.BoolField("Sur le lanceur (au lieu des cibles)", e.OnSelf, v => e.OnSelf = v);
        }, "+ Effet");

        f.Header("Journal");
        f.TextField("Texte à la place du texte automatique (%lanceur%, %sort%, %cible%)", _x.UseText, v => _x.UseText = v);
    }
}

// ====================================================================== Lieux

public sealed class LocationEditor : EditorPage
{
    private readonly LocationDef _x;
    public LocationEditor(LocationDef x) { _x = x; Render(); }
    protected override string PageTitle => "Lieu : " + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(Editors.LocationList());
    protected override Action Delete => () =>
    {
        DevState.Draft.Locations.Remove(_x);
        foreach (var l in DevState.Draft.Locations) l.ConnectedIds.Remove(_x.Id);
    };

    private LocationDef? Find(string id) => DevState.Draft.Locations.FirstOrDefault(l => l.Id == id);

    protected override void Build(Form f)
    {
        f.Note("Identifiant : " + _x.Id);
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Description", _x.Description, v => _x.Description = v, multiline: true);
        f.EnumField("Type", _x.Type, v => _x.Type = v, DevState.Name, rerender: true);

        f.Header("Carte");
        f.BoolField("Position fixée sur la carte hexagonale", _x.HexQ is not null, v =>
        {
            if (v) { _x.HexQ ??= 0; _x.HexR ??= 0; }
            else { _x.HexQ = null; _x.HexR = null; }
        }, rerender: true);
        if (_x.HexQ is { } q)
        {
            f.IntField("Colonne (q)", q, v => _x.HexQ = v);
            f.IntField("Ligne (r)", _x.HexR ?? 0, v => _x.HexR = v);
        }
        else f.Note("Sinon, le lieu est placé automatiquement à côté d'un lieu relié.");
        f.Note("Les liens sont créés dans les deux sens.");
        f.IdList("Lieux reliés", _x.ConnectedIds, DevState.Locations.Where(l => l.Id != _x.Id),
            onAdded: id => { if (Find(id) is { } other && !other.ConnectedIds.Contains(_x.Id)) other.ConnectedIds.Add(_x.Id); },
            onRemoved: id => Find(id)?.ConnectedIds.Remove(_x.Id));
        f.Conditions("Visible sur la carte seulement si (lieu secret)", _x.VisibleConditions);
        Form.OptionalInt(f, "Durée du voyage pour venir ici (minutes)", _x.TravelMinutes, v => _x.TravelMinutes = v, $"par défaut : {DevState.Draft.Time.TravelMinutes}");
        f.Conditions("Accessible seulement si", _x.AccessConditions);
        if (_x.AccessConditions.Count > 0)
            f.TextField("Message si bloqué", _x.LockedMessage, v => _x.LockedMessage = v);
        f.RefField("Dialogue à la première visite", _x.FirstVisitDialogueId, DevState.Dialogues, v => _x.FirstVisitDialogueId = v);

        if (_x.IsCity)
        {
            f.Header("Ville");
            f.IntField("Prix de l'auberge", _x.InnPrice, v => _x.InnPrice = v);
            f.IdList("Articles de la boutique", _x.ShopItemIds, DevState.Items());
        }

        f.Header("Combats");
        f.DoubleField("Chance de rencontre en arrivant (0 à 1)", _x.EncounterChance, v => _x.EncounterChance = v);
        f.ObjectList("Groupes de rencontre aléatoire", _x.RandomEncounters, () => new EncounterGroup(), (gf, g, _) =>
        {
            gf.IdList("Monstres", g.MonsterIds, DevState.Monsters);
            gf.IntField("Poids (plus = plus fréquent)", g.Weight, v => g.Weight = v);
        }, "+ Groupe");

        f.BoolField("Combat fixe (une fois, ex : boss)", _x.FixedBattle is not null, v =>
        {
            _x.FixedBattle = v ? new FixedBattleDef { Id = _x.Id } : null;
        }, rerender: true);
        if (_x.FixedBattle is { } fb)
        {
            f.IdList("Monstres du combat fixe", fb.MonsterIds, DevState.Monsters);
            f.RefField("Dialogue avant le combat", fb.IntroDialogueId, DevState.Dialogues, v => fb.IntroDialogueId = v);
            f.RefField("Dialogue après une victoire", fb.VictoryDialogueId, DevState.Dialogues, v => fb.VictoryDialogueId = v);
            f.RefField("Dialogue après une défaite", fb.DefeatDialogueId, DevState.Dialogues, v => fb.DefeatDialogueId = v);
            f.BattleLines("Répliques pendant ce combat", fb.BattleLines, fixedBattle: true);
        }
    }
}

// ====================================================================== Départ et équilibrage

/// <summary>Liste des départs : le principal et les autres (le joueur choisit après son héros).</summary>
public sealed class StartsPage : EditorPage
{
    public StartsPage() => Render();
    protected override string PageTitle => "Départs de partie";
    protected override void GoBack() => SkeApp.GoTo(new DevHomePage());

    protected override void Build(Form f)
    {
        var c = DevState.Draft;
        f.TextField("Titre du jeu", c.Title, v => c.Title = v);
        f.Header("Héros proposés et leur départ");
        f.Note("Le joueur ne choisit pas son départ : il dépend du héros (origine, prologue, lieu, équipement, monde de départ).");
        var starts = new[] { c.Start }.Concat(c.ExtraStarts).ToList();
        foreach (var ch in c.Characters)
        {
            var character = ch;
            f.BoolField(ch.Name, ch.IsStarter, v => character.IsStarter = v, rerender: true);
            if (ch.IsStarter)
                f.RefField("Départ de " + ch.Name, ch.StartId, starts.Select(s => (s.Id, s.Name)), v => character.StartId = v);
        }
        f.Header("Départs");
        var db = new GameDatabase(c);
        foreach (var start in starts)
        {
            var st = start;
            var main = st == c.Start;
            var heroes = db.HeroesOf(st).Select(h => h.Name).ToList();
            f.Add(Panel(Row(
                Stack(Txt(st.Name + (main ? "  (principal)" : ""), 15, Theme.Text, bold: true),
                    Muted($"{st.Id} · {DevState.Locations.FirstOrDefault(l => l.Id == st.LocationId).Name ?? st.LocationId}"
                        + (heroes.Count > 0 ? " · " + string.Join(", ", heroes) : " · aucun héros"))),
                Form.SmallButton("Modifier", () => SkeApp.GoTo(new StartEditor(st, main))))));
        }
        f.Add(Btn("+ Nouveau départ", async () =>
        {
            var name = await DisplayPromptAsync("Nouveau départ", "Nom :", "Créer", "Annuler");
            if (string.IsNullOrWhiteSpace(name)) return;
            var ids = new[] { c.Start.Id }.Concat(c.ExtraStarts.Select(x => x.Id));
            var start = new StartSettings { Id = DevState.NewId(name, ids), Name = name.Trim(), LocationId = c.Start.LocationId, Gold = c.Start.Gold };
            c.ExtraStarts.Add(start);
            DevState.Touch();
            SkeApp.GoTo(new StartEditor(start, main: false));
        }));
    }
}

public sealed class StartEditor : EditorPage
{
    private readonly StartSettings _s;
    private readonly bool _main;

    public StartEditor() : this(DevState.Draft.Start, main: true) { }

    public StartEditor(StartSettings s, bool main)
    {
        _s = s;
        _main = main;
        Render();
    }

    protected override string PageTitle => "Départ : " + _s.Name;
    protected override void GoBack() => SkeApp.GoTo(new StartsPage());
    protected override Action? Delete => _main ? null : () => DevState.Draft.ExtraStarts.Remove(_s);

    protected override void Build(Form f)
    {
        var s = _s;
        f.Note("Identifiant : " + s.Id + (_main ? " (départ principal)" : ""));
        f.TextField("Nom", s.Name, v => s.Name = v);
        f.TextField("Description (écran des héros)", s.Description, v => s.Description = v, multiline: true);
        f.Note("Quels héros commencent ici : se règle sur la fiche de chaque héros (ou dans la liste des départs).");
        f.RefField("Lieu de départ", s.LocationId, DevState.Locations, v => s.LocationId = v ?? "", allowNone: false);
        f.IntField("Or de départ", s.Gold, v => s.Gold = v);
        f.RefField("Dialogue d'introduction", s.IntroDialogueId, DevState.Dialogues, v => s.IntroDialogueId = v);
        f.IdList("Dialogues suivants (joués à la suite, dans l'ordre)", s.MoreIntroDialogueIds, DevState.Dialogues);
        f.IdList("Compagnons dès le début", s.Companions, DevState.Characters);
        Form.OptionalInt(f, "Jour de départ", s.Day, v => s.Day = v, $"par défaut : {DevState.Draft.Time.StartDay}");
        Form.OptionalInt(f, "Heure de départ", s.Hour, v => s.Hour = v, $"par défaut : {DevState.Draft.Time.StartHour}");
        f.ObjectList("Objets de départ", s.Inventory, () => new ItemStack("", 1), (sf, st, _) =>
        {
            sf.RefField("Objet", st.ItemId, DevState.Items(), v => st.ItemId = v ?? "", allowNone: false);
            sf.IntField("Quantité", st.Count, v => st.Count = v);
        }, "+ Objet");
        f.Note("Monde de départ : flags, variables, karma, quêtes déjà lancées, lieux révélés, PNJ au camp...");
        f.Actions("Effets au lancement", s.Actions);
    }
}

public sealed class BalanceEditor : EditorPage
{
    public BalanceEditor() => Render();
    protected override string PageTitle => "Équilibrage";
    protected override void GoBack() => SkeApp.GoTo(new DevHomePage());

    protected override void Build(Form f)
    {
        var b = DevState.Draft.Balance;
        f.Header("Progression");
        f.IntField("XP par niveau (XP requise = valeur × niveau)", b.XpPerLevel, v => b.XpPerLevel = v);
        f.IntField("Niveau maximum", b.MaxLevel, v => b.MaxLevel = v);
        f.Header("Économie");
        f.IntField("Prix de revente (% du prix d'achat)", b.SellPercent, v => b.SellPercent = v);
        f.Header("Combat");
        f.Note("Dégâts physiques = ATQ × puissance − DEF × facteur physique");
        f.DoubleField("Facteur de défense physique", b.PhysicalDefenseFactor, v => b.PhysicalDefenseFactor = v);
        f.Note("Dégâts magiques = MAG × puissance × multiplicateur − DEF × facteur magique");
        f.DoubleField("Multiplicateur magique", b.MagicMultiplier, v => b.MagicMultiplier = v);
        f.DoubleField("Facteur de défense magique", b.MagicDefenseFactor, v => b.MagicDefenseFactor = v);
        f.Note("Soin = MAG × puissance × multiplicateur + bonus");
        f.DoubleField("Multiplicateur de soin", b.HealMultiplier, v => b.HealMultiplier = v);
        f.IntField("Bonus de soin fixe", b.HealFlat, v => b.HealFlat = v);
        f.IntField("Variation aléatoire des dégâts (%)", b.DamageVariancePercent, v => b.DamageVariancePercent = v);
    }
}
