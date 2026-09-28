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

    public View DoubleField(string label, double value, Action<double> set, bool add = true)
    {
        var entry = MakeEntry(value.ToString(CultureInfo.InvariantCulture), Keyboard.Numeric);
        entry.TextChanged += (_, e) =>
        {
            var text = (e.NewTextValue ?? "").Replace(',', '.');
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) { set(v); Changed(); }
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

    public View EnumField<T>(string label, T value, Action<T> set, Func<T, string> name, bool rerender = false, bool add = true)
        where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
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
        bool allowNone = true, bool rerender = false, bool add = true)
    {
        var list = options.ToList();
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

    public void Conditions(string label, List<Condition> conditions) =>
        ObjectList(label, conditions, () => new Condition(ConditionType.FlagSet), (f, c, _) => f.ConditionFields(c), "+ Condition");

    private void ConditionFields(Condition c)
    {
        EnumField("Type", c.Type, v =>
        {
            c.Type = v;
            c.Arg = "";
            c.Arg2 = "";
            c.Children = v is ConditionType.AnyOf or ConditionType.AllOf ? c.Children ?? [] : null;
        }, DevState.Name, rerender: true);
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
            case ConditionType.InParty or ConditionType.NotInParty or ConditionType.Speaker:
                RefField(c.Type == ConditionType.Speaker ? "Le PJ qui parle est" : "Personnage", c.Arg, DevState.Characters, v => c.Arg = v ?? "", allowNone: false);
                break;
            case ConditionType.Variable:
                RefField("Variable", c.Arg, DevState.Variables, v => c.Arg = v ?? "", allowNone: false);
                Compare(c, "Valeur");
                break;
            case ConditionType.Karma:
                RefField("Karma de", c.Arg, DevState.KarmaWho, v => c.Arg = v ?? "", allowNone: false);
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
            case ConditionType.AnyOf or ConditionType.AllOf:
                c.Children ??= [];
                Conditions(c.Type == ConditionType.AnyOf ? "Au moins une de ces conditions" : "Toutes ces conditions", c.Children);
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
        EnumField("Effet", a.Type, v => { a.Type = v; a.Arg = ""; a.Arg2 = ""; a.Amount = 1; }, DevState.Name, rerender: true);
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
            case ActionType.StartQuest or ActionType.CompleteQuest:
                RefField("Quête", a.Arg, DevState.Quests, v => a.Arg = v ?? "", allowNone: false);
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
            case ActionType.MoveNpc:
                RefField("PNJ", a.Arg, DevState.Npcs, v => a.Arg = v ?? "", allowNone: false);
                RefField("Vers (aucun = revient à sa place)", a.Arg2, DevState.Locations, v => a.Arg2 = v ?? "");
                break;
        }
    }

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
