using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Dialogue joué quand un passif se met à agir (ou arrête).</summary>
public class PassiveDialogueTests
{
    private static GameSession Game(bool once = false)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var d1 = content.Dialogues[0].Id;
        var d2 = content.Dialogues[1].Id;
        content.Passives.Add(new PassiveDef
        {
            Id = "rage", Name = "Rage", Conditions = [new(ConditionType.FlagSet, "furieux")],
            ActivationDialogueId = d1, DeactivationDialogueId = d2, ActivationOnce = once,
        });
        content.Characters.First(c => c.Id == "aldric").Passives.Add(new PassiveUnlock(1, "rage"));
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        s.PendingDialogues.Clear();
        return s;
    }

    [Fact]
    public void Activation_AndDeactivation_QueueTheirDialogues()
    {
        var s = Game();
        s.Execute(new GameAction(ActionType.SetFlag, "furieux"));
        s.UpdateQuests();
        Assert.Equal(s.Db.Passives["rage"].ActivationDialogueId, Assert.Single(s.PendingDialogues));
        Assert.Equal("aldric", s.SpeakerId);
        s.PendingDialogues.Clear();

        s.UpdateQuests(); // toujours actif : rien de plus
        Assert.Empty(s.PendingDialogues);

        s.Execute(new GameAction(ActionType.ClearFlag, "furieux"));
        s.UpdateQuests();
        Assert.Equal(s.Db.Passives["rage"].DeactivationDialogueId, Assert.Single(s.PendingDialogues));
    }

    [Fact]
    public void Once_PlaysTheActivationDialogueOnlyTheFirstTime()
    {
        var s = Game(once: true);
        s.Execute(new GameAction(ActionType.SetFlag, "furieux"));
        s.UpdateQuests();
        s.Execute(new GameAction(ActionType.ClearFlag, "furieux"));
        s.UpdateQuests();
        s.PendingDialogues.Clear();
        s.Execute(new GameAction(ActionType.SetFlag, "furieux"));
        s.UpdateQuests();
        Assert.Empty(s.PendingDialogues);
    }

    [Fact]
    public void AlreadyActiveAtStart_NoDialogue()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Passives.Add(new PassiveDef { Id = "calme", Name = "Calme", ActivationDialogueId = content.Dialogues[0].Id });
        content.Characters.First(c => c.Id == "aldric").Passives.Add(new PassiveUnlock(1, "calme"));
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        Assert.DoesNotContain(content.Dialogues[0].Id, s.PendingDialogues);
    }
}
