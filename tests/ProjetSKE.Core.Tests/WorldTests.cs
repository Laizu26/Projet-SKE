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

public class BattleLineTests
{
    [Fact]
    public void BattleLines_AreSaidAtTheRightMoments()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Characters.First(c => c.Id == "aldric").BattleLines =
        [
            new() { Trigger = BattleTrigger.Start, Text = "Pour %pays% !" },
            new() { Trigger = BattleTrigger.Kill, Text = "Au suivant.", Actions = [new(ActionType.AddVariable, "reputation", 1)] },
            new() { Trigger = BattleTrigger.Victory, Text = "Jamais.", Conditions = [new(ConditionType.FlagSet, "absent")] },
        ];
        content.Monsters.First(m => m.Id == "gobelin").BattleLines =
        [
            new() { Trigger = BattleTrigger.HpBelow, Amount = 99, Text = "Aïe !" },
            new() { Trigger = BattleTrigger.Down, Text = "Argh..." },
        ];
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(3));
        var battle = s.StartBattle(["gobelin"]);
        Assert.Contains("Aldric : « Pour Valdor ! »", battle.Log);
        for (var i = 0; i < 200 && battle.Outcome == BattleOutcome.Ongoing; i++)
            battle.UseSkill(battle.CurrentActor!.Skills[0], battle.Enemies[0]);

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Single(battle.Log, l => l.Contains("Aïe !"));
        Assert.Contains(battle.Log, l => l.Contains("Argh..."));
        Assert.Contains(battle.Log, l => l.Contains("Au suivant."));
        Assert.DoesNotContain(battle.Log, l => l.Contains("Jamais."));
        Assert.Equal(1, s.GetVariable("reputation"));
        Assert.All(battle.SpeechLines, i => Assert.Contains("«", battle.Log[i]));
    }
}

public class CampTests
{
    private static GameSession NewGame(int seed = 5) => GameSession.NewGame(GameDatabase.Default, "aldric", new Random(seed));

    [Fact]
    public void Camp_StartsWithResidentsAndRanks()
    {
        var s = NewGame();
        Assert.Equal("soldat", s.CampMember("bran")!.RankId);
        Assert.Equal("recrue", s.CampMember("mara")!.RankId);
    }

    [Fact]
    public void Camp_RanksHaveLimitedSlots()
    {
        var s = NewGame();
        Assert.True(s.SetCampRank("bran", "lieutenant"));
        Assert.False(s.SetCampRank("mara", "lieutenant"));
        Assert.True(s.Check(new Condition(ConditionType.CampRank, "bran", 2)));
    }

    [Fact]
    public void Camp_TasksNeedRankAndProduceResultsOverTime()
    {
        var s = NewGame();
        Assert.False(s.SetCampTask("mara", "chasse")); // recrue : grade trop bas
        Assert.True(s.SetCampTask("bran", "chasse"));
        Assert.True(s.SetCampTask("mara", "rondes"));

        s.AdvanceTime(24 * 60);
        Assert.Equal(6 + 4, s.State.CampLog.Count); // 24 h : 6 chasses de 4 h et 4 rondes de 6 h
        Assert.All(s.State.CampLog, l => Assert.DoesNotContain("%membre%", l));
        Assert.Contains(s.State.CampLog, l => l.Contains("Bran"));
        Assert.DoesNotContain(s.Notifications, n => n.StartsWith("Obtenu"));
    }

