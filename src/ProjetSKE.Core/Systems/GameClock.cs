using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Systems;

/// <summary>Date et heure du monde, calculées à partir des minutes écoulées et du calendrier du contenu.</summary>
public readonly record struct GameClock(long TotalMinutes, TimeSettings Settings)
{
    private int HoursPerDay => Math.Max(1, Settings.HoursPerDay);
    private long MinutesPerDay => HoursPerDay * 60L;

    /// <summary>Numéro du jour depuis le début du calendrier (1 = premier jour).</summary>
    public int Day => (int)(Math.Max(0, TotalMinutes) / MinutesPerDay) + 1;
    public int Hour => (int)(Math.Max(0, TotalMinutes) % MinutesPerDay / 60);
    public int Minute => (int)(Math.Max(0, TotalMinutes) % 60);

    public string WeekDay => Settings.WeekDays.Count > 0 ? Settings.WeekDays[(Day - 1) % Settings.WeekDays.Count] : "";

    private int DaysPerMonth => Math.Max(1, Settings.DaysPerMonth);
    private int MonthsPerYear => Math.Max(1, Settings.Months.Count);

    public int DayOfMonth => (Day - 1) % DaysPerMonth + 1;
    public string Month => Settings.Months.Count > 0 ? Settings.Months[(Day - 1) / DaysPerMonth % MonthsPerYear] : "";
    public int Year => Settings.StartYear + (Day - 1) / (DaysPerMonth * MonthsPerYear);

    /// <summary>Moment de la journée : la dernière période commencée.</summary>
    public string Period
    {
        get
        {
            var hour = Hour;
            return Settings.Periods.Where(p => p.FromHour <= hour).OrderBy(p => p.FromHour).LastOrDefault()?.Name
                ?? Settings.Periods.OrderBy(p => p.FromHour).LastOrDefault()?.Name
                ?? "";
        }
    }

    public string TimeText => $"{Hour:00}:{Minute:00}";

    /// <summary>Ex : « Mardi 3 Givrelune, An 1 ».</summary>
    public string DateText
    {
        get
        {
            var parts = new List<string>();
            if (WeekDay.Length > 0) parts.Add(WeekDay);
            parts.Add(Settings.Months.Count > 0 ? $"{DayOfMonth} {Month}" : $"Jour {Day}");
            var date = string.Join(" ", parts);
            return Settings.Months.Count > 0 && Settings.YearLabel.Length > 0 ? $"{date}, {Settings.YearLabel} {Year}" : date;
        }
    }

    /// <summary>Minutes du début de partie (jour et heure de départ des réglages).</summary>
    public static long StartMinutes(TimeSettings s) =>
        ((long)Math.Max(0, s.StartDay - 1) * Math.Max(1, s.HoursPerDay) + Math.Max(0, s.StartHour)) * 60;

    /// <summary>Minutes à attendre pour arriver à l'heure donnée (le lendemain si elle est passée).</summary>
    public long MinutesUntilHour(int hour)
    {
        var target = (long)Math.Clamp(hour, 0, HoursPerDay - 1) * 60;
        var now = Math.Max(0, TotalMinutes) % MinutesPerDay;
        var wait = target - now;
        return wait <= 0 ? wait + MinutesPerDay : wait;
    }
}

