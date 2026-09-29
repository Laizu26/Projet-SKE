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

public class GaugeTests
{
    private static GameSession Game()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        s.Recruit("lyra");
        return s;
    }

    [Fact]
    public void Gauge_IsPerCharacter_WithStartValues()
    {
        var s = Game();
        Assert.Equal(0, s.GetGauge("aldric", "folie"));
        Assert.Equal(10, s.GetGauge("lyra", "folie")); // valeur de départ propre à Lyra
        Assert.Equal(5, s.GetGauge("@equipe", "folie"));
    }

    [Fact]
    public void Gauge_EffectsAndConditions()
    {
        var s = Game();
        s.Execute(new GameAction(ActionType.AddGauge, "folie", 30) { Arg2 = "lyra" });
        Assert.Equal(40, s.GetGauge("lyra", "folie"));
        Assert.Equal(0, s.GetGauge("aldric", "folie"));
        Assert.Contains(s.Notifications, n => n.Contains("Folie +30"));

        s.Execute(new GameAction(ActionType.AddGauge, "folie", 500) { Arg2 = "@equipe" });
        Assert.Equal(100, s.GetGauge("aldric", "folie")); // bornée au maximum
        s.Execute(new GameAction(ActionType.SetGauge, "folie", 20) { Arg2 = "aldric" });
        Assert.Equal(20, s.GetGauge("aldric", "folie"));

        Assert.True(s.Check(new Condition(ConditionType.Gauge, "lyra", 95) { Arg2 = "folie", Op = CompareOp.AtLeast }));
        Assert.False(s.Check(new Condition(ConditionType.Gauge, "aldric", 50) { Arg2 = "folie", Op = CompareOp.AtLeast }));
        Assert.Equal("Perdu", s.Db.Gauges["folie"].TierName(100));
    }

    [Fact]
    public void Gauge_ScriptWords_RoundTrip()
    {
        var nodes = DialogueScript.Parse("- Quelque chose se brise en toi. [jauge folie 10 @heros]", out var errors);
        Assert.Empty(errors);
        var action = nodes.SelectMany(n => n.Actions).Single();
        Assert.Equal(ActionType.AddGauge, action.Type);
        Assert.Equal("folie", action.Arg);
        Assert.Equal("@heros", action.Arg2);
        Assert.Equal(10, action.Amount);
    }
}

public class MergerCoversEverythingTests
{
    /// <summary>Chaque réglage du contenu, modifié seulement en local, doit survivre à la synchronisation.</summary>
    [Fact]
    public void LocalOnlyChanges_OfEveryContentProperty_SurviveMerge()
    {
        var baseline = ContentSerializer.Clone(GameDatabase.Default.Content);
        var local = ContentSerializer.Clone(baseline);
        local.Tutorial.Enabled = true;
        local.Tutorial.Name = "Songe";
        local.Gauges.Add(new CharacterGaugeDef { Id = "peur", Name = "Peur" });
        local.Title = "Autre titre";
        var merged = ProjetSKE.Core.Cloud.ContentMerger.Merge(baseline, local, ContentSerializer.Clone(baseline)).Merged;
        Assert.Equal(ContentSerializer.ToJson(local), ContentSerializer.ToJson(merged));
    }

    /// <summary>Garde-fou : toute nouvelle propriété du contenu doit être ajoutée à la synchronisation.</summary>
    [Fact]
    public void Merger_HandlesEveryContentProperty()
    {
        var source = File.ReadAllText(Path.Combine(FindRepo(), "src/ProjetSKE.Core/Cloud/ContentMerger.cs"));
        foreach (var p in typeof(GameContent).GetProperties().Where(p => p.CanWrite && p.Name != nameof(GameContent.Version)))
            Assert.True(source.Contains($"{p.Name} = Merge"), $"ContentMerger ne synchronise pas « {p.Name} »");
    }

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("racine du dépôt");
    }
}

public class PassiveTests
{
    private static GameSession Game()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        s.Recruit("lyra");
        return s;
    }

    private static ProjetSKE.Core.State.CharacterState Pc(GameSession s, string id) => s.State.Party.First(c => c.DefId == id);

    [Fact]
    public void Passive_AddsPercentStats_AndBattleStartShield()
    {
        var s = Game();
        var aldric = Pc(s, "aldric");
        Assert.Contains(s.ActivePassives(aldric), p => p.Id == "sang_froid");
        s.Execute(new GameAction(ActionType.RemovePassive, "sang_froid") { Arg2 = "aldric" });
        var without = s.GetStats(aldric).Defense;
        s.Execute(new GameAction(ActionType.GivePassive, "sang_froid") { Arg2 = "aldric" });
        Assert.Equal(without + without * 10 / 100, s.GetStats(aldric).Defense);

        var battle = s.StartBattle(["loup"]);
        var ally = battle.Allies.First(a => a.Character == aldric);
        Assert.Contains(ally.Effects, e => e.Source == "passif:sang_froid" && e.Def.Type == EffectType.Shield);
        Assert.Contains(battle.Log, l => l.StartsWith("Sang-froid"));
    }

    [Fact]
    public void ConditionalPassive_WorksOnlyWhenItsOwnerMeetsIt()
    {
        var s = Game();
        var lyra = Pc(s, "lyra");
        var transe = s.Db.Passives["transe"];
        Assert.Contains(transe, s.PassivesOf(lyra));
        Assert.False(s.IsPassiveActive(lyra, transe)); // Folie 10 < 50
        var magic = s.GetStats(lyra).Magic;

        s.Execute(new GameAction(ActionType.AddGauge, "folie", 45) { Arg2 = "lyra" });
        Assert.True(s.IsPassiveActive(lyra, transe));
        Assert.Equal(magic + magic * 25 / 100, s.GetStats(lyra).Magic);
        Assert.True(s.Check(new Condition(ConditionType.HasPassive, "lyra") { Arg2 = "transe" }));
        Assert.False(s.Check(new Condition(ConditionType.HasPassive, "aldric") { Arg2 = "transe" }));
    }

    [Fact]
    public void Passive_GiveAndRemove_WithNotifications()
    {
        var s = Game();
        var aldric = Pc(s, "aldric");
        s.Execute(new GameAction(ActionType.GivePassive, "transe") { Arg2 = "aldric" });
        Assert.Contains(s.PassivesOf(aldric), p => p.Id == "transe");
        Assert.Contains(s.Notifications, n => n.Contains("obtient le passif « Transe »"));
        s.Execute(new GameAction(ActionType.RemovePassive, "sang_froid") { Arg2 = "aldric" });
        Assert.DoesNotContain(s.PassivesOf(aldric), p => p.Id == "sang_froid");
    }

    [Fact]
    public void Passive_ScriptWords()
    {
        var nodes = DialogueScript.Parse("- Une force t'envahit. [passif transe @heros]", out var errors);
        Assert.Empty(errors);
        var a = nodes.SelectMany(n => n.Actions).Single();
        Assert.Equal((ActionType.GivePassive, "transe", "@heros"), (a.Type, a.Arg, a.Arg2));
    }
}

