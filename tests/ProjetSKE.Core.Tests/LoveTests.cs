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
        Assert.Equal(55, s.GetLove(npc.Id)); // par défaut : envers le héros (le PP)
        Assert.Equal(55, s.GetLove(npc.Id, "@equipe")); // ancien réglage : le héros aussi
        Assert.Equal(10, s.GetLove(npc.Id, "lyra")); // envers un autre PJ : à part
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

public class TowardHeroTests
{
    [Fact]
    public void OldSaves_TeamRelations_BecomeHeroRelations()
    {
        var s = GameSession.NewGame(GameDatabase.Default, "aldric", new Random(1));
        var npc = s.Db.Content.Npcs[0].Id;
        s.State.Relations[$"{npc}>@equipe"] = 42;
        var loaded = new GameSession(s.Db, s.State); // chargement : même état, remis à jour
        Assert.Equal(42, loaded.GetFriendship(npc));
        Assert.False(loaded.State.Relations.ContainsKey($"{npc}>@equipe"));
    }
}
