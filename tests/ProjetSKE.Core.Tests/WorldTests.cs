using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Temps, karma, amitié, variables, conditions avancées, dialogues croisés et vocabulaire.</summary>
public class WorldTests
{
    private static GameSession NewGame(GameContent? content = null) =>
        GameSession.NewGame(content is null ? GameDatabase.Default : new GameDatabase(content), "aldric", new Random(1));

    private static GameContent Sample() => ContentSerializer.Clone(GameDatabase.Default.Content);

    [Fact]
    public void Clock_StartsAtConfiguredDayAndHour()
    {
        var content = Sample();
        content.Time.StartDay = 3;
        content.Time.StartHour = 9;
        var s = NewGame(content);
        Assert.Equal(3, s.Clock.Day);
        Assert.Equal(9, s.Clock.Hour);
        Assert.Equal("Mercredi", s.Clock.WeekDay);
        Assert.Equal("Matin", s.Clock.Period);
        Assert.Contains("Givrelune", s.Clock.DateText);
    }

    [Fact]
    public void Time_PassesWhenTravellingAndResting()
    {
        var s = NewGame();
        var before = s.State.Minutes;
        s.Travel("route_roi");
        Assert.Equal(before + GameDatabase.Default.Content.Time.TravelMinutes, s.State.Minutes);

        s.Execute(new GameAction(ActionType.WaitUntilHour, amount: 7));
        Assert.Equal(7, s.Clock.Hour);
        Assert.Equal(0, s.Clock.Minute);
    }

    [Fact]
    public void Time_CanBeDisabled()
    {
        var content = Sample();
        content.Time.Enabled = false;
        var s = NewGame(content);
        var before = s.State.Minutes;
        s.Travel("route_roi");
        Assert.Equal(before, s.State.Minutes);
    }

    [Fact]
    public void HourBetween_WrapsAroundMidnight()
    {
        Assert.True(GameSession.HourBetween(23, 22, 6));
        Assert.True(GameSession.HourBetween(3, 22, 6));
        Assert.False(GameSession.HourBetween(12, 22, 6));
        Assert.True(GameSession.HourBetween(10, 8, 18));
        Assert.False(GameSession.HourBetween(18, 8, 18));
    }

    [Fact]
    public void Karma_StartsFromCharacterOrDefaultAndIsClamped()
    {
        var content = Sample();
        content.Karma.Default = 5;
        content.Karma.Max = 20;
        content.Characters.First(c => c.Id == "lyra").BaseKarma = -10;
        var s = NewGame(content);
        s.Recruit("lyra");
        Assert.Equal(5, s.GetKarma("aldric"));
        Assert.Equal(-10, s.GetKarma("lyra"));

        s.Execute(new GameAction(ActionType.AddKarma, "@equipe", 100));
        Assert.Equal(20, s.GetKarma("aldric"));
        Assert.Equal(20, s.GetKarma("lyra"));
    }

    [Fact]
    public void Karma_TargetsTheSpeaker()
    {
        var s = NewGame();
        s.Recruit("lyra");
        s.SetSpeaker("lyra");
        s.Execute(new GameAction(ActionType.AddKarma, "", 7));
        Assert.Equal(7, s.GetKarma("lyra"));
        Assert.Equal(0, s.GetKarma("aldric"));
        Assert.True(s.Check(new Condition(ConditionType.Karma, "@parle", 5)));
        Assert.False(s.Check(new Condition(ConditionType.Karma, "@heros", 5)));
    }

    [Fact]
    public void Friendship_IsTrackedPerPersonAndTowardWhom()
    {
        var s = NewGame();
        s.Recruit("lyra");
        s.Execute(new GameAction(ActionType.AddFriendship, "fermier_joss", 10));
        s.Execute(new GameAction(ActionType.AddFriendship, "fermier_joss", 25) { Arg2 = "lyra" });
        Assert.Equal(10, s.GetFriendship("fermier_joss"));
        Assert.Equal(25, s.GetFriendship("fermier_joss", "lyra"));

        s.SetSpeaker("lyra");
        Assert.True(s.Check(new Condition(ConditionType.Friendship, "fermier_joss", 20) { Arg2 = "@parle" }));
        Assert.False(s.Check(new Condition(ConditionType.Friendship, "fermier_joss", 20)));
    }

    [Fact]
    public void Variables_AreClampedAndCompared()
    {
        var s = NewGame();
        s.Execute(new GameAction(ActionType.AddVariable, "reputation", 500));
        Assert.Equal(100, s.GetVariable("reputation"));
        Assert.True(s.Check(new Condition(ConditionType.Variable, "reputation", 100) { Op = CompareOp.Equal }));
        Assert.True(s.Check(new Condition(ConditionType.Variable, "inconnue", 1) { Op = CompareOp.Less }));
    }