    [Fact]
    public void Camp_MemberContextTargetsTheWorker()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var task = content.Camp.Tasks.First(t => t.Id == "repos");
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        var before = s.GetFriendship("mara");
        s.SetCampTask("mara", "repos");
        s.AdvanceTime(task.DurationMinutes);
        Assert.Equal(before + 2, s.GetFriendship("mara"));
        Assert.Equal(10, s.GetFriendship("bran"));
    }

    [Fact]
    public void Camp_DialogueCanPromote()
    {
        var s = NewGame();
        var d = s.StartDialogue(s.Talk("mara")!);
        d.Choose(0);
        Assert.Equal("soldat", s.CampMember("mara")!.RankId);
        Assert.Equal(15, s.GetFriendship("mara"));
        Assert.Contains("Merci, Aldric", d.Text);
    }

    [Fact]
    public void Resources_StartWithInitialStockAndAreConsumedDaily()
    {
        var s = NewGame();
        Assert.Equal(10, s.GetCampResource("nourriture"));
        var residents = s.State.Camp.Count;
        s.AdvanceTime(s.Db.Content.Time.HoursPerDay * 60);
        Assert.Equal(10 - residents, s.GetCampResource("nourriture"));
        Assert.Equal(5, s.GetCampResource("bois")); // pas de consommation
    }

    [Fact]
    public void Resources_ShortageLowersFriendship()
    {
        var s = NewGame();
        s.AddCampResource("nourriture", -100);
        Assert.Equal(0, s.GetCampResource("nourriture"));
        var before = s.GetFriendship("bran");
        s.AdvanceTime(s.Db.Content.Time.HoursPerDay * 60);
        Assert.Equal(before - 3, s.GetFriendship("bran"));
        Assert.Contains(s.State.CampLog, l => l.Contains("Pénurie"));
    }

    [Fact]
    public void Resources_AreCappedAndUsableInScripts()
    {
        var s = NewGame();
        s.Execute(new GameAction(ActionType.AddCampResource, "bois", 500));
        Assert.Equal(80, s.GetCampResource("bois"));
        Assert.True(s.Check(new Condition(ConditionType.CampResource, "bois", 80)));
    }

    [Fact]
    public void Buildings_CostResourcesAndUnlockTasks()
    {
        var s = NewGame();
        var atelier = s.CampRules.Buildings.First(b => b.Id == "atelier");
        Assert.NotNull(s.CannotBuild(atelier)); // pas assez de bois
        Assert.False(s.Build("atelier"));

        s.AddCampResource("bois", 20);
        s.State.Gold = 100;
        Assert.Null(s.CannotBuild(atelier));
        Assert.True(s.Build("atelier"));
        Assert.Equal(15, s.GetCampResource("bois"));
        Assert.Equal(70, s.State.Gold);
        Assert.True(s.Check(new Condition(ConditionType.CampBuilt, "atelier")));
        Assert.False(s.Build("atelier"));
        Assert.True(s.SetCampTask("bran", "forge"));
    }

    [Fact]
    public void Buildings_ApplyTheirEffects()
    {
        var s = NewGame();
        var before = s.GetVariable("reputation");
        s.Execute(new GameAction(ActionType.BuildCampBuilding, "palissade"));
        Assert.True(s.IsBuilt("palissade"));
        Assert.Equal(before + 3, s.GetVariable("reputation"));
        Assert.Equal(5, s.GetCampResource("bois")); // construit par un effet : gratuit
    }

    [Fact]
    public void Script_ParsesResourceWords()
    {
        var script = "Bran: Du bois ! [ressource bois 4] [construit palissade]\n> Et ? {ressource bois >= 4} {construit palissade}";
        var nodes = DialogueScript.Parse(script, out var errors);
        Assert.Empty(errors);
        var node = nodes[0];
        Assert.Contains(node.Actions, a => a.Type == ActionType.AddCampResource && a.Arg == "bois" && a.Amount == 4);
        Assert.Contains(node.Actions, a => a.Type == ActionType.BuildCampBuilding && a.Arg == "palissade");
        var conds = node.Choices[0].Conditions;
        Assert.Contains(conds, c => c.Type == ConditionType.CampResource && c.Amount == 4);
        Assert.Contains(conds, c => c.Type == ConditionType.CampBuilt && c.Arg == "palissade");
    }
}

public class NarrationTests
{
    [Fact]
    public void Script_AcceptsSeveralNarrationForms()
    {
        var script = "- La porte grince.\n* Un silence pesant.\nNarration: La nuit tombe.\nNarrateur: Au loin, un loup.\nBran: Qui va là ?\nLe vent souffle.";
        var nodes = DialogueScript.Parse(script, out var errors);
        Assert.Empty(errors);
        Assert.Equal(6, nodes.Count);
        Assert.All(nodes.Where(n => n.Speaker.Length == 0), n => Assert.DoesNotContain(":", n.Text));
        Assert.Equal(["", "", "", "", "Bran", ""], nodes.Select(n => n.Speaker).ToArray());
        Assert.Equal("La nuit tombe.", nodes[2].Text);
        var again = DialogueScript.Parse(DialogueScript.Write(nodes), out var errors2);
        Assert.Empty(errors2);
        Assert.Equal(nodes.Select(n => (n.Speaker, n.Text)), again.Select(n => (n.Speaker, n.Text)));
    }

    [Fact]
    public void Script_ChoiceCanBeNarration()
    {
        var script = "Bran: Tu restes ?\n> Oui, je reste. -> fin\n> * Tu t'éloignes sans un mot. -> fin";
        var nodes = DialogueScript.Parse(script, out var errors);
        Assert.Empty(errors);
        var choices = nodes[0].Choices;
        Assert.False(choices[0].Narration);
        Assert.True(choices[1].Narration);
        Assert.Equal("Tu t'éloignes sans un mot.", choices[1].Text);
        var again = DialogueScript.Parse(DialogueScript.Write(nodes), out _);
        Assert.True(again[0].Choices[1].Narration);
        Assert.Equal("Tu t'éloignes sans un mot.", again[0].Choices[1].Text);
    }
}

