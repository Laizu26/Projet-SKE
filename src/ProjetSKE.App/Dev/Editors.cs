using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
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
        subtitle: x => (x.IsStarter ? "départ · " : "") + x.Title,
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
        subtitle: x => DevState.Name(x.Kind) + (x.ManaCost > 0 ? $" · {x.ManaCost} PM" : ""));

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
        f.TextField("Classe / titre", _x.Title, v => _x.Title = v);
        f.TextField("Description", _x.Description, v => _x.Description = v, multiline: true);
        f.BoolField("Proposé au départ", _x.IsStarter, v => _x.IsStarter = v);
        f.StatsField("Stats de base (niveau 1)", _x.BaseStats);
        f.StatsField("Gain par niveau", _x.GrowthPerLevel);
        f.Header("Équipement de départ");
        f.RefField("Arme", _x.StartingWeaponId, DevState.Items(i => i.Type == ItemType.Weapon), v => _x.StartingWeaponId = v);
        f.RefField("Armure", _x.StartingArmorId, DevState.Items(i => i.Type == ItemType.Armor), v => _x.StartingArmorId = v);
        f.RefField("Relique", _x.StartingRelicId, DevState.Items(i => i.Slot == EquipSlot.Relic), v => _x.StartingRelicId = v);
        f.ObjectList("Compétences", _x.Skills, () => new SkillUnlock(1, ""), (sf, s, _) =>
        {
            sf.RefField("Compétence", s.SkillId, DevState.Skills, v => s.SkillId = v ?? "", allowNone: false);
            sf.IntField("Apprise au niveau", s.Level, v => s.Level = v);
        }, "+ Compétence");
        f.Header("Personnalité");
        f.RefField("Portrait (banque d'images)", _x.PortraitId, DevState.Portraits, v => _x.PortraitId = v);
        Form.OptionalInt(f, "Karma de départ", _x.BaseKarma, v => _x.BaseKarma = v, $"par défaut : {DevState.Draft.Karma.Default}");
        Form.OptionalInt(f, "Amitié de départ envers les autres", _x.BaseFriendship, v => _x.BaseFriendship = v, $"par défaut : {DevState.Draft.Friendship.Default}");
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
    }
}

// ====================================================================== Histoire

public sealed class DialogueEditor : EditorPage
{
    private readonly DialogueDef _x;
    private bool _textMode;
    private string? _script;
    private List<string> _errors = [];

    public DialogueEditor(DialogueDef x) { _x = x; Render(); }
    protected override string PageTitle => "Dialogue : " + (_x.Name.Length > 0 ? _x.Name : _x.Id);
    protected override void GoBack() => SkeApp.GoTo(Editors.DialogueList());
    protected override Action Delete => () => DevState.Draft.Dialogues.Remove(_x);

    protected override void Build(Form f)
    {
        f.Note("Identifiant : " + _x.Id);
        f.TextField("Nom (pour s'y retrouver)", _x.Name, v => _x.Name = v);
        f.Add(ButtonRow(
            Btn("Formulaire", () => { _textMode = false; Render(); }, selected: !_textMode),
            Btn("Texte", () => { _textMode = true; _script = DialogueScript.Write(_x.Nodes); _errors = []; Render(); }, selected: _textMode)));

        if (_textMode) BuildText(f);
        else BuildForm(f);
    }

    private void BuildText(Form f)
    {
        _script ??= DialogueScript.Write(_x.Nodes);
        var editor = new Editor
        {
            Text = _script,
            TextColor = Theme.Text,
            BackgroundColor = Theme.Panel,
            FontSize = 14,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 300,
        };
        editor.TextChanged += (_, e) => _script = e.NewTextValue ?? "";
        f.Add(editor);
        f.Add(Btn("Appliquer le texte", () =>
        {
            var nodes = DialogueScript.Parse(_script ?? "", out _errors);
            if (_errors.Count == 0 || nodes.Count > 0)
            {
                _x.Nodes = nodes;
                DevState.Touch();
            }
            Render();
        }));
        if (_errors.Count > 0)
        {
            var box = Stack(Txt("À corriger :", 13, Theme.Danger, bold: true));
            foreach (var e in _errors) box.Add(Txt("• " + e, 12));
            f.Add(Panel(box));
        }
        else f.Note("Appuie sur « Appliquer » pour enregistrer le texte dans le dialogue.");
        f.Header("Aide");
        f.Add(Panel(Txt(DialogueScript.Help, 12, Theme.Muted)));
    }