    [Fact]
    public void Conditions_SupportNegationAndGroups()
    {
        var s = NewGame();
        s.SetFlag("a");
        var notA = new Condition(ConditionType.FlagSet, "a") { Negate = true };
        Assert.False(s.Check(notA));

        var anyOf = new Condition(ConditionType.AnyOf)
        {
            Children = [new(ConditionType.FlagSet, "b"), new(ConditionType.FlagSet, "a")],
        };
        Assert.True(s.Check(anyOf));

        var allOf = new Condition(ConditionType.AllOf)
        {
            Children = [new(ConditionType.FlagSet, "b"), new(ConditionType.FlagSet, "a")],
        };
        Assert.False(s.Check(allOf));
    }

    [Fact]
    public void Dialogue_UsesVariantBranchesAndCrossesIntoOtherDialogues()
    {
        var content = Sample();
        content.Dialogues.Add(new DialogueDef
        {
            Id = "croise_a",
            Nodes =
            [
                new()
                {
                    Id = "debut", Speaker = "Garde", Text = "Bonjour %pj%.",
                    Variants = [new() { Conditions = [new(ConditionType.FlagSet, "connu")], Text = "Re-bonjour %pj% !" }],
                    Branches = [new() { Conditions = [new(ConditionType.FlagSet, "connu")], NextId = "croise_b:suite" }],
                    NextId = "fin_a",
                },
                new() { Id = "fin_a", Text = "Fin A." },
            ],
        });
        content.Dialogues.Add(new DialogueDef
        {
            Id = "croise_b",
            Nodes = [new() { Id = "debut", Text = "Début B." }, new() { Id = "suite", Text = "Suite B, %var:reputation%." }],
        });
        Assert.Empty(new GameDatabase(content).Validate());

        var s = NewGame(content);
        var d = s.StartDialogue("croise_a");
        Assert.Equal("Bonjour Aldric.", d.Text);
        d.Continue();
        Assert.Equal("Fin A.", d.Text);

        s.SetFlag("connu");
        d = s.StartDialogue("croise_a");
        Assert.Equal("Re-bonjour Aldric !", d.Text);
        d.Continue();
        Assert.Equal("croise_b", d.Dialogue.Id);
        Assert.Equal("Suite B, 0.", d.Text);
    }

    [Fact]
    public void Dialogue_ShowsLockedChoicesButRefusesThem()
    {
        var content = Sample();
        content.Dialogues.Add(new DialogueDef
        {
            Id = "grise",
            Nodes =
            [
                new()
                {
                    Id = "debut", Text = "?",
                    Choices =
                    [
                        new() { Text = "Menacer", ShowLocked = true, LockedText = "Karma trop bas", Conditions = [new(ConditionType.Karma, "", -50) { Op = CompareOp.AtMost }] },
                        new() { Text = "Partir" },
                    ],
                },
            ],
        });
        var s = NewGame(content);
        var d = s.StartDialogue("grise");
        Assert.Equal(2, d.Options.Count);
        Assert.False(d.Options[0].Enabled);
        d.ChooseOption(0);
        Assert.False(d.IsFinished);
        d.ChooseOption(1);
        Assert.True(d.IsFinished);
    }

    [Fact]
    public void NpcSpecialDialogue_DependsOnWhoSpeaks()
    {
        var s = NewGame();
        s.Recruit("lyra");
        var d = s.StartDialogue(s.Talk("aubergiste", "lyra")!);
        Assert.StartsWith("Lyra !", d.Text);
        d = s.StartDialogue(s.Talk("aubergiste", "aldric")!);
        Assert.StartsWith("On raconte", d.Text);
    }

    [Fact]
    public void Npcs_FollowPlacementsAndMoves()
    {
        var content = Sample();
        var joss = content.Npcs.First(n => n.Id == "fermier_joss");
        joss.Placements = [new() { LocationId = "route_roi", Conditions = [new(ConditionType.FlagSet, "joss_aux_champs")] }];
        var s = NewGame(content);
        Assert.Contains(s.VisibleNpcs, n => n.Id == "fermier_joss");
        s.SetFlag("joss_aux_champs");
        Assert.DoesNotContain(s.VisibleNpcs, n => n.Id == "fermier_joss");

        s.Execute(new GameAction(ActionType.MoveNpc, "fermier_joss") { Arg2 = "havrefort" });
        Assert.Contains(s.VisibleNpcs, n => n.Id == "fermier_joss");
    }

