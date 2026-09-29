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

/// <summary>« A choisi » : un choix fait peut conditionner la suite du dialogue (ou toute autre chose, plus tard).</summary>
public class ChoiceMadeTests
{
    private const string Script = "Bran: Tu m'aides ?\n> Oui, bien sûr #oui -> suite\n> Non -> suite\n@suite\nsi {choisi test debut:oui} Bran: Merci !\nsi {choisi test debut:2} Bran: Tant pis.\n- Il s'éloigne.";

    private static (GameSession, DialogueRunner) Run(int choice)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Dialogues.Add(new DialogueDef { Id = "test", Nodes = DialogueScript.Parse(Script, out var errors) });
        Assert.Empty(errors);
        Assert.Empty(new GameDatabase(content).Validate());
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        var d = s.StartDialogue("test");
        d.Choose(choice);
        return (s, d);
    }

    [Fact]
    public void ChoiceMade_DrivesTheNextLines()
    {
        var (s, yes) = Run(0);
        Assert.Equal("Merci !", yes.Text);
        Assert.True(s.Check(new Condition(ConditionType.ChoiceMade, "test") { Arg2 = "debut:oui" }));
        Assert.False(s.Check(new Condition(ConditionType.ChoiceMade, "test") { Arg2 = "debut:2" }));

        var (_, no) = Run(1);
        Assert.Equal("Tant pis.", no.Text);
    }

    [Fact]
    public void ChoiceId_RoundTripsThroughText()
    {
        var nodes = DialogueScript.Parse(Script, out _);
        Assert.Equal("oui", nodes[0].Choices[0].Id);
        Assert.Equal("Oui, bien sûr", nodes[0].Choices[0].Text);
        Assert.Equal("2", nodes[0].ChoiceKey(nodes[0].Choices[1]));
        var again = DialogueScript.Parse(DialogueScript.Write(nodes), out var errors);
        Assert.Empty(errors);
        Assert.Equal("oui", again[0].Choices[0].Id);
        Assert.Equal(ConditionType.ChoiceMade, again[1].Conditions[0].Type);
    }
}

/// <summary>Effet « Dialogue : lancer » et dialogues d'introduction multiples.</summary>
public class StartDialogueTests
{
    [Fact]
    public void StartDialogue_QueuesDialoguesInOrder()
    {
        var s = GameSession.NewGame(GameDatabase.Default, "aldric", new Random(1));
        var ids = GameDatabase.Default.Content.Dialogues.Take(2).Select(d => d.Id).ToList();
        foreach (var id in ids) s.Execute(new GameAction(ActionType.StartDialogue, id));
        s.Execute(new GameAction(ActionType.StartDialogue, "inexistant"));
        Assert.Equal(ids, s.PendingDialogues.ToList());

        var nodes = DialogueScript.Parse($"- Test [dialogue {ids[0]}]", out var errors);
        Assert.Empty(errors);
        Assert.Equal(ActionType.StartDialogue, nodes[0].Actions[0].Type);
    }

    [Fact]
    public void Start_CanHaveSeveralIntroDialoguesAndQuestsCanLaunchDialogues()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Start.MoreIntroDialogueIds = ["intro_exile"];
        content.Quests.First(q => q.Id == "rumeurs").Rewards.Add(new GameAction(ActionType.StartDialogue, "intro"));
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        Assert.Equal(["intro", "intro_exile"], db.Start.IntroDialogues.ToArray());

        content.Start.MoreIntroDialogueIds = ["disparu"];
        Assert.Contains(new GameDatabase(content).Validate(), e => e.Contains("disparu"));
    }
}

/// <summary>Liens entre répliques (garde-fous de l'éditeur de dialogues).</summary>
public class DialogueGraphTests
{
    [Fact]
    public void Graph_FindsBrokenAndUnreachableAndCleansOnRemove()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var nodes = DialogueScript.Parse("Bran: Salut.\n> Oui -> b\n> Non -> c\n@b\n- B.\n-> fin\n@c\n- C.\n-> fin\n@seul\n- Personne ne vient ici.", out _);
        var d = new DialogueDef { Id = "g", Nodes = nodes };
        content.Dialogues.Add(d);
        Assert.Empty(DialogueGraph.Broken(content, d));
        Assert.Equal(["seul"], DialogueGraph.Unreachable(content, d).Select(n => n.Id));

