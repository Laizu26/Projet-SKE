using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>PJ présents dans la scène d'un dialogue sans être dans le groupe.</summary>
public class SceneCastTests
{
    private static GameSession Game()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Dialogues.Add(new DialogueDef
        {
            Id = "rencontre",
            ScenePjIds = ["lyra"],
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "Lyra", Text = "Je sens quelque chose d'étrange.",
                    Actions = [new(ActionType.AddGauge, "folie", 20) { Arg2 = "lyra" }, new(ActionType.AddKarma, "lyra", 5)],
                    NextId = "2",
                },
                new() { Id = "2", Speaker = "Aldric", Text = "Reste près de moi." },
            ],
        });
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        return GameSession.NewGame(db, "aldric", new Random(1));
    }

    [Fact]
    public void ScenePj_IsPresent_DuringTheDialogueOnly()
    {
        var s = Game();
        Assert.False(s.IsPresent("lyra"));
        var runner = s.StartDialogue("rencontre");
        Assert.True(s.IsPresent("lyra"));
        Assert.True(s.Check(new Condition(ConditionType.InParty, "lyra")));
        s.SetSpeaker("lyra");
        Assert.Equal("lyra", s.SpeakerId);

        s.SetScene(null); // fin du dialogue
        Assert.False(s.IsPresent("lyra"));
        Assert.False(s.IsInParty("lyra"));
        Assert.Equal("aldric", s.SpeakerId);
        Assert.NotNull(runner);
    }

    [Fact]
    public void Effects_OnAScenePj_AreKept_AndFollowHerIntoTheGroup()
    {
        var s = Game();
        s.StartDialogue("rencontre"); // l'effet de la 1re réplique s'applique à Lyra, hors du groupe
        Assert.Equal(30, s.GetGauge("lyra", "folie")); // 10 de départ + 20
        var karma = s.GetKarma("lyra");
        s.SetScene(null);

        Assert.Equal(30, s.GetGauge("lyra", "folie")); // gardé après la scène
        s.Recruit("lyra");
        var lyra = s.State.Party.First(c => c.DefId == "lyra");
        Assert.Equal(30, s.GaugeOf(lyra, s.Db.Gauges["folie"]));
        Assert.Equal(karma, lyra.Karma);
        Assert.DoesNotContain(s.State.Offstage, c => c.DefId == "lyra");
    }

    [Fact]
    public void LeavingTheGroup_KeepsValues()
    {
        var s = Game();
        s.Recruit("lyra");
        s.Execute(new GameAction(ActionType.AddKarma, "lyra", 40));
        var karma = s.GetKarma("lyra");
        s.Leave("lyra");
        Assert.Equal(karma, s.GetKarma("lyra"));
        s.Recruit("lyra");
        Assert.Equal(karma, s.State.Party.First(c => c.DefId == "lyra").Karma);
    }
}