public class PowerTests
{
    [Fact]
    public void GivingAPower_TeachesAllItsSkills_ByLevel()
    {
        var db = new GameDatabase(ContentSerializer.Clone(GameDatabase.Default.Content));
        Assert.Empty(db.Validate());
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        var aldric = s.State.Party[0];
        Assert.DoesNotContain(s.GetSkills(aldric), k => k.Id == "soin");

        s.Execute(new GameAction(ActionType.GivePower, "sacre") { Arg2 = "@heros" });
        Assert.Contains(s.Notifications, n => n.Contains("maîtrise le pouvoir « Magie sacrée »"));
        var skills = s.GetSkills(aldric).Select(k => k.Id).ToList();
        Assert.Contains("soin", skills);                // niveau 1
        Assert.DoesNotContain("resurrection", skills);  // niveau 5 : pas encore
        Assert.True(s.Check(new Condition(ConditionType.HasPower, "@heros") { Arg2 = "sacre" }));

        aldric.Level = 5;
        Assert.Contains(s.GetSkills(aldric), k => k.Id == "resurrection");

        s.Execute(new GameAction(ActionType.RemovePower, "sacre") { Arg2 = "@heros" });
        Assert.DoesNotContain(s.GetSkills(aldric), k => k.Id == "soin");
    }

    [Fact]
    public void InterfaceFeatures_HaveShortFrenchWords()
    {
        Assert.True(UiFeatures.TryParse("carte", out var f) && f == UiFeature.TabMap);
        Assert.True(UiFeatures.TryParse("Sac", out f) && f == UiFeature.CampBag);
        Assert.True(UiFeatures.TryParse("BattleFlee", out f) && f == UiFeature.BattleFlee);
        Assert.False(UiFeatures.TryParse("rien", out _));

        var nodes = DialogueScript.Parse("- Le monde s'ouvre. [debloquer carte] [verrouiller sac] [pouvoir sacre @heros]", out var errors);
        Assert.Empty(errors);
        var s = GameSession.NewTutorial(GameDatabase.Default);
        foreach (var a in nodes.SelectMany(n => n.Actions)) s.Execute(a);
        Assert.False(s.IsLocked(UiFeature.TabMap));
        Assert.True(s.IsLocked(UiFeature.CampBag));
    }
}

public class HiddenLocationTests
{
    [Fact]
    public void HiddenAtStart_AppearsOnlyOnceRevealed()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Locations.First(l => l.Id == "taverne_sanglier").HiddenAtStart = true;
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        s.State.CurrentLocationId = "havrefort";
        s.State.Config.TravelEncounters = TravelEncounterMode.None;
        Assert.DoesNotContain(s.SubLocations, l => l.Id == "taverne_sanglier");
        Assert.False(s.Travel("taverne_sanglier").Success);

        s.Execute(new GameAction(ActionType.RevealLocation, "taverne_sanglier"));
        Assert.Contains(s.SubLocations, l => l.Id == "taverne_sanglier");
        Assert.True(s.Travel("taverne_sanglier").Success);

        s.Travel("havrefort");
        s.Execute(new GameAction(ActionType.HideLocation, "taverne_sanglier"));
        Assert.DoesNotContain(s.SubLocations, l => l.Id == "taverne_sanglier");
    }
}

public class HiddenNpcTests
{
    [Fact]
    public void HiddenNpc_AppearsOnlyWhenShown()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Npcs.First(n => n.Id == "capitaine_hardin").HiddenAtStart = true;
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        s.State.CurrentLocationId = "havrefort";
        Assert.DoesNotContain(s.VisibleNpcs, n => n.Id == "capitaine_hardin");

        var nodes = DialogueScript.Parse("- Une silhouette approche. [montre_pnj capitaine_hardin]", out var errors);
        Assert.Empty(errors);
        foreach (var a in nodes.SelectMany(n => n.Actions)) s.Execute(a);
        Assert.Contains(s.VisibleNpcs, n => n.Id == "capitaine_hardin");

        s.Execute(new GameAction(ActionType.HideNpc, "capitaine_hardin"));
        Assert.DoesNotContain(s.VisibleNpcs, n => n.Id == "capitaine_hardin");
    }
}