        var c = d.Nodes.First(n => n.Id == "c");
        Assert.Single(DialogueGraph.Incoming(content, d, c));
        Assert.Equal(1, DialogueGraph.Remove(content, d, c));
        Assert.Null(d.Nodes[0].Choices[1].NextId); // le choix mène à la fin, plus à une réplique disparue
        Assert.Empty(DialogueGraph.Broken(content, d));

        d.Nodes[0].Choices[0].NextId = "disparue";
        Assert.Single(DialogueGraph.Broken(content, d));

        var copy = ContentSerializer.Clone(d.Nodes[0]);
        Assert.Equal(d.Nodes[0].Text, copy.Text);
        Assert.NotSame(d.Nodes[0].Choices, copy.Choices);
    }
}

/// <summary>Une réplique peut mêler narration et paroles : en jeu, une bulle par morceau.</summary>
public class SegmentTests
{
    [Fact]
    public void Segments_SplitNarrationAndSpeech()
    {
        var parts = DialogueScript.Segments("", "- La porte grince.\nBran: Qui va là ?\nPersonne ne répond.\n\n- Il lève sa lanterne.");
        Assert.Equal([("", "La porte grince."), ("Bran", "Qui va là ?\nPersonne ne répond."), ("", "Il lève sa lanterne.")], parts);
        Assert.Equal([("Bran", "Bonjour. Note : rien.")], DialogueScript.Segments("Bran", "Bonjour. Note : rien."));
        Assert.Equal([("Bran", "Il dit alors ceci: rien.")], DialogueScript.Segments("Bran", "Il dit alors ceci: rien."));
    }

    [Fact]
    public void Runner_ShowsEachBubbleThenTheChoices()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var nodes = DialogueScript.Parse("- La porte grince.\n+ Bran: Qui va là ?\n+ - Il lève sa lanterne.\n> Moi. -> fin\n> Personne. -> fin", out var errors);
        Assert.Empty(errors);
        Assert.Single(nodes);
        content.Dialogues.Add(new DialogueDef { Id = "bulles", Nodes = nodes });
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        var d = s.StartDialogue("bulles");

        Assert.Equal(("", "La porte grince."), (d.Speaker, d.Text));
        Assert.False(d.HasOptions); // les choix attendent la dernière bulle
        d.Continue();
        Assert.Equal(("Bran", "Qui va là ?"), (d.Speaker, d.Text));
        d.Continue();
        Assert.Equal("Il lève sa lanterne.", d.Text);
        Assert.True(d.HasOptions);
        Assert.Equal(2, d.Choices.Count);

        var again = DialogueScript.Parse(DialogueScript.Write(nodes), out var errors2);
        Assert.Empty(errors2);
        Assert.Equal(nodes[0].Text, again[0].Text);
    }
}

public class DialogueStyleTests
{
    [Fact]
    public void Style_DefaultsToClassic_AndSurvivesSave()
    {
        var content = ContentSerializer.LoadDefault();
        Assert.All(content.Dialogues, d => Assert.Equal(DialogueStyle.Classic, d.Style));
        content.Dialogues[0].Style = DialogueStyle.Cinematic;
        var json = ContentSerializer.ToJson(content);
        Assert.Contains("\"Cinematic\"", json);
        Assert.Equal(DialogueStyle.Cinematic, ContentSerializer.FromJson(json).Dialogues[0].Style);
    }
}

public class QuestWithoutObjectiveTests
{
    [Fact]
    public void QuestWithoutObjective_StaysActiveUntilCompleteEffect()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Quests.Add(new QuestDef { Id = "libre", Name = "Sans objectif", Rewards = [new(ActionType.SetFlag, "libre_finie")] });
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));

        s.Execute(new GameAction(ActionType.StartQuest, "libre"));
        s.UpdateQuests();
        s.UpdateQuests(ObjectiveType.TalkTo, "olric");
        Assert.Equal(QuestStatus.Active, s.GetQuestStatus("libre"));
        Assert.DoesNotContain("libre_finie", s.State.Flags);

        s.Execute(new GameAction(ActionType.CompleteQuest, "libre"));
        Assert.Equal(QuestStatus.Completed, s.GetQuestStatus("libre"));
        Assert.Contains("libre_finie", s.State.Flags);
    }
}