    private void BuildForm(Form f)
    {
        f.Note("La première réplique est le début du dialogue. « Suivante » : réplique jouée après (si pas de choix).");
        f.ObjectList("Répliques", _x.Nodes, () => new DialogueNode { Id = DevState.NewId("r", _x.Nodes.Select(n => n.Id)) }, (nf, n, _) =>
        {
            // Répliques de ce dialogue, puis celles des autres dialogues (les histoires peuvent se croiser).
            var nodeIds = _x.Nodes.Select(o => (o.Id, o.Id + " : " + Short(o.Text)))
                .Concat(DevState.Draft.Dialogues.Where(d => d != _x).SelectMany(d =>
                    d.Nodes.Select((o, i) => (i == 0 ? d.Id + ":" : d.Id + ":" + o.Id, $"↪ {(d.Name.Length > 0 ? d.Name : d.Id)} › {(i == 0 ? "début" : o.Id)}"))))
                .ToList();
            nf.TextField("Étiquette", n.Id, v => n.Id = v);
            nf.TextField("Qui parle (vide = narration, %pj% = le PJ qui parle)", n.Speaker, v => n.Speaker = v);
            nf.TextField("Texte", n.Text, v => n.Text = v, multiline: true);
            nf.RefField("Portrait (aucun = celui de « Qui parle »)", n.PortraitId, DevState.Portraits, v => n.PortraitId = v);
            nf.ObjectList("Variantes du texte", n.Variants, () => new TextVariant { Text = n.Text }, (vf, v, _) =>
            {
                vf.Conditions("Si", v.Conditions);
                vf.TextField("Qui parle (vide = le même)", v.Speaker, x => v.Speaker = x);
                vf.TextField("Texte à la place", v.Text, x => v.Text = x, multiline: true);
            }, "+ Variante");
            if (n.Choices.Count == 0)
            {
                nf.RefField("Réplique suivante (aucun = fin)", n.NextId, nodeIds, v => n.NextId = v);
                nf.ObjectList("Aiguillages (testés avant la suite)", n.Branches, () => new DialogueBranch(), (bf, b, _) =>
                {
                    bf.Conditions("Si", b.Conditions);
                    bf.RefField("Aller à (aucun = fin)", b.NextId, nodeIds, v => b.NextId = v);
                }, "+ Aiguillage");
            }
            nf.ObjectList("Choix", n.Choices, () => new DialogueChoice { Text = "..." }, (cf, c, _) =>
            {
                cf.TextField("Texte du choix", c.Text, v => c.Text = v);
                cf.RefField("Mène à (aucun = fin)", c.NextId, nodeIds, v => c.NextId = v);
                cf.Conditions("Proposé seulement si", c.Conditions);
                if (c.Conditions.Count > 0)
                {
                    cf.BoolField("Sinon : l'afficher grisé", c.ShowLocked, v => c.ShowLocked = v, rerender: true);
                    if (c.ShowLocked) cf.TextField("Raison affichée", c.LockedText, v => c.LockedText = v);
                }
                cf.Actions("Effets du choix", c.Actions);
            }, "+ Choix");
            nf.Actions("Effets de la réplique", n.Actions);
        }, "+ Réplique");
    }

    private static string Short(string text) => text.Length > 30 ? text[..30] + "…" : text;
}

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
        f.Note($"Identifiant : {_x.Id} — à démarrer depuis un dialogue avec l'effet « Démarrer une quête ».");
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Description", _x.Description, v => _x.Description = v, multiline: true);
        f.BoolField("Quête secrète (cachée du journal)", _x.Hidden, v => _x.Hidden = v);
        f.ObjectList("Objectifs (dans l'ordre)", _x.Objectives, () => new QuestObjective { Type = ObjectiveType.TalkTo }, (of, o, i) =>
        {
            of.Note($"Étape {i + 1}");
            of.EnumField("Type", o.Type, v => { o.Type = v; o.TargetId = ""; }, DevState.Name, rerender: true);
            switch (o.Type)
            {
                case ObjectiveType.TalkTo:
                    of.RefField("PNJ", o.TargetId, DevState.Npcs, v => o.TargetId = v ?? "", allowNone: false);
                    break;
                case ObjectiveType.Defeat:
                    of.RefField("Monstre", o.TargetId, DevState.Monsters, v => o.TargetId = v ?? "", allowNone: false);
                    of.IntField("Nombre", o.Count, v => o.Count = v);
                    break;
                case ObjectiveType.Reach:
                    of.RefField("Lieu", o.TargetId, DevState.Locations, v => o.TargetId = v ?? "", allowNone: false);
                    break;
                default:
                    of.RefField("Objet", o.TargetId, DevState.Items(), v => o.TargetId = v ?? "", allowNone: false);
                    of.IntField("Quantité", o.Count, v => o.Count = v);
                    of.RefField("À remettre à (aucun = il suffit de l'avoir)", o.NpcId, DevState.Npcs, v => o.NpcId = v);
                    of.BoolField("Retirer l'objet du sac", o.ConsumeItems, v => o.ConsumeItems = v);
                    break;
            }
            of.TextField("Texte affiché (vide = automatique)", o.Description, v => o.Description = v);
        }, "+ Objectif");
        f.Actions("Récompenses", _x.Rewards);
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
        f.EnumField("Type", _x.Kind, v => _x.Kind = v, DevState.Name);
        f.EnumField("Cible", _x.Target, v => _x.Target = v, DevState.Name);
        f.IntField("Coût en PM", _x.ManaCost, v => _x.ManaCost = v);
        f.DoubleField("Puissance (1 = normal, 1.5 = +50 %)", _x.Power, v => _x.Power = v);
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

public sealed class StartEditor : EditorPage
{
    public StartEditor() => Render();
    protected override string PageTitle => "Départ de partie";
    protected override void GoBack() => SkeApp.GoTo(new DevHomePage());

    protected override void Build(Form f)
    {
        var s = DevState.Draft.Start;
        var c = DevState.Draft;
        f.TextField("Titre du jeu", c.Title, v => c.Title = v);
        f.RefField("Lieu de départ", s.LocationId, DevState.Locations, v => s.LocationId = v ?? "", allowNone: false);
        f.IntField("Or de départ", s.Gold, v => s.Gold = v);
        f.RefField("Dialogue d'introduction", s.IntroDialogueId, DevState.Dialogues, v => s.IntroDialogueId = v);
        f.ObjectList("Objets de départ", s.Inventory, () => new ItemStack("", 1), (sf, st, _) =>
        {
            sf.RefField("Objet", st.ItemId, DevState.Items(), v => st.ItemId = v ?? "", allowNone: false);
            sf.IntField("Quantité", st.Count, v => st.Count = v);
        }, "+ Objet");
        f.Header("Héros proposés");
        foreach (var ch in c.Characters)
        {
            var character = ch;
            f.BoolField(ch.Name, ch.IsStarter, v => character.IsStarter = v);
        }
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
