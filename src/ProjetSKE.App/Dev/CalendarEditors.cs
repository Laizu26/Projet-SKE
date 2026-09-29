using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Dev;

/// <summary>
/// Calendrier du mode développeur : un mois à la fois, chaque jour montre ses événements.
/// Toucher (ou cliquer) un jour le sélectionne : ses événements s'affichent dessous, avec « + Événement ce jour ».
/// </summary>
public sealed class CalendarPage : EditorPage
{
    // Mois et jour affichés (gardés en revenant d'un événement).
    private static int _month = 1;
    private static int _day = 1;

    public CalendarPage() => Render();

    /// <summary>Ouvre le calendrier sur le mois et le jour d'un événement.</summary>
    public static CalendarPage At(CalendarEventDef e)
    {
        if (e.Month > 0) _month = e.Month;
        if (e.Day > 0) _day = e.Day;
        return new CalendarPage();
    }

    protected override string PageTitle => "Calendrier des événements";
    protected override void GoBack() => SkeApp.GoTo(new DevHomePage());

    private static TimeSettings T => DevState.Draft.Time;
    private static int Months => Math.Max(1, T.Months.Count);
    private static int DaysPerMonth => Math.Max(1, T.DaysPerMonth);
    private static string MonthName(int m) => T.Months.Count >= m && m > 0 ? T.Months[m - 1] : $"Mois {m}";

    protected override void Build(Form f)
    {
        _month = Math.Clamp(_month, 1, Months);
        _day = Math.Clamp(_day, 1, DaysPerMonth);
        var events = DevState.Draft.Events;
        if (!T.Enabled)
            f.Note("⚠ Le temps ne s'écoule pas (« Temps et calendrier ») : les événements n'auront jamais lieu en jeu.");
        f.Note("Touche un jour pour voir ses événements et en ajouter. Pendant un événement, la condition « Événement en cours » "
            + "est remplie : PNJ présents, dialogues, choix, lieux ouverts... Il peut aussi lancer des effets au début et à la fin.");

        // Mois : flèches (toucher et souris), comme le choix des héros.
        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8,
        };
        header.Add(Btn("◂", () => { _month = _month <= 1 ? Months : _month - 1; Render(); }), 0, 0);
        var title = Serif(T.Months.Count > 0 ? MonthName(_month) : "Mois", 20);
        title.HorizontalTextAlignment = TextAlignment.Center;
        title.VerticalOptions = LayoutOptions.Center;
        header.Add(title, 1, 0);
        header.Add(Btn("▸", () => { _month = _month >= Months ? 1 : _month + 1; Render(); }), 2, 0);
        f.Add(header);
        f.Add(Grid(events));

        // Le jour choisi.
        var day = _day;
        var month = _month;
        f.Header($"Le {day} {(T.Months.Count > 0 ? MonthName(month) : "")}".TrimEnd());
        var onDay = Calendar.OnCalendarDay(events, month, day).ToList();
        if (onDay.Count == 0) f.Note("Aucun événement ce jour-là.");
        foreach (var e in onDay) f.Add(EventRow(e));
        f.Add(Btn($"+ Événement le {day} {(T.Months.Count > 0 ? MonthName(month) : "")}".TrimEnd(), () =>
        {
            var e = new CalendarEventDef
            {
                Id = DevState.NewId("evenement", events.Select(x => x.Id)),
                Name = "Nouvel événement",
                Month = T.Months.Count > 0 ? month : 0,
                Day = day,
            };
            events.Add(e);
            DevState.Touch();
            SkeApp.GoTo(new CalendarEventEditor(e));
        }, selected: true));

        // Événements sans date fixe dans le mois (chaque semaine, chaque jour...).
        var weekly = events.Where(e => e.Day == 0 && (e.Month == 0 || e.Month == month)).ToList();
        if (weekly.Count > 0)
        {
            f.Header("Tous les jours / chaque semaine");
            foreach (var e in weekly) f.Add(EventRow(e));
        }

