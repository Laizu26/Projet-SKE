using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Réplique d'un PJ « choix si c'est le héros » : choix pour le héros joué, dite par un compagnon.</summary>
public class HeroChoiceTests
{
    private static GameDatabase Db()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Dialogues.Add(new DialogueDef
        {
            Id = "serment",
            Nodes =
            [
                new() { Id = "1", Speaker = "Garde", Text = "Qui défendra la ville ?", NextId = "2" },
                new()
                {
                    Id = "2", Speaker = "Aldric", Text = "Moi. Je resterai.", HeroChoice = true, NextId = "3",
                    Choices = [new() { Text = "Pas moi.", NextId = "fuite" }],
                },
                new() { Id = "3", Speaker = "Garde", Text = "Merci, chevalier." },
                new() { Id = "fuite", Speaker = "Garde", Text = "Lâche !" },
            ],
        });
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        return db;
    }

    [Fact]
    public void HeroSpeaker_LineIsOfferedAsAChoice()
    {
        var s = GameSession.NewGame(Db(), "aldric", new Random(1));
        var runner = s.StartDialogue("serment");
        runner.Continue();
        Assert.True(runner.IsHeroLine);
        Assert.Equal("", runner.Text);
        Assert.Equal(["Moi. Je resterai.", "Pas moi."], runner.Options.Select(o => o.Text));

        runner.ChooseOption(0); // le héros dit sa réplique
        Assert.Equal("3", runner.Current!.Id);
        Assert.Contains("serment:2:replique", s.State.Choices);
    }

    [Fact]
    public void HeroCanPickAnotherAnswer()
    {
        var s = GameSession.NewGame(Db(), "aldric", new Random(1));
        var runner = s.StartDialogue("serment");
        runner.Continue();
        runner.ChooseOption(1);
        Assert.Equal("fuite", runner.Current!.Id);
    }

    [Fact]
    public void CompanionSpeaker_SaysTheLine_AndGoesOn()
    {
        var s = GameSession.NewGame(Db(), "lyra", new Random(1)); // Aldric n'est pas le héros : un compagnon
        var runner = s.StartDialogue("serment");
        runner.Continue();
        Assert.False(runner.IsHeroLine);
        Assert.Equal("Moi. Je resterai.", runner.Text);
        Assert.Empty(runner.Options);
        runner.Continue();
        Assert.Equal("3", runner.Current!.Id);
    }

    [Fact]
    public void Script_KeepsTheChoiceMarker()
    {
        var nodes = DialogueScript.Parse("Aldric: Moi. Je resterai. #choix", out var errors);
        Assert.Empty(errors);
        Assert.True(nodes[0].HeroChoice);
        Assert.Equal("Moi. Je resterai.", nodes[0].Text);
        Assert.Contains("#choix", DialogueScript.Write(nodes));
    }
}