/// <summary>Dates des événements du calendrier (voir <see cref="CalendarEventDef"/>).</summary>
public static class Calendar
{
    /// <summary>L'événement commence ce jour-là (numéro de jour depuis le début du calendrier).</summary>
    public static bool StartsOn(CalendarEventDef e, int day, TimeSettings t)
    {
        if (day < 1) return false;
        var clock = new GameClock((day - 1L) * Math.Max(1, t.HoursPerDay) * 60, t);
        if (e.Year > 0 && clock.Year != e.Year) return false;
        if (e.Month > 0 && t.Months.Count > 0 && MonthIndex(day, t) != e.Month) return false;
        if (e.Day > 0 && clock.DayOfMonth != e.Day) return false;
        if (e.WeekDay.Length > 0 && !string.Equals(clock.WeekDay, e.WeekDay, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>Numéro du mois (1 = premier) d'un jour.</summary>
    public static int MonthIndex(int day, TimeSettings t) =>
        (day - 1) / Math.Max(1, t.DaysPerMonth) % Math.Max(1, t.Months.Count) + 1;

    /// <summary>Début et fin (en minutes) de l'occurrence qui commence ce jour-là.</summary>
    public static (long Start, long End) Window(CalendarEventDef e, int startDay, TimeSettings t)
    {
        var perDay = Math.Max(1, t.HoursPerDay) * 60L;
        var start = (startDay - 1L) * perDay + Math.Clamp(e.FromHour, 0, t.HoursPerDay) * 60L;
        var lastDay = startDay - 1L + Math.Max(1, e.Days) - 1;
        var end = e.ToHour > 0 ? lastDay * perDay + Math.Clamp(e.ToHour, 0, t.HoursPerDay) * 60L : (lastDay + 1) * perDay;
        return (start, Math.Max(end, start + 1));
    }

    /// <summary>Jours de début possibles pour une occurrence en cours entre deux instants (bornés).</summary>
    public static IEnumerable<int> CandidateDays(CalendarEventDef e, long from, long to, TimeSettings t)
    {
        var perDay = Math.Max(1, t.HoursPerDay) * 60L;
        var first = (int)(Math.Max(0, from) / perDay) + 1 - Math.Max(1, e.Days);
        var last = (int)(Math.Max(0, to) / perDay) + 1;
        first = Math.Max(1, Math.Max(first, last - 800)); // grand saut dans le temps : on ne regarde que les derniers jours
        for (var d = first; d <= last; d++)
            if (StartsOn(e, d, t)) yield return d;
    }

    /// <summary>L'événement a lieu à cet instant (sans compter ses conditions).</summary>
    public static bool IsOn(CalendarEventDef e, long minutes, TimeSettings t) =>
        CandidateDays(e, minutes, minutes, t).Any(d => Window(e, d, t) is var w && w.Start <= minutes && minutes < w.End);

    /// <summary>Événements qui commencent un jour du calendrier « mois / jour » (vue calendrier de l'éditeur).</summary>
    public static IEnumerable<CalendarEventDef> OnCalendarDay(IEnumerable<CalendarEventDef> events, int month, int dayOfMonth) =>
        events.Where(e => (e.Month == 0 || e.Month == month) && (e.Day == 0 || e.Day == dayOfMonth));

    /// <summary>Quand a lieu l'événement, en clair (ex : « le 3 Givrelune, chaque année, 3 jours »).</summary>
    public static string Describe(CalendarEventDef e, TimeSettings t)
    {
        var month = e.Month > 0 && e.Month <= t.Months.Count ? t.Months[e.Month - 1] : "";
        var when = (e.Day, e.Month) switch
        {
            (0, 0) => "tous les jours",
            (0, _) => $"tous les jours de {month}",
            (_, 0) => $"le {e.Day} de chaque mois",
            _ => t.Months.Count > 0 ? $"le {e.Day} {month}" : $"le jour {e.Day}",
        };
        if (e.WeekDay.Length > 0)
            when = e.Day == 0 && e.Month == 0 ? $"chaque {e.WeekDay.ToLowerInvariant()}" : $"{when} (si c'est un {e.WeekDay.ToLowerInvariant()})";
        when += e.Year > 0 ? $", {t.YearLabel} {e.Year}".TrimEnd() : e.Month > 0 && e.Day > 0 ? ", chaque année" : "";
        if (e.Days > 1) when += $", {e.Days} jours";
        if (e.FromHour > 0 || e.ToHour > 0) when += $", {e.FromHour:00}h → {(e.ToHour > 0 ? $"{e.ToHour:00}h" : "minuit")}";
        return when;
    }
}