        f.Header($"Tous les événements ({events.Count})");
        if (events.Count == 0) f.Note("Aucun événement pour l'instant.");
        foreach (var e in events.OrderBy(e => e.Month).ThenBy(e => e.Day)) f.Add(EventRow(e));
    }

    /// <summary>Grille des jours du mois : numéro du jour et pastille du nombre d'événements.</summary>
    private View Grid(List<CalendarEventDef> events)
    {
        var columns = T.WeekDays.Count is >= 5 and <= 10 ? T.WeekDays.Count : 7;
        var grid = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var d = 1; d <= DaysPerMonth; d++)
        {
            var index = d - 1;
            if (index % columns == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var count = Calendar.OnCalendarDay(events, _month, d).Count(e => e.Day > 0);
            var selected = d == _day;
            var cell = new VerticalStackLayout
            {
                Spacing = 1,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = d.ToString(), FontSize = 14, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = selected ? Theme.Gold500 : Theme.Text,
                    },
                    new Label
                    {
                        Text = count > 0 ? (count > 1 ? $"● {count}" : "●") : " ", FontSize = 9, HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = selected ? Theme.Gold400 : Theme.Gold600,
                    },
                },
            };
            var box = new Border
            {
                Content = cell,
                Padding = new Thickness(2, 6),
                MinimumHeightRequest = 44,
                BackgroundColor = selected ? Night.Stone900 : count > 0 ? Theme.Highlight : Theme.Surface,
                Stroke = selected ? Theme.Gold600 : count > 0 ? Theme.Gold500.WithAlpha(0.5f) : Theme.Stone200,
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            };
            var day = d;
            OnTap(box, () => { _day = day; Render(); });
            grid.Add(box, index % columns, index / columns);
        }
        return grid;
    }

    private static View EventRow(CalendarEventDef e)
    {
        var info = Stack(Txt(e.Name.Length > 0 ? e.Name : e.Id, 15, Theme.Text, bold: true), Muted(Calendar.Describe(e, T), 11));
        info.Spacing = 1;
        var card = Card(IconRow(IconBox(Ico.Sparkles, Theme.Gold600, 36), info, Icon(Ico.ChevronRight, 16, Theme.Stone400)));
        card.Padding = new Thickness(10, 8);
        return OnTap(card, () => SkeApp.GoTo(new CalendarEventEditor(e)));
    }
}

/// <summary>Un événement du calendrier : date, durée, conditions, message et effets.</summary>
public sealed class CalendarEventEditor : EditorPage
{
    private readonly CalendarEventDef _x;
    public CalendarEventEditor(CalendarEventDef x) { _x = x; Render(); }
    protected override string PageTitle => "Événement : " + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(CalendarPage.At(_x));
    protected override Action Delete => () => DevState.Draft.Events.Remove(_x);

    protected override void Build(Form f)
    {
        var t = DevState.Draft.Time;
        f.Note("Identifiant : " + _x.Id + " (condition « Événement en cours », ou {evenement " + _x.Id + "} en mode texte)");
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Description (pour toi)", _x.Description, v => _x.Description = v, multiline: true);

        f.Header("Quand");
        f.Add(Txt("A lieu " + Calendar.Describe(_x, t) + ".", 13, Theme.Gold700, bold: true));
        if (t.Months.Count > 0)
        {
            var months = new List<(string Id, string Name)> { ("0", "Tous les mois") };
            months.AddRange(t.Months.Select((m, i) => ((i + 1).ToString(), m)));
            f.RefField("Mois", _x.Month.ToString(), months, v => _x.Month = int.TryParse(v, out var m) ? m : 0, allowNone: false, rerender: true);
        }
        f.IntField($"Jour du mois (1 à {Math.Max(1, t.DaysPerMonth)} ; 0 = tous les jours)", _x.Day, v => _x.Day = Math.Clamp(v, 0, Math.Max(1, t.DaysPerMonth)));
        f.IntField("Année précise (0 = chaque année)", _x.Year, v => _x.Year = Math.Max(0, v));
        if (t.WeekDays.Count > 0)
            f.RefField("Seulement un (jour de la semaine)", _x.WeekDay.Length > 0 ? _x.WeekDay : null, t.WeekDays.Select(d => (d, d)),
                v => _x.WeekDay = v ?? "", rerender: true);
        f.IntField("Durée (jours)", _x.Days, v => _x.Days = Math.Max(1, v));
        f.IntField("Commence à (heure, le premier jour)", _x.FromHour, v => _x.FromHour = Math.Clamp(v, 0, t.HoursPerDay));
        f.IntField("Finit à (heure, le dernier jour ; 0 = minuit)", _x.ToHour, v => _x.ToHour = Math.Clamp(v, 0, t.HoursPerDay));
        f.Add(Btn("Appliquer (mettre à jour la date ci-dessus)", Render));

        f.Header("Seulement si");
        f.Conditions("L'événement n'a lieu que si", _x.Conditions);

        f.Header("Au début");
        f.BoolField("Prévenir le joueur (message à l'écran)", _x.Announce, v => _x.Announce = v, rerender: true);
        if (_x.Announce) f.TextField("Message (vide = « Événement : nom »)", _x.Message, v => _x.Message = v);
        f.Actions("Effets au début (flags, dialogue, PNJ...)", _x.StartActions);

        f.Header("À la fin");
        f.Actions("Effets à la fin", _x.EndActions);
    }
}
