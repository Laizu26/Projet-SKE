using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Événements du calendrier : date, durée, effets au début et à la fin, condition « en cours ».</summary>
public class CalendarEventTests
{
    private static GameSession Game(CalendarEventDef e)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Events.Add(e);
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        return GameSession.NewGame(db, "aldric", new Random(1));
    }

    private static long DayStart(GameSession s, int day) => (day - 1L) * s.Db.Content.Time.HoursPerDay * 60;

    [Fact]
    public void Festival_StartsAndEnds_WithEffects()
    {
        // Le 3 du premier mois, 2 jours : flag au début, retiré à la fin.
        var fete = new CalendarEventDef
        {
            Id = "fete", Name = "Fête des lanternes", Month = 1, Day = 3, Days = 2,
            StartActions = [new(ActionType.SetFlag, "fete")],
            EndActions = [new(ActionType.ClearFlag, "fete")],
        };
        var s = Game(fete);
        Assert.False(s.IsEventActive("fete"));

        s.AdvanceTime(DayStart(s, 3) + 60 - s.State.Minutes); // le 3, 1 h du matin
        Assert.True(s.IsEventActive("fete"));
        Assert.True(s.HasFlag("fete"));
        Assert.Contains(s.Notifications, n => n.Contains("Fête des lanternes"));
        Assert.True(s.Check(new Condition(ConditionType.EventActive, "fete")));

        s.AdvanceTime(DayStart(s, 5) + 60 - s.State.Minutes); // le 5 : c'est fini
        Assert.False(s.IsEventActive("fete"));
        Assert.False(s.HasFlag("fete"));
    }

    [Fact]
    public void Effects_RunOncePerOccurrence_EvenWhenTimeJumpsOverIt()
    {
        var marche = new CalendarEventDef { Id = "marche", Name = "Marché", Month = 0, Day = 0, WeekDay = "Mardi", FromHour = 8, ToHour = 12,
            StartActions = [new(ActionType.GiveGold, "", 10)] };
        var s = Game(marche);
        var gold = s.State.Gold;
        // Une semaine d'un coup : un seul mardi, passé entièrement pendant le saut → effets quand même, une fois.
        s.AdvanceTime(7L * s.Db.Content.Time.HoursPerDay * 60);
        Assert.Equal(gold + 10, s.State.Gold);
        s.AdvanceTime(30);
        Assert.Equal(gold + 10, s.State.Gold);
    }

    [Fact]
    public void Conditions_BlockTheEvent()
    {
        var e = new CalendarEventDef { Id = "eclipse", Name = "Éclipse", Month = 0, Day = 0, Conditions = [new(ConditionType.FlagSet, "prophetie")] };
        var s = Game(e);
        Assert.False(s.IsEventActive("eclipse"));
        s.Execute(new GameAction(ActionType.SetFlag, "prophetie"));
        Assert.True(s.IsEventActive("eclipse"));
    }

    [Fact]
    public void Calendar_FindsEventsOfADay_AndDescribesThem()
    {
        var t = new TimeSettings();
        var e = new CalendarEventDef { Id = "a", Month = 2, Day = 14, Days = 3 };
        Assert.Single(Calendar.OnCalendarDay([e], 2, 14));
        Assert.Empty(Calendar.OnCalendarDay([e], 1, 14));
        Assert.True(Calendar.StartsOn(e, t.DaysPerMonth + 14, t));
        Assert.Contains("14 " + t.Months[1], Calendar.Describe(e, t));
        Assert.Contains("3 jours", Calendar.Describe(e, t));
    }

    [Fact]
    public void Script_Knows_TheEventCondition()
    {
        var nodes = DialogueScript.Parse("- La place s'illumine.\n> Aller à la fête {evenement fete} -> fin", out var errors);
        Assert.Empty(errors);
        Assert.Equal(ConditionType.EventActive, nodes.SelectMany(n => n.Choices).Single().Conditions.Single().Type);
    }
}
