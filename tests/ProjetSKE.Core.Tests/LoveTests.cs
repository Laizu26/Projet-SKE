using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Amour : comme l'amitié, mais à part.</summary>
public class LoveTests
{
    [Fact]
    public void Love_IsSeparateFromFriendship_AndWorksLikeIt()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var npc = content.Npcs[0];
        npc.BaseLove = 10;
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        var friendship = s.GetFriendship(npc.Id);

        Assert.Equal(10, s.GetLove(npc.Id));
        s.Execute(new GameAction(ActionType.AddLove, npc.Id, 45) { Arg2 = "@heros" });
        Assert.Equal(55, s.GetLove(npc.Id, "@heros"));
        Assert.Equal(10, s.GetLove(npc.Id)); // envers l'équipe : inchangé
        Assert.Equal(friendship, s.GetFriendship(npc.Id));
        Assert.True(s.Check(new Condition(ConditionType.Love, npc.Id, 50) { Arg2 = "@heros" }));
        Assert.Equal("Épris", s.Db.Content.Love.TierName(55));

        s.Execute(new GameAction(ActionType.SetLove, npc.Id, 500));
        Assert.Equal(s.Db.Content.Love.Max, s.GetLove(npc.Id)); // borné
        Assert.Equal(s.GetLove(npc.Id).ToString(), s.FormatText($"%amour:{npc.Id}%"));
    }

    [Fact]
    public void Script_Knows_Love()
    {
        var nodes = DialogueScript.Parse("- Un regard.\n> Sourire [amour mara 5 @parle] {amour mara >= 20 @parle} -> fin", out var errors);
        Assert.Empty(errors);
        var choice = nodes[0].Choices[0];
        Assert.Equal(ActionType.AddLove, choice.Actions.Single().Type);
        Assert.Equal(ConditionType.Love, choice.Conditions.Single().Type);
        Assert.Contains("[amour mara 5 @parle]", DialogueScript.Write(nodes));
    }
}
