using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Choix réservé à un PJ : proposé si c'est le héros joué, sinon ce PJ le dit.</summary>
public class ChoiceSpeakerTests
{
    private static GameSession Game(string hero)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Dialogues.Add(new DialogueDef
        {
            Id = "porte",
            ScenePjIds = ["lyra"],
            Nodes =
            [
                new()
                {
                    Id = "1", Speaker = "", Text = "La porte est fermée.",
                    Choices =
                    [
                        new() { Id = "forcer", Speaker = "Aldric", Text = "Je la défonce.", NextId = "2" },
                        new() { Id = "sort", Speaker = "Lyra", Text = "Laisse-moi l'ouvrir.", NextId = "3" },
                    ],
                },
                new() { Id = "2", Text = "Le bois vole en éclats." },
                new() { Id = "3", Text = "La serrure cède." },
            ],
        });
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        return GameSession.NewGame(db, hero, new Random(1));
    }

    [Fact]
    public void HeroSeesOnlyHisOwnChoices()
    {
        var runner = Game("aldric").StartDialogue("porte")!;
        var option = Assert.Single(runner.Options);
        Assert.Equal("forcer", option.Choice.Id);
        Assert.Null(runner.AutoChoice);
    }

    [Fact]
    public void ChoicesFollowThePlayedHero()
    {
        var s = Game("lyra");
        // Lyra est l'héroïne : son choix lui est proposé, celui d'Aldric disparaît.
        var runner = s.StartDialogue("porte")!;
        Assert.Equal("sort", Assert.Single(runner.Options).Choice.Id);
    }

    [Fact]
    public void AutoChoice_IsSaid_ByThePj_AndFollowed()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Dialogues.Add(new DialogueDef
        {
            Id = "seule",
            ScenePjIds = ["lyra"],
            Nodes =
            [
                new() { Id = "1", Text = "Un bruit.", Choices = [new() { Speaker = "Lyra", Text = "J'y vais.", NextId = "2" }] },
                new() { Id = "2", Text = "Elle revient." },
            ],
        });
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        var runner = s.StartDialogue("seule")!;
        Assert.False(runner.HasOptions);
        Assert.NotNull(runner.AutoChoice);
        runner.Continue();
        Assert.Equal("Lyra", runner.Speaker.Split(' ')[0]);
        Assert.Equal("J'y vais.", runner.Text);
        runner.Continue();
        Assert.Equal("Elle revient.", runner.Text);
    }

    [Fact]
    public void Script_RoundTrips_ChoiceSpeaker()
    {
        var nodes = DialogueScript.Parse("- La porte.\n> Aldric: Je la défonce. -> fin\n> Attends: une idée", out var errors, ["Aldric", "Lyra"]);
        Assert.Empty(errors);
        Assert.Equal("Aldric", nodes[0].Choices[0].Speaker);
        Assert.Equal("Je la défonce.", nodes[0].Choices[0].Text);
        Assert.Equal("", nodes[0].Choices[1].Speaker); // « Attends » n'est pas un PJ
        Assert.Contains("> Aldric: Je la défonce.", DialogueScript.Write(nodes));
    }
}