public class IsHeroTests
{
    [Fact]
    public void IsHero_ChecksTheCharacterPlayedByThePlayer()
    {
        var s = GameSession.NewGame(GameDatabase.Default, "tobin", new Random(1));
        s.Recruit("aldric");
        Assert.True(s.Check(new Condition(ConditionType.IsHero, "tobin")));
        Assert.False(s.Check(new Condition(ConditionType.IsHero, "aldric"))); // dans l'équipe, mais pas incarné
        var nodes = DialogueScript.Parse("- Test {etre tobin}\n> Choix {etre aldric}", out var errors);
        Assert.Empty(errors);
        Assert.Equal(ConditionType.IsHero, nodes[0].Choices[0].Conditions[0].Type);
    }
}

public class CrackTests
{
    [Fact]
    public void Level_FollowsTheHeroHpThresholds()
    {
        var cracks = new CrackSettings();
        Assert.Equal(0, cracks.Level(100));
        Assert.Equal(0, cracks.Level(30));
        Assert.Equal(1, cracks.Level(29));
        Assert.Equal(2, cracks.Level(14));
        Assert.Equal(3, cracks.Level(4));
        Assert.Equal(3, cracks.Level(0));
        Assert.Equal(3, cracks.MaxLevel);
        cracks.Enabled = false;
        Assert.Equal(0, cracks.Level(0));
    }

    [Fact]
    public void HeroHpPercent_OnlyCountsTheHero()
    {
        var s = GameSession.NewGame(GameDatabase.Default, "aldric", new Random(1));
        s.Recruit("lyra");
        Assert.Equal(100, s.HeroHpPercent);
        s.State.Party.First(c => c.DefId == "lyra").CurrentHp = 1; // un compagnon blessé ne compte pas
        Assert.Equal(100, s.HeroHpPercent);
        var hero = s.State.Party.First(c => c.DefId == "aldric");
        hero.CurrentHp = s.GetStats(hero).MaxHp / 10;
        Assert.InRange(s.HeroHpPercent, 9, 11);
        Assert.Equal(2, s.Db.Content.World.Cracks.Level(s.HeroHpPercent));
    }
}

public class DifferenceTests
{
    [Fact]
    public void CountDifferences_CountsChangedAddedAndRemovedElements()
    {
        var a = ContentSerializer.Clone(GameDatabase.Default.Content);
        var b = ContentSerializer.Clone(a);
        Assert.Equal(0, Cloud.ContentMerger.CountDifferences(a, b));
        b.Npcs[0].Name = "Autre";
        b.Items.RemoveAt(0);
        b.Variables.Add(new VariableDef { Id = "x" });
        b.World.CountryName = "Ailleurs";
        Assert.Equal(4, Cloud.ContentMerger.CountDifferences(a, b));
    }
}

public class StoryTests
{
    private static GameSession NewGame(string? start = null) => GameSession.NewGame(GameDatabase.Default, "aldric", start, new Random(2));

    [Fact]
    public void Starts_AreListedAndApplied()
    {
        var db = GameDatabase.Default;
        Assert.Equal(["principal", "exile"], db.Starts.Select(s => s.Id).ToArray());
        Assert.Equal("principal", db.StartFor("aldric").Id);
        Assert.Equal("exile", db.StartFor("tobin").Id);

        var s = NewGame("exile");
        Assert.Equal("foret_sombrebois", s.State.CurrentLocationId);
        Assert.Equal(15, s.State.Gold);
        Assert.Equal(-10, s.GetKarma("@heros"));
        Assert.Equal(-20, s.GetVariable("reputation"));
        Assert.True(s.HasFlag("exile"));
        Assert.Equal(5, s.Clock.Hour);
        Assert.Equal("exile", s.State.StartId);
        Assert.Equal("principal", NewGame().State.StartId);
    }

    [Fact]
    public void Starts_DependOnTheHero()
    {
        // Sans départ précisé, la partie commence au départ du héros.
        var tobin = GameSession.NewGame(GameDatabase.Default, "tobin", new Random(1));
        Assert.Equal("exile", tobin.State.StartId);
        Assert.Equal("foret_sombrebois", tobin.State.CurrentLocationId);
        Assert.Equal("principal", GameSession.NewGame(GameDatabase.Default, "aldric", new Random(1)).State.StartId);

        // Départ inconnu : départ principal. Ancien réglage (héros cités par le départ) : encore compris.
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Characters.First(c => c.Id == "tobin").StartId = "disparu";
        content.ExtraStarts[0].HeroIds = ["aldric"];
        var db = new GameDatabase(content);
        Assert.Equal("principal", db.StartFor("tobin").Id);
        Assert.Equal("exile", db.StartFor("aldric").Id);
        Assert.Contains(db.Validate(), e => e.Contains("disparu"));
    }

