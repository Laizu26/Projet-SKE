using System.Globalization;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using Condition = ProjetSKE.Core.Models.Condition;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Dev;

/// <summary>
/// Constructeur de formulaires de l'éditeur : chaque champ lit et écrit directement dans l'objet édité.
/// Les champs qui changent la structure (ajout/suppression dans une liste, changement de type) redessinent la page.
/// </summary>
public sealed class Form
{
    private readonly Action _rerender;

    public VerticalStackLayout Root { get; } = new() { Spacing = 8 };

    public Form(Action rerender) => _rerender = rerender;

    private static void Changed() => DevState.Touch();

    public void Add(View view) => Root.Add(view);

    public void Header(string text) => Root.Add(Section(text));

    public void Note(string text) => Root.Add(Muted(text));

    private static View Labeled(string label, View field, VerticalStackLayout? into = null)
    {
        var box = into ?? new VerticalStackLayout { Spacing = 2 };
        box.Add(Muted(label));
        box.Add(field);
        return box;
    }

    private static Entry MakeEntry(string text, Keyboard? keyboard = null) => new()
    {
        Text = text,
        TextColor = Theme.Text,
        BackgroundColor = Theme.Panel,
        PlaceholderColor = Theme.Muted,
        FontSize = 14,
        Keyboard = keyboard ?? Keyboard.Text,
    };

    private static Picker MakePicker(IList<string> items, int selected) => new()
    {
        ItemsSource = items.ToList(),
        SelectedIndex = selected,
        TextColor = Theme.Text,
        TitleColor = Theme.Muted,
        BackgroundColor = Theme.Panel,
        FontSize = 14,
    };

    // ------------------------------------------------------------------ Champs simples

    public View TextField(string label, string value, Action<string> set, bool multiline = false, bool add = true)
    {
        View field;
        if (multiline)
        {
            var editor = new Editor
            {
                Text = value,
                TextColor = Theme.Text,
                BackgroundColor = Theme.Panel,
                FontSize = 14,
                AutoSize = EditorAutoSizeOption.TextChanges,
                MinimumHeightRequest = 60,
            };
            editor.TextChanged += (_, e) => { set(e.NewTextValue ?? ""); Changed(); };
            field = editor;
        }
        else
        {
            var entry = MakeEntry(value);
            entry.TextChanged += (_, e) => { set(e.NewTextValue ?? ""); Changed(); };
            field = entry;
        }
        var view = Labeled(label, field);
        if (add) Root.Add(view);
        return view;
    }

    public View IntField(string label, int value, Action<int> set, bool add = true)
    {
        var entry = MakeEntry(value.ToString(CultureInfo.InvariantCulture), Keyboard.Numeric);
        entry.TextChanged += (_, e) =>
        {
            if (int.TryParse(e.NewTextValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) { set(v); Changed(); }
        };
        var view = Labeled(label, entry);
        if (add) Root.Add(view);
        return view;
    }

    /// <summary>
    /// Nombre à virgule (1,25 ou 1.25). Clavier texte : le clavier chiffres d'Android ne propose pas toujours
    /// de séparateur décimal. Une saisie invalide est signalée en rouge et n'est pas enregistrée.
    /// </summary>
    public View DoubleField(string label, double value, Action<double> set, bool add = true)
    {
        var entry = MakeEntry(value.ToString(CultureInfo.GetCultureInfo("fr-FR")), Keyboard.Default);
        entry.Placeholder = "ex : 1,25";
        entry.TextChanged += (_, e) =>
        {
            var text = (e.NewTextValue ?? "").Trim().Replace(',', '.').Replace(" ", "");
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                entry.TextColor = Theme.Text;
                set(v);
                Changed();
            }
            else if (text.Length > 0 && text != "-" && text != ".") entry.TextColor = Theme.Danger;
        };
        var view = Labeled(label, entry);
        if (add) Root.Add(view);
        return view;
    }

    public View BoolField(string label, bool value, Action<bool> set, bool rerender = false, bool add = true)
    {
        var toggle = new Switch { IsToggled = value, OnColor = Theme.ButtonSelected, ThumbColor = Theme.Accent };
        toggle.Toggled += (_, e) =>
        {
            set(e.Value);
            Changed();
            if (rerender) _rerender();
        };
        var view = Row(Txt(label, 14), toggle);
        if (add) Root.Add(view);
        return view;
    }

