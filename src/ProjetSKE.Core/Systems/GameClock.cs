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