public class NpcCombatTests
{
    private static GameSession Game(Action<NpcDef>? change = null)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var npc = content.Npcs.First(n => n.Id == "capitaine_hardin");
        change?.Invoke(npc);
        Assert.Empty(new GameDatabase(content).Validate());
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        s.State.Config.TravelEncounters = TravelEncounterMode.None;
        return s;
    }

    [Fact]
    public void FightingNpc_IsAnOpponent_AndBeatenFlagIsSet()
    {
        var s = Game();
        Assert.True(s.Db.Monsters.ContainsKey("capitaine_hardin"));
        Assert.Equal("Capitaine Hardin", s.Db.Monsters["capitaine_hardin"].Name);
        Assert.Equal(["capitaine_hardin"], s.Execute(new GameAction(ActionType.StartBattle, "capitaine_hardin")));

        var battle = s.StartBattle(["capitaine_hardin"]);
        s.ApplyVictory(battle);
        Assert.True(s.HasFlag(GameSession.NpcBeatenFlag("capitaine_hardin")));
        Assert.DoesNotContain(s.GetEncyclopedia(EncyclopediaCategory.Monsters), e => e.Name == "Capitaine Hardin");
    }

    [Fact]
    public void NpcWithoutCombat_IsNotAnOpponent()
    {
        var s = Game(n => n.Combat = null);
        Assert.False(s.Db.Monsters.ContainsKey("capitaine_hardin"));
    }

    [Fact]
    public void HostileNpc_AttacksOnArrival_UntilBeaten()
    {
        var s = Game(n =>
        {
            n.LocationId = "route_roi";
            n.Combat!.Attacks = true;
            n.Combat.AllyIds = ["bandit"];
            n.Combat.AttackConditions = [new(ConditionType.FlagNotSet, "paix")];
        });
        s.State.CurrentLocationId = "havrefort";

        var arrival = s.Travel("route_roi");
        Assert.Equal(["capitaine_hardin", "bandit"], arrival.BattleMonsterIds);

        s.SetFlag("paix");
        Assert.Null(s.HostileNpc());
        s.State.Flags.Remove("paix");
        Assert.NotNull(s.HostileNpc());

        s.ApplyVictory(s.StartBattle(arrival.BattleMonsterIds!));
        Assert.Null(s.HostileNpc());
    }
}