    private static void TravelTo(GameSession s, params string[] path)
    {
        foreach (var id in path) Assert.True(s.Travel(id).Success, id);
    }

    [Fact]
    public void StagedQuest_AutoStartsAndBranchesOnChoice()
    {
        var s = NewGame();
        s.Config.TravelEncounters = TravelEncounterMode.None;
        TravelTo(s, "route_roi", "bourg_brume");
        Assert.Equal(QuestStatus.Active, s.GetQuestStatus("rancon"));
        Assert.Equal("enquete", s.QuestProgressOf("rancon")!.StageId);

        s.Talk("aubergiste");
        Assert.Equal("ravisseurs", s.QuestProgressOf("rancon")!.StageId);
        Assert.True(s.Check(new Condition(ConditionType.QuestAtStage, "rancon") { Arg2 = "ravisseurs" }));

        TravelTo(s, "route_roi", "foret_sombrebois");
        Assert.Contains(s.VisibleNpcs, n => n.Id == "ravisseur");
        s.State.Gold = 100;
        var d = s.StartDialogue(s.Talk("ravisseur")!);
        d.ChooseOption(0); // payer

        var p = s.QuestProgressOf("rancon")!;
        Assert.Equal(QuestStatus.Completed, p.Status);
        Assert.Equal("paix", p.EndingId);
        Assert.Equal(["enquete", "ravisseurs", "paix"], p.Path);
        Assert.Equal(50, s.State.Gold);
        Assert.DoesNotContain(s.VisibleNpcs, n => n.Id == "ravisseur");

        // Le monde a changé : Olric est de retour à Havrefort et se souvient de la rançon.
        TravelTo(s, "route_roi", "havrefort");
        Assert.Contains(s.VisibleNpcs, n => n.Id == "olric");
        Assert.Contains("rançon", s.StartDialogue(s.Talk("olric")!).Text);
    }

    [Fact]
    public void StagedQuest_AssaultPathNeedsVictory()
    {
        var s = NewGame();
        s.Config.TravelEncounters = TravelEncounterMode.None;
        s.GoToStage("rancon", "ravisseurs");
        TravelTo(s, "route_roi", "foret_sombrebois");
        var d = s.StartDialogue(s.Talk("ravisseur")!);
        d.ChooseOption(1); // assaut
        Assert.NotNull(d.PendingBattle);
        Assert.Equal("assaut", s.QuestProgressOf("rancon")!.StageId);
        s.UpdateQuests(ObjectiveType.Defeat, "bandit");
        s.UpdateQuests(ObjectiveType.Defeat, "bandit");
        Assert.Equal("libere", s.QuestProgressOf("rancon")!.EndingId);
    }

    [Fact]
    public void StagedQuest_CanFail()
    {
        var s = NewGame();
        s.GoToStage("rancon", "ravisseurs");
        s.State.CurrentLocationId = "foret_sombrebois";
        s.SetFlag("rancon_abandon");
        s.UpdateQuests();
        Assert.Equal(QuestStatus.Failed, s.GetQuestStatus("rancon"));
        Assert.True(s.Check(new Condition(ConditionType.QuestFailed, "rancon")));
        Assert.Equal(-10, s.GetKarma("@heros"));
    }

    [Fact]
    public void Script_RoundTripsQuestWords()
    {
        var nodes = DialogueScript.Parse("- ? {etape rancon ravisseurs} {!fin rancon paix} [etape rancon assaut] [echouer rancon]\n> ok {passe rancon enquete}", out var errors);
        Assert.Empty(errors);
        var written = DialogueScript.Write(nodes);
        Assert.Contains("[etape rancon assaut]", written);
        Assert.Contains("{passe rancon enquete}", written);
    }
}

public class ClassTitleTests
{
    [Fact]
    public void ClassAndTitleAreSeparate()
    {
        var aldric = GameDatabase.Default.Characters["aldric"];
        Assert.Equal("Chevalier", aldric.Class);
        Assert.Equal("Chevalier errant", aldric.Title);
        Assert.Equal("Chevalier · Chevalier errant", aldric.ClassAndTitle);
        var s = GameSession.NewGame(GameDatabase.Default, "aldric");
        Assert.Equal("Chevalier / Chevalier errant", s.FormatText("%classe% / %titre%"));
        Assert.Equal("Mage", s.FormatText("%classe:lyra%"));
    }
}