    [Fact]
    public void HiddenLocations_AppearWhenRevealed()
    {
        var content = Sample();
        content.Locations.First(l => l.Id == "route_roi").VisibleConditions = [new(ConditionType.FlagSet, "carte")];
        var s = NewGame(content);
        Assert.DoesNotContain(s.Destinations, l => l.Id == "route_roi");
        Assert.False(s.Travel("route_roi").Success);
        s.Execute(new GameAction(ActionType.RevealLocation, "route_roi"));
        Assert.Contains(s.Destinations, l => l.Id == "route_roi");
    }

    [Fact]
    public void Vocabulary_CanBeRenamed()
    {
        var content = Sample();
        content.World.Texts["money"] = "écus";
        var db = new GameDatabase(content);
        Assert.Equal("écus", db.T("money"));
        Assert.Equal("PV", db.T("hp"));
        var s = GameSession.NewGame(db, "aldric");
        s.Execute(new GameAction(ActionType.GiveGold, amount: 5));
        Assert.Contains("5 écus", s.Notifications.Last());
        Assert.Equal("Valdor", s.FormatText("%pays%"));
    }

    [Fact]
    public void OldSave_IsMigrated()
    {
        var s = NewGame();
        s.State.Version = 1;
        s.State.Minutes = 0;
        var migrated = new GameSession(GameDatabase.Default, s.State);
        Assert.Equal(GameState.CurrentVersion, migrated.State.Version);
        Assert.Equal(GameClock.StartMinutes(GameDatabase.Default.Content.Time), migrated.State.Minutes);
    }

    [Fact]
    public void Script_RoundTripsNewSyntax()
    {
        const string script = """
            Garde: Halte, %pj% ! [karma -5 @equipe] [amitie garde 3]
            ~ {karma >= 20} Garde: Ah, c'est vous. Passez.
            >~ Menacer -> fin {!flag lache | karma < -10 & taille_equipe >= 2} ((Pas assez redoutable))
            > Attendre la nuit -> nuit {heure 20 6} [attendre 22]
            > Parler de l'autre affaire -> autre_dialogue:debut
            ? {parle lyra} -> lyra
            -> fin
            @nuit
            - La relève arrive. [message Le garde s'endort.] [temps 30] [var ronde 2]
            @lyra
            Garde: Une mage ? {…}
            """;
        var nodes = DialogueScript.Parse(script.Replace(" {…}", ""), out var errors);
        Assert.Empty(errors);
        var first = nodes[0];
        Assert.Equal(ActionType.AddKarma, first.Actions[0].Type);
        Assert.Equal(-5, first.Actions[0].Amount);
        Assert.Equal("@equipe", first.Actions[0].Arg);
        Assert.Equal("garde", first.Actions[1].Arg);
        Assert.Equal(3, first.Actions[1].Amount);
        Assert.Single(first.Variants);
        Assert.Equal(CompareOp.AtLeast, first.Variants[0].Conditions[0].Op);
        Assert.True(first.Choices[0].ShowLocked);
        Assert.Equal("Pas assez redoutable", first.Choices[0].LockedText);
        var any = first.Choices[0].Conditions.Single();
        Assert.Equal(ConditionType.AnyOf, any.Type);
        Assert.True(any.Children![0].Negate);
        Assert.Equal(ConditionType.AllOf, any.Children[1].Type);
        Assert.Equal("autre_dialogue:debut", first.Choices[2].NextId);
        Assert.Equal("lyra", first.Branches.Single().NextId);
        Assert.Equal("Le garde s'endort.", nodes[1].Actions[0].Arg);

        var written = DialogueScript.Write(nodes);
        var again = DialogueScript.Parse(written, out var errors2);
        Assert.Empty(errors2);
        Assert.Equal(written, DialogueScript.Write(again));
    }

    [Fact]
    public void Merge_KeepsNewSettings()
    {
        var @base = Sample();
        var local = Sample();
        local.World.CountryName = "Aldoria";
        local.Variables.Add(new VariableDef { Id = "dette", Name = "Dette" });
        var remote = Sample();
        remote.Time.TravelMinutes = 120;
        var merged = Cloud.ContentMerger.Merge(@base, local, remote).Merged;
        Assert.Equal("Aldoria", merged.World.CountryName);
        Assert.Equal(120, merged.Time.TravelMinutes);
        Assert.Contains(merged.Variables, v => v.Id == "dette");
    }
}
