using System.Globalization;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
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

    public void Conditions(string label, List<Condition> conditions) =>
        ObjectList(label, conditions, () => new Condition(ConditionType.FlagSet), (f, c, _) =>
        {
            f.EnumField("Type", c.Type, v => { c.Type = v; c.Arg = ""; }, DevState.Name, rerender: true);
            switch (c.Type)
            {
                case ConditionType.FlagSet or ConditionType.FlagNotSet:
                    f.TextField("Nom du flag", c.Arg, v => c.Arg = v);
                    break;
                case ConditionType.QuestNotStarted or ConditionType.QuestActive or ConditionType.QuestCompleted:
                    f.RefField("Quête", c.Arg, DevState.Quests, v => c.Arg = v ?? "", allowNone: false);
                    break;
                case ConditionType.HasItem:
                    f.RefField("Objet", c.Arg, DevState.Items(), v => c.Arg = v ?? "", allowNone: false);
                    f.IntField("Quantité", c.Amount, v => c.Amount = v);
                    break;
                case ConditionType.InParty or ConditionType.NotInParty:
                    f.RefField("Personnage", c.Arg, DevState.Characters, v => c.Arg = v ?? "", allowNone: false);
                    break;
                default:
                    f.IntField("Valeur", c.Amount, v => c.Amount = v);
                    break;
            }
        }, "+ Condition");

    public void Actions(string label, List<GameAction> actions) =>
        ObjectList(label, actions, () => new GameAction(ActionType.SetFlag), (f, a, _) =>
        {
            f.EnumField("Effet", a.Type, v => { a.Type = v; a.Arg = ""; a.Amount = 1; }, DevState.Name, rerender: true);
            switch (a.Type)
            {
                case ActionType.SetFlag or ActionType.ClearFlag:
                    f.TextField("Nom du flag", a.Arg, v => a.Arg = v);
                    break;
                case ActionType.Recruit:
                    f.RefField("Personnage", a.Arg, DevState.Characters, v => a.Arg = v ?? "", allowNone: false);
                    break;
                case ActionType.GiveItem or ActionType.TakeItem:
                    f.RefField("Objet", a.Arg, DevState.Items(), v => a.Arg = v ?? "", allowNone: false);
                    f.IntField("Quantité", a.Amount, v => a.Amount = v);
                    break;
                case ActionType.GiveGold or ActionType.TakeGold or ActionType.GiveXp:
                    f.IntField("Montant", a.Amount, v => a.Amount = v);
                    break;
                case ActionType.StartQuest or ActionType.CompleteQuest:
                    f.RefField("Quête", a.Arg, DevState.Quests, v => a.Arg = v ?? "", allowNone: false);
                    break;
                case ActionType.Teleport:
                    f.RefField("Lieu", a.Arg, DevState.Locations, v => a.Arg = v ?? "", allowNone: false);
                    break;
                case ActionType.StartBattle:
                    var ids = a.Arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    f.IdList("Monstres", ids, DevState.Monsters,
                        onAdded: _ => a.Arg = string.Join(",", ids),
                        onRemoved: _ => a.Arg = string.Join(",", ids));
                    break;
            }
        }, "+ Effet");
}