public class SubLocationTests
{
    private static GameSession Game(Action<GameContent>? change = null)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Locations.Add(new LocationDef { Id = "cave", Name = "Cave", Type = LocationType.Dungeon, ParentId = "taverne_sanglier" });
        change?.Invoke(content);
        Assert.Empty(new GameDatabase(content).Validate());
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        s.State.Config.TravelEncounters = TravelEncounterMode.None;
        s.State.CurrentLocationId = "havrefort";
        return s;
    }

    [Fact]
    public void EnterAndExit_SubLocations()
    {
        var s = Game();
        Assert.Contains(s.SubLocations, l => l.Id == "taverne_sanglier");
        Assert.False(s.Travel("cave").Success); // pas directement : il faut passer par la taverne

        Assert.True(s.Travel("taverne_sanglier").Success);
        Assert.Equal("havrefort", s.RootLocation.Id);
        Assert.True(s.Travel("cave").Success);
        Assert.Equal(["havrefort", "taverne_sanglier", "cave"], s.Db.PathOf(s.CurrentLocation).Select(l => l.Id));

        // Depuis la cave : voyager dans le royaume (depuis Havrefort) ou ressortir d'un coup.
        Assert.Contains(s.Destinations, l => l.Id == "route_roi");
        Assert.True(s.Travel("havrefort").Success);
        Assert.Equal("havrefort", s.State.CurrentLocationId);
    }

    [Fact]
    public void BeingInsideCountsAsBeingAtTheParent()
    {
        var s = Game();
        s.Travel("taverne_sanglier");
        Assert.True(s.Check(new Condition(ConditionType.AtLocation, "havrefort")));
        Assert.True(s.Check(new Condition(ConditionType.AtLocation, "taverne_sanglier")));
        Assert.False(s.Check(new Condition(ConditionType.AtLocation, "route_roi")));
    }

    [Fact]
    public void SubLocations_AreNotOnTheKingdomMap_AndTravelFromInside()
    {
        var s = Game();
        Assert.DoesNotContain("taverne_sanglier", WorldLayout.Compute(s.Db).Keys);
        s.Travel("taverne_sanglier");
        Assert.True(s.Travel("route_roi").Success);
    }

    [Fact]
    public void LockedSubLocation_CannotBeEntered_ButCanAlwaysBeLeft()
    {
        var s = Game(c => c.Locations.First(l => l.Id == "cave").AccessConditions = [new(ConditionType.FlagSet, "cle_cave")]);
        s.Travel("taverne_sanglier");
        Assert.False(s.Travel("cave").Success);
        s.SetFlag("cle_cave");
        Assert.True(s.Travel("cave").Success);
        s.State.Flags.Remove("cle_cave");
        Assert.True(s.Travel("taverne_sanglier").Success);
    }

    [Fact]
    public void ParentLoop_IsReported()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Locations.First(l => l.Id == "havrefort").ParentId = "taverne_sanglier";
        Assert.Contains(new GameDatabase(content).Validate(), e => e.Contains("boucle"));
    }
}

public class TutorialTests
{
    private static GameDatabase Db(Action<TutorialSettings>? change = null)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Tutorial.Enabled = true;
        content.Tutorial.HeroId = "aldric";
        content.Tutorial.Start.LocationId = "taverne_sanglier";
        content.Tutorial.Start.Actions = [new(ActionType.SetFlag, "reve")];
        change?.Invoke(content.Tutorial);
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        return db;
    }

    [Fact]
    public void Tutorial_StartsLocked_AndUnlocksStepByStep()
    {
        var s = GameSession.NewTutorial(Db(), new Random(1));
        Assert.True(s.State.IsTutorial);
        Assert.Equal("taverne_sanglier", s.State.CurrentLocationId);
        Assert.True(s.HasFlag("reve"));
        Assert.True(s.IsLocked(UiFeature.WorldMap));
        Assert.False(s.IsLocked(UiFeature.TabMap));

        s.Execute(new GameAction(ActionType.UnlockFeature, nameof(UiFeature.TabQuests)));
        Assert.False(s.IsLocked(UiFeature.TabQuests));
        Assert.Contains(s.Notifications, n => n.Contains("Quêtes"));
        s.Execute(new GameAction(ActionType.LockFeature, nameof(UiFeature.BattleFlee)));
        Assert.True(s.IsLocked(UiFeature.BattleFlee));

        Assert.False(s.State.TutorialDone);
        s.Execute(new GameAction(ActionType.EndTutorial));
        Assert.True(s.State.TutorialDone);
    }

    [Fact]
    public void Tutorial_CanHaveNobody_AndNormalGameIsNotLocked()
    {
        var db = Db(t => t.HeroId = null);
        var s = GameSession.NewTutorial(db, new Random(1));
        Assert.Empty(s.State.Party);

        var game = GameSession.NewGame(db, "aldric", new Random(1));
        Assert.False(game.State.IsTutorial);
        Assert.Empty(game.State.LockedFeatures);
        game.Execute(new GameAction(ActionType.EndTutorial));
        Assert.False(game.State.TutorialDone);
    }

    [Fact]
    public void Validation_RejectsUnknownInterfacePart()
    {
        var db = Db();
        Assert.Empty(db.Validate());
        var bad = ContentSerializer.Clone(db.Content);
        bad.Tutorial.Start.Actions.Add(new(ActionType.UnlockFeature, "rien"));
        Assert.Contains(new GameDatabase(bad).Validate(), e => e.Contains("interface"));
    }
}