    public View EnumField<T>(string label, T value, Action<T> set, Func<T, string> name, bool rerender = false, bool add = true, bool sorted = false)
        where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        // Longues listes (effets, conditions) : par ordre alphabétique, les noms « Catégorie : ... » se regroupent.
        if (sorted) values = values.OrderBy(v => name(v), StringComparer.Create(new System.Globalization.CultureInfo("fr-FR"), true)).ToArray();
        var picker = MakePicker(values.Select(name).ToList(), Array.IndexOf(values, value));
        picker.SelectedIndexChanged += (_, _) =>
        {
            if (picker.SelectedIndex < 0) return;
            set(values[picker.SelectedIndex]);
            Changed();
            if (rerender) _rerender();
        };
        var view = Labeled(label, picker);
        if (add) Root.Add(view);
        return view;
    }

    /// <summary>Menu déroulant vers un autre élément du contenu (objet, lieu, dialogue...).</summary>
    public View RefField(string label, string? value, IEnumerable<(string Id, string Name)> options, Action<string?> set,
        bool allowNone = true, bool rerender = false, bool add = true, string? emptyHint = null)
    {
        var list = options.ToList();
        if (list.Count == 0 && !allowNone && string.IsNullOrEmpty(value))
        {
            // Liste vide : un sélecteur vide ne servirait à rien, on explique plutôt quoi faire.
            var empty = Labeled(label, IconRow(Icon(Ico.Info, 13, Theme.Gold600),
                Txt(emptyHint ?? "Rien à choisir pour l'instant : crée-en d'abord un dans l'éditeur.", 12, Theme.Stone600)));
            if (add) Root.Add(empty);
            return empty;
        }
        var ids = new List<string?>();
        var labels = new List<string>();
        if (allowNone) { ids.Add(null); labels.Add("(aucun)"); }
        foreach (var (id, name) in list) { ids.Add(id); labels.Add($"{name} ({id})"); }
        if (!string.IsNullOrEmpty(value) && !list.Any(o => o.Id == value))
        {
            ids.Add(value);
            labels.Add($"⚠ introuvable ({value})");
        }
        var picker = MakePicker(labels, Math.Max(0, ids.IndexOf(string.IsNullOrEmpty(value) ? null : value)));
        if (!allowNone && string.IsNullOrEmpty(value)) picker.SelectedIndex = -1;
        picker.Title = "Choisir...";
        picker.SelectedIndexChanged += (_, _) =>
        {
            if (picker.SelectedIndex < 0) return;
            set(ids[picker.SelectedIndex]);
            Changed();
            if (rerender) _rerender();
        };
        var view = Labeled(label, picker);
        if (add) Root.Add(view);
        return view;
    }

    public void StatsField(string label, StatBlock s)
    {
        var grid = new Grid { ColumnSpacing = 6, RowSpacing = 4 };
        for (var i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        (string, int, Action<int>)[] fields =
        [
            ("PV", s.MaxHp, v => s.MaxHp = v),
            ("PM", s.MaxMana, v => s.MaxMana = v),
            ("ATQ", s.Attack, v => s.Attack = v),
            ("DEF", s.Defense, v => s.Defense = v),
            ("MAG", s.Magic, v => s.Magic = v),
            ("VIT", s.Speed, v => s.Speed = v),
        ];
        for (var i = 0; i < fields.Length; i++)
        {
            var (name, value, set) = fields[i];
            grid.Add(IntField(name, value, set, add: false), i % 3, i / 3);
        }
        Root.Add(Labeled(label, grid));
    }

    /// <summary>Nombre facultatif : case « personnaliser » + champ (sinon la valeur par défaut s'applique).</summary>
    public static void OptionalInt(Form f, string label, int? value, Action<int?> set, string defaultText)
    {
        f.BoolField($"{label} ({defaultText})", value is not null, v => set(v ? value ?? 0 : null), rerender: true);
        if (value is { } current) f.IntField(label, current, v => set(v));
    }

    /// <summary>Liste de textes (jours, mois...), un par ligne.</summary>
    public void Lines(string label, List<string> items)
    {
        var editor = new Editor
        {
            Text = string.Join("\n", items),
            TextColor = Theme.Text,
            BackgroundColor = Theme.Panel,
            FontSize = 14,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 60,
        };
        editor.TextChanged += (_, e) =>
        {
            items.Clear();
            items.AddRange((e.NewTextValue ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
            Changed();
        };
        Root.Add(Labeled(label + " (un par ligne)", editor));
    }

    // ------------------------------------------------------------------ Listes

    /// <summary>Liste d'identifiants (ex : compétences d'un monstre, articles d'une boutique).</summary>
    public void IdList(string label, List<string> ids, IEnumerable<(string Id, string Name)> options,
        Action<string>? onAdded = null, Action<string>? onRemoved = null, VerticalStackLayout? into = null)
    {
        var target = into ?? Root;
        var list = options.ToList();
        var box = new VerticalStackLayout { Spacing = 4 };
        box.Add(Muted(label));
        for (var i = 0; i < ids.Count; i++)
        {
            var index = i;
            var name = list.FirstOrDefault(o => o.Id == ids[i]).Name ?? "⚠ introuvable";
            box.Add(Row(Txt($"{name} ({ids[i]})", 13), SmallButton("✕", () =>
            {
                var removed = ids[index];
                ids.RemoveAt(index);
                onRemoved?.Invoke(removed);
                Changed();
                _rerender();
            })));
        }
        var picker = MakePicker(list.Select(o => $"+ {o.Name} ({o.Id})").ToList(), -1);
        picker.Title = "+ Ajouter...";
        picker.SelectedIndexChanged += (_, _) =>
        {
            if (picker.SelectedIndex < 0) return;
            var id = list[picker.SelectedIndex].Id;
            ids.Add(id);
            onAdded?.Invoke(id);
            Changed();
            _rerender();
        };
        box.Add(picker);
        target.Add(Panel(box));
    }

    /// <summary>Liste d'objets éditables (répliques, objectifs, butins...), avec ajout et suppression.</summary>
    public void ObjectList<T>(string label, List<T> items, Func<T> create, Action<Form, T, int> edit, string addText = "+ Ajouter")
    {
        Root.Add(Section($"{label} ({items.Count})"));
        for (var i = 0; i < items.Count; i++)
        {
            var index = i;
            var sub = new Form(_rerender);
            edit(sub, items[i], i);
            var actions = new HorizontalStackLayout { Spacing = 4 };
            if (i > 0) actions.Add(SmallButton("▲", () => { (items[index - 1], items[index]) = (items[index], items[index - 1]); Changed(); _rerender(); }));
            actions.Add(SmallButton("✕ Supprimer", () => { items.RemoveAt(index); Changed(); _rerender(); }));
            sub.Root.Add(actions);
            Root.Add(Panel(sub.Root));
        }
        Root.Add(Btn(addText, () => { items.Add(create()); Changed(); _rerender(); }));
    }

    public static Button SmallButton(string text, Action onClick)
    {
        var b = Btn(text, onClick);
        b.FontSize = 12;
        b.MinimumHeightRequest = 32;
        b.Padding = new Thickness(8, 2);
        return b;
    }

    // ------------------------------------------------------------------ Conditions et actions

    /// <summary>Comparaison (« au moins », « au plus »...) suivie de la valeur.</summary>
    private void Compare(Condition c, string valueLabel)
    {
        EnumField("Comparaison", c.Op, v => c.Op = v, DevState.Name);
        IntField(valueLabel, c.Amount, v => c.Amount = v);
    }

    /// <summary>
    /// Liste de conditions : toutes doivent être vraies (ET). Pour « l'une ou l'autre », un groupe OU ;
    /// les groupes s'imbriquent (ex : OU [ être Aldric, ET [ être Lyra, karma ≥ 20 ] ]).
    /// </summary>
    public void Conditions(string label, List<Condition> conditions, bool anyOf = false)
    {
        Root.Add(Section($"{label} ({conditions.Count})"));
        var link = anyOf ? "OU" : "ET";
        if (conditions.Count >= 2)
            Root.Add(Muted(anyOf ? "Il suffit qu'UNE de ces conditions soit vraie (OU)." : "TOUTES ces conditions doivent être vraies (ET).", 11));
        for (var i = 0; i < conditions.Count; i++)
        {
            var index = i;
            if (i > 0)
            {
                // Le lien entre deux conditions, bien visible.
                var tag = Badge(link, anyOf ? Theme.Gold700 : Theme.Stone600);
                tag.HorizontalOptions = LayoutOptions.Center;
                Root.Add(tag);
            }
            var sub = new Form(_rerender);
            sub.ConditionFields(conditions[i]);
            var actions = new HorizontalStackLayout { Spacing = 4 };
            if (i > 0) actions.Add(SmallButton("▲", () => { (conditions[index - 1], conditions[index]) = (conditions[index], conditions[index - 1]); Changed(); _rerender(); }));
            actions.Add(SmallButton("✕ Supprimer", () => { conditions.RemoveAt(index); Changed(); _rerender(); }));
            sub.Root.Add(actions);
            Root.Add(Panel(sub.Root));
        }
        void Add(Condition c) { conditions.Add(c); Changed(); _rerender(); }
        // Deux lignes : sur téléphone, trois boutons côte à côte coupaient leur texte (« + » seul).
        Root.Add(Btn("+ Condition", () => Add(new Condition(ConditionType.FlagSet))));
        Root.Add(ButtonRow(
            Btn("+ Groupe OU", () => Add(new Condition(ConditionType.AnyOf) { Children = [] })),
            Btn("+ Groupe ET", () => Add(new Condition(ConditionType.AllOf) { Children = [] }))));
    }

    private void ConditionFields(Condition c)
    {
        EnumField("Type", c.Type, v =>
        {
            c.Type = v;
            c.Arg = "";
            c.Arg2 = "";
            c.Children = v is ConditionType.AnyOf or ConditionType.AllOf ? c.Children ?? [] : null;
        }, DevState.Name, rerender: true, sorted: true);
        switch (c.Type)
        {
            case ConditionType.FlagSet or ConditionType.FlagNotSet:
                TextField("Nom du flag", c.Arg, v => c.Arg = v);
                break;
            case ConditionType.QuestNotStarted or ConditionType.QuestActive or ConditionType.QuestCompleted:
                RefField("Quête", c.Arg, DevState.Quests, v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.HasItem:
                RefField("Objet", c.Arg, DevState.Items(), v => c.Arg = v ?? "", allowNone: false);
                IntField("Quantité", c.Amount, v => c.Amount = v);
                break;
            case ConditionType.InParty or ConditionType.NotInParty or ConditionType.Speaker or ConditionType.IsHero:
                RefField(c.Type switch
                {
                    ConditionType.Speaker => "Le PJ qui parle est",
                    ConditionType.IsHero => "Le joueur incarne",
                    _ => "Personnage",
                }, c.Arg, DevState.Characters, v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.Variable:
                RefField("Variable", c.Arg, DevState.Variables, v => c.Arg = v ?? "", allowNone: false);
                Compare(c, "Valeur");
                break;
            case ConditionType.Karma:
                RefField("Karma de", c.Arg, DevState.KarmaWho, v => c.Arg = v ?? "", allowNone: false);
                Compare(c, "Valeur");
                break;
            case ConditionType.HasPower:
                RefField("Pouvoir", c.Arg2, DevState.Powers, v => c.Arg2 = v ?? "", allowNone: false,
                    emptyHint: "Aucun pouvoir : crée-en un dans « Pouvoirs » (menu du mode dev).");
                RefField("Qui", c.Arg.Length > 0 ? c.Arg : "@parle", DevState.KarmaWho, v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.HasPassive:
                RefField("Passif", c.Arg2, DevState.Passives, v => c.Arg2 = v ?? "", allowNone: false,
                    emptyHint: "Aucun passif : crée-en un dans « Passifs » (menu du mode dev).");
                RefField("Qui", c.Arg.Length > 0 ? c.Arg : "@parle", DevState.KarmaWho, v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.Gauge:
                RefField("Jauge", c.Arg2, DevState.Gauges, v => c.Arg2 = v ?? "", allowNone: false,
                    emptyHint: "Aucune jauge : crée-en une dans « Jauges de personnage » (menu du mode dev).");
                RefField("De qui", c.Arg.Length > 0 ? c.Arg : "@parle", DevState.KarmaWho, v => c.Arg = v ?? "", allowNone: false);
                Compare(c, "Valeur");
                break;
            case ConditionType.Friendship:
                RefField("Amitié de", c.Arg, DevState.Persons, v => c.Arg = v ?? "", allowNone: false);
                RefField("Envers", c.Arg2, DevState.Toward, v => c.Arg2 = v ?? "", allowNone: false);
                Compare(c, "Valeur");
                break;
            case ConditionType.Gold or ConditionType.Level or ConditionType.PartySize or ConditionType.Day:
                Compare(c, "Valeur");
                break;
            case ConditionType.HourBetween:
                Note("De (inclus) à (exclu). Ex : 20 → 6 = la nuit.");
                IntField("De (heure)", c.Amount, v => c.Amount = v);
                IntField("À (heure)", c.Amount2, v => c.Amount2 = v);
                break;
            case ConditionType.Period:
                RefField("Moment", c.Arg, DevState.Draft.Time.Periods.Select(p => (p.Name, p.Name)).Distinct(), v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.WeekDay:
                RefField("Jour", c.Arg, DevState.Draft.Time.WeekDays.Select(d => (d, d)), v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.Month:
                RefField("Mois", c.Arg, DevState.Draft.Time.Months.Select(m => (m, m)), v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.AtLocation or ConditionType.Visited:
                RefField("Lieu", c.Arg, DevState.Locations, v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.MetNpc:
                RefField("PNJ", c.Arg, DevState.Npcs, v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.Chance:
                IntField("Chance (%)", c.Amount, v => c.Amount = v);
                break;
            case ConditionType.CampMember:
                RefField("Qui", c.Arg, DevState.CampWho, v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.CampRank:
                RefField("Qui", c.Arg, DevState.CampWho, v => c.Arg = v ?? "", allowNone: false);
                Note("Niveaux : " + string.Join(", ", DevState.Draft.Camp.Ranks.OrderBy(r => r.Level).Select(r => $"{r.Name} = {r.Level}")));
                Compare(c, "Niveau du grade");
                break;
            case ConditionType.CampTask:
                RefField("Qui", c.Arg, DevState.CampWho, v => c.Arg = v ?? "", allowNone: false);
                RefField("Tâche", c.Arg2, DevState.CampTasks, v => c.Arg2 = v ?? "", allowNone: false);
                break;
            case ConditionType.CampResource:
                RefField("Ressource", c.Arg, DevState.CampResources, v => c.Arg = v ?? "", allowNone: false);
                Compare(c, "Stock");
                break;
            case ConditionType.CampBuilt:
                RefField("Lieu du camp", c.Arg, DevState.CampBuildings, v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.QuestAtStage or ConditionType.QuestStageReached or ConditionType.QuestEnding:
                RefField("Quête", c.Arg, DevState.StagedQuests, v => { c.Arg = v ?? ""; c.Arg2 = ""; }, allowNone: false, rerender: true);
                RefField(c.Type == ConditionType.QuestEnding ? "Fin (aucune = n'importe quelle fin)" : "Étape", c.Arg2,
                    DevState.StagesOf(c.Arg, endingsOnly: c.Type == ConditionType.QuestEnding), v => c.Arg2 = v ?? "",
                    allowNone: c.Type == ConditionType.QuestEnding);
                break;
            case ConditionType.QuestFailed:
                RefField("Quête", c.Arg, DevState.Quests, v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.ChoiceMade:
                RefField("Dans le dialogue", c.Arg, DevState.Dialogues, v => { c.Arg = v ?? ""; c.Arg2 = ""; }, allowNone: false, rerender: true);
                RefField("Le joueur a choisi", c.Arg2, DevState.ChoicesOf(c.Arg), v => c.Arg2 = v ?? "", allowNone: false);
                Note("Vrai dès que ce choix a été fait (même dans une ancienne conversation). « Inverser » = ne l'a pas choisi.");
                break;
            case ConditionType.QuestPartNotStarted or ConditionType.QuestPartActive or ConditionType.QuestPartCompleted or ConditionType.QuestPartFailed:
                RefField("Quête", c.Arg, DevState.PartQuests, v => { c.Arg = v ?? ""; c.Arg2 = ""; }, allowNone: false, rerender: true,
                    emptyHint: "Aucune quête en plusieurs parties. Pour une quête normale, prends plutôt le type « Quête : en cours » (ou pas commencée, terminée). Sinon, dans la quête, choisis la forme « Parties en parallèle ».");
                RefField("Partie", c.Arg2, DevState.PartsOf(c.Arg), v => c.Arg2 = v ?? "", allowNone: false);
                break;
            case ConditionType.AnyOf or ConditionType.AllOf:
                c.Children ??= [];
                Conditions(c.Type == ConditionType.AnyOf ? "OU : au moins une de ces conditions" : "ET : toutes ces conditions", c.Children, anyOf: c.Type == ConditionType.AnyOf);
                break;
            default:
                IntField("Valeur", c.Amount, v => c.Amount = v);
                break;
        }
        BoolField("Inverser (« sauf si »)", c.Negate, v => c.Negate = v);
    }

    public void Actions(string label, List<GameAction> actions) =>
        ObjectList(label, actions, () => new GameAction(ActionType.SetFlag), (f, a, _) => f.ActionFields(a), "+ Effet");

    private void ActionFields(GameAction a)
    {
        EnumField("Effet", a.Type, v => { a.Type = v; a.Arg = ""; a.Arg2 = ""; a.Amount = 1; }, DevState.Name, rerender: true, sorted: true);
        switch (a.Type)
        {
            case ActionType.SetFlag or ActionType.ClearFlag:
                TextField("Nom du flag", a.Arg, v => a.Arg = v);
                break;
            case ActionType.Recruit or ActionType.LeaveParty:
                RefField("Personnage", a.Arg, DevState.Characters, v => a.Arg = v ?? "", allowNone: false);
                break;
            case ActionType.GiveItem or ActionType.TakeItem:
                RefField("Objet", a.Arg, DevState.Items(), v => a.Arg = v ?? "", allowNone: false);
                IntField("Quantité", a.Amount, v => a.Amount = v);
                break;
            case ActionType.GiveGold or ActionType.TakeGold or ActionType.GiveXp:
                IntField("Montant", a.Amount, v => a.Amount = v);
                break;
            case ActionType.SetQuestStage:
                RefField("Quête", a.Arg, DevState.StagedQuests, v => { a.Arg = v ?? ""; a.Arg2 = ""; }, allowNone: false, rerender: true);
                RefField("Étape", a.Arg2, DevState.StagesOf(a.Arg), v => a.Arg2 = v ?? "", allowNone: false);
                break;
            case ActionType.StartQuest or ActionType.CompleteQuest or ActionType.FailQuest:
                RefField("Quête", a.Arg, DevState.Quests, v => a.Arg = v ?? "", allowNone: false);
                break;
            case ActionType.UnlockFeature or ActionType.LockFeature:
                RefField(a.Type == ActionType.UnlockFeature ? "Menu à activer" : "Menu à désactiver", a.Arg,
                    Enum.GetValues<UiFeature>().Select(x => (x.ToString(), DevState.Name(x))), v => a.Arg = v ?? "", allowNone: false);
                break;
            case ActionType.EndTutorial:
                Note("Seulement pendant le prologue : quand l'écran est libre (fin du dialogue ou du combat), le joueur arrive au choix de son héros.");
                break;
            case ActionType.StartDialogue:
                RefField("Dialogue", a.Arg, DevState.Dialogues, v => a.Arg = v ?? "", allowNone: false);
                Note("Joué dès que l'écran est libre (après le dialogue ou le combat en cours).");
                break;
            case ActionType.StartQuestPart or ActionType.CompleteQuestPart or ActionType.FailQuestPart:
                RefField("Quête", a.Arg, DevState.PartQuests, v => { a.Arg = v ?? ""; a.Arg2 = ""; }, allowNone: false, rerender: true,
                    emptyHint: "Aucune quête en plusieurs parties. Pour une quête normale, prends plutôt l'effet « Quête : ajouter / terminer ». Sinon, dans la quête, choisis la forme « Parties en parallèle ».");
                RefField("Partie", a.Arg2, DevState.PartsOf(a.Arg), v => a.Arg2 = v ?? "", allowNone: false);
                break;
            case ActionType.ShowNpc or ActionType.HideNpc:
                RefField("PNJ", a.Arg, DevState.Npcs, v => a.Arg = v ?? "", allowNone: false);
                break;
            case ActionType.Teleport or ActionType.RevealLocation or ActionType.HideLocation:
                RefField("Lieu", a.Arg, DevState.Locations, v => a.Arg = v ?? "", allowNone: false);
                break;
            case ActionType.StartBattle:
                var ids = a.Arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                IdList("Monstres", ids, DevState.Monsters,
                    onAdded: _ => a.Arg = string.Join(",", ids),
                    onRemoved: _ => a.Arg = string.Join(",", ids));
                break;
            case ActionType.SetVariable or ActionType.AddVariable:
                RefField("Variable", a.Arg, DevState.Variables, v => a.Arg = v ?? "", allowNone: false);
                IntField(a.Type == ActionType.AddVariable ? "Ajouter (négatif = retirer)" : "Nouvelle valeur", a.Amount, v => a.Amount = v);
                break;
            case ActionType.GivePower or ActionType.RemovePower:
                RefField("Pouvoir", a.Arg, DevState.Powers, v => a.Arg = v ?? "", allowNone: false,
                    emptyHint: "Aucun pouvoir : crée-en un dans « Pouvoirs » (menu du mode dev).");
                RefField("À qui", a.Arg2.Length > 0 ? a.Arg2 : "@parle", DevState.KarmaWho, v => a.Arg2 = v ?? "", allowNone: false);
                break;
            case ActionType.GivePassive or ActionType.RemovePassive:
                RefField("Passif", a.Arg, DevState.Passives, v => a.Arg = v ?? "", allowNone: false,
                    emptyHint: "Aucun passif : crée-en un dans « Passifs » (menu du mode dev).");
                RefField("À qui", a.Arg2.Length > 0 ? a.Arg2 : "@parle", DevState.KarmaWho, v => a.Arg2 = v ?? "", allowNone: false);
                break;
            case ActionType.AddGauge or ActionType.SetGauge:
                RefField("Jauge", a.Arg, DevState.Gauges, v => a.Arg = v ?? "", allowNone: false,
                    emptyHint: "Aucune jauge : crée-en une dans « Jauges de personnage » (menu du mode dev).");
                RefField("De qui", a.Arg2.Length > 0 ? a.Arg2 : "@parle", DevState.KarmaWho, v => a.Arg2 = v ?? "", allowNone: false);
                IntField(a.Type == ActionType.AddGauge ? "Ajouter (négatif = retirer)" : "Nouvelle valeur", a.Amount, v => a.Amount = v);
                break;
            case ActionType.AddKarma or ActionType.SetKarma:
                RefField("Karma de", a.Arg, DevState.KarmaWho, v => a.Arg = v ?? "", allowNone: false);
                IntField(a.Type == ActionType.AddKarma ? "Ajouter (négatif = retirer)" : "Nouvelle valeur", a.Amount, v => a.Amount = v);
                break;
            case ActionType.AddFriendship or ActionType.SetFriendship:
                RefField("Amitié de", a.Arg, DevState.Persons, v => a.Arg = v ?? "", allowNone: false);
                RefField("Envers", a.Arg2, DevState.Toward, v => a.Arg2 = v ?? "", allowNone: false);
                IntField(a.Type == ActionType.AddFriendship ? "Ajouter (négatif = retirer)" : "Nouvelle valeur", a.Amount, v => a.Amount = v);
                break;
            case ActionType.AdvanceTime:
                IntField("Minutes (60 = 1 h, 1440 = 1 jour)", a.Amount, v => a.Amount = v);
                break;
            case ActionType.WaitUntilHour:
                IntField("Attendre jusqu'à (heure)", a.Amount, v => a.Amount = v);
                break;
            case ActionType.ShowMessage:
                TextField("Message (balises %pj%, %heure%... possibles)", a.Arg, v => a.Arg = v, multiline: true);
                break;
            case ActionType.JoinCamp:
                RefField("Qui", a.Arg, DevState.Persons, v => a.Arg = v ?? "", allowNone: false);
                RefField("Grade (aucun = le plus bas)", a.Arg2, DevState.CampRanks, v => a.Arg2 = v ?? "");
                break;
            case ActionType.LeaveCamp:
                RefField("Qui", a.Arg, DevState.CampWho, v => a.Arg = v ?? "", allowNone: false);
                break;
            case ActionType.SetCampRank:
                RefField("Qui", a.Arg, DevState.CampWho, v => a.Arg = v ?? "", allowNone: false);
                RefField("Nouveau grade", a.Arg2, DevState.CampRanks, v => a.Arg2 = v ?? "", allowNone: false);
                break;
            case ActionType.SetCampTask:
                RefField("Qui", a.Arg, DevState.CampWho, v => a.Arg = v ?? "", allowNone: false);
                RefField("Tâche (aucune = repos)", a.Arg2, DevState.CampTasks, v => a.Arg2 = v ?? "");
                break;
            case ActionType.AddCampResource:
                RefField("Ressource", a.Arg, DevState.CampResources, v => a.Arg = v ?? "", allowNone: false);
                IntField("Ajouter (négatif = retirer)", a.Amount, v => a.Amount = v);
                break;
            case ActionType.BuildCampBuilding:
                RefField("Lieu du camp", a.Arg, DevState.CampBuildings, v => a.Arg = v ?? "", allowNone: false);
                break;
            case ActionType.MoveNpc:
                RefField("PNJ", a.Arg, DevState.Npcs, v => a.Arg = v ?? "", allowNone: false);
                RefField("Vers (aucun = revient à sa place)", a.Arg2, DevState.Locations, v => a.Arg2 = v ?? "");
                break;
        }
    }

    /// <summary>Faiblesses et résistances aux éléments des compétences.</summary>
    public void Resistances(string label, List<ElementModifier> list)
    {
        Note("100 = normal, 200 = faiblesse (dégâts ×2), 50 = résistance, 0 = immunité, négatif = absorbe (soigne).");
        ObjectList(label, list, () => new ElementModifier("feu", 150), (rf, r, _) =>
        {
            var known = DevState.Draft.Skills.Select(s => s.Element).Where(e => e.Length > 0).Distinct().ToList();
            rf.TextField(known.Count > 0 ? $"Élément (utilisés : {string.Join(", ", known)})" : "Élément", r.Element, v => r.Element = v.Trim());
            rf.IntField("Pourcentage", r.Percent, v => r.Percent = v);
        }, "+ Élément");
    }

    /// <summary>Objectifs de quête (parler, vaincre, aller, apporter).</summary>
    public void Objectives(string label, List<QuestObjective> objectives) =>
        ObjectList(label, objectives, () => new QuestObjective { Type = ObjectiveType.TalkTo }, (of, o, i) =>
        {
            of.Note($"Objectif {i + 1}");
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

    /// <summary>Répliques de combat (monstre, PJ ou combat fixe).</summary>
    public void BattleLines(string label, List<BattleLine> lines, bool fixedBattle = false) =>
        ObjectList(label, lines, () => new BattleLine { Trigger = BattleTrigger.Start }, (f, l, _) =>
        {
            f.EnumField("Moment", l.Trigger, v => l.Trigger = v, DevState.Name, rerender: true);
            if (l.Trigger == BattleTrigger.Turn) f.IntField("Tour n°", l.Amount, v => l.Amount = v);
            if (l.Trigger == BattleTrigger.HpBelow) f.IntField("PV sous (%)", l.Amount, v => l.Amount = v);
            f.TextField(fixedBattle ? "Qui parle (vide = narration)" : "Qui parle (vide = lui-même)", l.Speaker, v => l.Speaker = v);
            f.TextField("Réplique", l.Text, v => l.Text = v, multiline: true);
            f.IntField("Chance (%)", l.Chance, v => l.Chance = v);
            f.Conditions("Seulement si", l.Conditions);
            f.Actions("Effets", l.Actions);
        }, "+ Réplique de combat");
}
