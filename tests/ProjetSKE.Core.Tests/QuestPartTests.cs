using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Quêtes en plusieurs parties (en parallèle, chacune avec son état) et départ propre à chaque héros.</summary>
public class QuestPartTests
{
    private static GameSession Game(Action<GameContent>? change = null, string hero = "aldric")
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Quests.Add(new QuestDef
        {
            Id = "complot", Name = "Le complot",
            Parts =
            [
                new() { Id = "temoin", Name = "Interroger le témoin", Objectives = [new() { Type = ObjectiveType.TalkTo, TargetId = "olric" }] },
                new()
                {
                    Id = "route", Name = "Fouiller la route", StartConditions = [new(ConditionType.FlagSet, "piste")],
                    Objectives = [new() { Type = ObjectiveType.Reach, TargetId = "route_roi" }],
                    Rewards = [new(ActionType.GiveGold, amount: 10)],
                },
                new() { Id = "bonus", Name = "Secret", Optional = true, StartConditions = [new(ConditionType.FlagSet, "jamais")] },
            ],
            Rewards = [new(ActionType.SetFlag, "complot_dejoue")],
        });
        change?.Invoke(content);
        Assert.Empty(new GameDatabase(content).Validate());
        return GameSession.NewGame(new GameDatabase(content), hero, new Random(1));
    }

    [Fact]
    public void Parts_RunInParallelAndEachHasItsState()
    {
        var s = Game();
        s.StartQuest("complot");
        Assert.Equal(QuestStatus.Active, s.GetPartStatus("complot", "temoin"));
        Assert.Equal(QuestStatus.NotStarted, s.GetPartStatus("complot", "route")); // attend sa condition

        s.SetFlag("piste");
        s.UpdateQuests();
        Assert.Equal(QuestStatus.Active, s.GetPartStatus("complot", "route"));

        // La 2ᵉ partie avant la 1ʳᵉ : l'ordre est libre.
        var gold = s.State.Gold;
        s.Execute(new GameAction(ActionType.Teleport, "route_roi"));
        s.UpdateQuests();
        Assert.Equal(QuestStatus.Completed, s.GetPartStatus("complot", "route"));
        Assert.Equal(gold + 10, s.State.Gold);
        Assert.Equal(QuestStatus.Active, s.GetQuestStatus("complot"));
        Assert.True(s.Check(new Condition(ConditionType.QuestPartCompleted, "complot") { Arg2 = "route" }));

        s.UpdateQuests(ObjectiveType.TalkTo, "olric");
        Assert.Equal(QuestStatus.Completed, s.GetPartStatus("complot", "temoin"));
        // La partie facultative n'empêche pas la réussite.
        Assert.Equal(QuestStatus.Completed, s.GetQuestStatus("complot"));
        Assert.True(s.HasFlag("complot_dejoue"));
    }

    [Fact]
    public void Parts_CanFailAndBeDrivenByEffects()
    {
        var s = Game(c => c.Quests.First(q => q.Id == "complot").Parts[0].FailConditions = [new(ConditionType.FlagSet, "temoin_mort")]);
        s.Execute(new GameAction(ActionType.StartQuestPart, "complot") { Arg2 = "route" }); // démarre la quête et la partie
        Assert.Equal(QuestStatus.Active, s.GetQuestStatus("complot"));
        Assert.Equal(QuestStatus.Active, s.GetPartStatus("complot", "route"));

        s.Execute(new GameAction(ActionType.CompleteQuestPart, "complot") { Arg2 = "route" });
        Assert.Equal(QuestStatus.Completed, s.GetPartStatus("complot", "route"));

        s.SetFlag("temoin_mort");
        s.UpdateQuests();
        Assert.Equal(QuestStatus.Failed, s.GetPartStatus("complot", "temoin"));
        Assert.Equal(QuestStatus.Failed, s.GetQuestStatus("complot")); // partie obligatoire échouée
    }

    [Fact]
    public void Parts_WorkInScripts()
    {
        var nodes = DialogueScript.Parse("- Test [partie complot route] [finir_partie complot temoin]\n> Ok {partie_finie complot temoin} {partie_active complot route}", out var errors);
        Assert.Empty(errors);
        Assert.Equal(ActionType.StartQuestPart, nodes[0].Actions[0].Type);
        Assert.Equal("route", nodes[0].Actions[0].Arg2);
        Assert.Equal(ConditionType.QuestPartCompleted, nodes[0].Choices[0].Conditions[0].Type);
    }

    [Fact]
    public void Hero_HasOwnCompanionsAndStartEffects()
    {
        var s = Game(c =>
        {
            var tobin = c.Characters.First(x => x.Id == "tobin");
            tobin.StartCompanions = ["lyra"];
            tobin.StartActions = [new(ActionType.SetFlag, "voleur"), new(ActionType.GiveGold, amount: 5)];
        }, hero: "tobin");
        Assert.True(s.IsInParty("lyra"));
        Assert.True(s.HasFlag("voleur"));
        Assert.False(Game().IsInParty("lyra")); // Aldric n'a pas ces compagnons
    }
}
