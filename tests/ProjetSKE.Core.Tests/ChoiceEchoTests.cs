using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Le choix fait est joué comme une réplique (dite par celui qui parle, ou racontée) avant la suite.</summary>
public class ChoiceEchoTests
{
    [Fact]
    public void ChosenReply_IsPlayedBeforeTheNextLine()
    {
        var s = GameSession.NewGame(GameDatabase.Default, "aldric", new Random(1));
        var runner = s.StartDialogue("joss_quete");
        runner.EchoChoices = true;
        var step = runner.Step;

        runner.ChooseOption(0);
        // D'abord la réplique du choix, dite par celui qui parle (le héros).
        Assert.Equal("Compte sur moi.", runner.Text);
        Assert.Equal("Aldric", runner.Speaker);
        Assert.Empty(runner.Options);
        Assert.True(runner.Step > step);
        Assert.Equal(QuestStatus.Active, s.GetQuestStatus("chasse_loups")); // les effets du choix sont déjà appliqués

        // Puis la suite.
        runner.Continue();
        Assert.Equal("oui", runner.Current!.Id);
        Assert.Equal("Fermier Joss", runner.Speaker);
    }

    [Fact]
    public void ChoiceThatEndsTheDialogue_IsStillPlayed()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Dialogues.Add(new DialogueDef
        {
            Id = "adieu",
            Nodes = [new() { Id = "1", Speaker = "Garde", Text = "Tu pars ?", Choices = [new() { Text = "Oui, adieu." }] }],
        });
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        var runner = s.StartDialogue("adieu");
        runner.EchoChoices = true;
        runner.ChooseOption(0);
        Assert.False(runner.IsFinished);
        Assert.Equal("Oui, adieu.", runner.Text);
        runner.Continue();
        Assert.True(runner.IsFinished);
    }

    [Fact]
    public void WithoutEcho_NextLineComesDirectly()
    {
        var s = GameSession.NewGame(GameDatabase.Default, "aldric", new Random(1));
        var runner = s.StartDialogue("joss_quete");
        runner.ChooseOption(1);
        Assert.Equal("non", runner.Current!.Id);
    }
}
