using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Condition « Stat » : PV, PM, ATQ, niveau... d'un PJ ou de l'équipe.</summary>
public class StatConditionTests
{
    [Fact]
    public void Stats_OfTheSpeaker_Hero_OrTeam()
    {
        var s = GameSession.NewGame(GameDatabase.Default, "aldric", new Random(1));
        var hero = s.State.Party[0];
        var max = s.GetStats(hero).MaxHp;
        hero.CurrentHp = max / 4; // 25 %

        Assert.True(s.Check(new Condition(ConditionType.Stat, "@heros", 30) { Arg2 = "pv%", Op = CompareOp.Less }));
        Assert.False(s.Check(new Condition(ConditionType.Stat, "@heros", 30) { Arg2 = "pv%", Op = CompareOp.AtLeast }));
        Assert.True(s.Check(new Condition(ConditionType.Stat, "", s.GetStats(hero).Attack) { Arg2 = "atq" })); // @parle par défaut
        Assert.True(s.Check(new Condition(ConditionType.Stat, "@equipe", 1) { Arg2 = "niveau" }));
        Assert.False(s.Check(new Condition(ConditionType.Stat, "@equipe", 99) { Arg2 = "niveau" }));
    }

    [Fact]
    public void Script_Knows_Stat()
    {
        var nodes = DialogueScript.Parse("- Tu vacilles.\n> Se reposer {stat pv% < 30 @parle} -> fin", out var errors);
        Assert.Empty(errors);
        var c = nodes[0].Choices[0].Conditions.Single();
        Assert.Equal(ConditionType.Stat, c.Type);
        Assert.Equal("pv%", c.Arg2);
        Assert.Equal(CompareOp.Less, c.Op);
        Assert.Equal(30, c.Amount);
        Assert.Contains("{stat pv% < 30 @parle}", DialogueScript.Write(nodes));
    }
}
