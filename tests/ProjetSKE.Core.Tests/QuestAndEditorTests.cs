using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

public class QuestAndEditorTests
{
    private static GameSession NewGame(GameDatabase? db = null) =>
        GameSession.NewGame(db ?? GameDatabase.Default, "aldric", new Random(1));

    private static void Win(GameSession s, params string[] monsters)
    {
        var battle = s.StartBattle(monsters);
        for (var i = 0; i < 500 && battle.Outcome == BattleOutcome.Ongoing; i++)
        {
            var skill = battle.CurrentActor!.Skills[0];
            battle.UseSkill(skill, battle.TargetsFor(skill).FirstOrDefault());
        }
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        s.ApplyVictory(battle);
    }

    [Fact]
    public void Npc_DialogueDependsOnQuestProgress()
    {
        var s = NewGame();
        Assert.Contains(s.VisibleNpcs, n => n.Id == "fermier_joss");
        Assert.Equal("joss_quete", s.Talk("fermier_joss"));
        Assert.Contains("fermier_joss", s.State.SeenNpcs);

        s.StartDialogue("joss_quete").Choose(0);
        Assert.Equal(QuestStatus.Active, s.GetQuestStatus("chasse_loups"));
        Assert.Equal("joss_attente", s.Talk("fermier_joss"));
    }

    [Fact]
    public void Quest_DefeatThenTalk_GivesRewards()
    {
        var s = NewGame();
        s.StartQuest("chasse_loups");
        Win(s, "loup");
        Win(s, "loup", "loup");
        Assert.Equal(1, s.State.Quests["chasse_loups"].Step);

        var gold = s.State.Gold;
        var potions = s.CountItem("potion");
        Assert.Equal("joss_merci", s.Talk("fermier_joss"));
        Assert.Equal(QuestStatus.Completed, s.GetQuestStatus("chasse_loups"));
        Assert.Equal(gold + 60, s.State.Gold);
        Assert.Equal(potions + 2, s.CountItem("potion"));
        Assert.Contains(s.Notifications, n => n.Contains("Quête terminée"));
    }

    [Fact]
    public void Quest_BringItemToNpc()
    {
        var s = NewGame();
        s.StartQuest("remede");
        Assert.Equal(1, s.CountItem("ether"));
        s.UpdateQuests(); // posséder l'objet ne suffit pas : il faut le remettre
        Assert.Equal(QuestStatus.Active, s.GetQuestStatus("remede"));
        s.Talk("herboriste");
        Assert.Equal(QuestStatus.Completed, s.GetQuestStatus("remede"));
        Assert.Equal(0, s.CountItem("ether"));
        Assert.Equal(1, s.CountItem("grande_potion"));
    }

    [Fact]
    public void Quest_ReachLocation()
    {
        var s = NewGame();
        s.Config.TravelEncounters = TravelEncounterMode.None;
        s.StartQuest("quete_crypte");
        s.SetFlag("crypte_ouverte");
        s.Travel("route_roi");
        s.Travel("foret_sombrebois");
        Assert.Equal(0, s.State.Quests["quete_crypte"].Step);
        s.Travel("crypte");
        Assert.Equal(1, s.State.Quests["quete_crypte"].Step);
    }

    [Fact]
    public void Location_LockedByCondition()
    {
        var s = NewGame();
        s.Config.TravelEncounters = TravelEncounterMode.None;
        s.Travel("route_roi");
        s.Travel("foret_sombrebois");
        var blocked = s.Travel("crypte");
        Assert.False(blocked.Success);
        Assert.Contains("grille", blocked.Error);
        s.SetFlag("crypte_ouverte");
        Assert.True(s.Travel("crypte").Success);
    }

    [Fact]
    public void DialogueChoice_HiddenWhenConditionFails()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Dialogues.Add(new DialogueDef
        {
            Id = "test",
            Nodes = DialogueScript.Parse("""
                Garde: Halte !
                > Payer 500 or -> ok {or 500} [payer 500]
                > Partir
                @ok
                Garde: Passez.
                """, out var errors),
        });
        Assert.Empty(errors);
        var s = NewGame(new GameDatabase(content));
        var d = s.StartDialogue("test");
        Assert.Single(d.Choices);
        s.State.Gold = 600;
        Assert.Equal(2, d.Choices.Count);
        d.Choose(0);
        Assert.Equal(100, s.State.Gold);
        Assert.Equal("Passez.", d.Current!.Text);
    }

    [Fact]
    public void Script_ParsesChainsLabelsActionsAndChoices()
    {
        var nodes = DialogueScript.Parse("""
            // commentaire
            - Une porte grince.
            Lyra: Tu entends ? [flag porte]
            > Entrer -> dedans {sans_flag peur} [xp 10]
            > Fuir
            @dedans
            Lyra: Il fait noir...
            -> fin
            """, out var errors);

        Assert.Empty(errors);
        Assert.Equal(3, nodes.Count);
        Assert.Equal("", nodes[0].Speaker);
        Assert.Equal(nodes[1].Id, nodes[0].NextId);
        Assert.Equal("Lyra", nodes[1].Speaker);
        Assert.Equal(ActionType.SetFlag, nodes[1].Actions.Single().Type);
        Assert.Equal(2, nodes[1].Choices.Count);
        Assert.Equal("dedans", nodes[1].Choices[0].NextId);
        Assert.Equal("Entrer", nodes[1].Choices[0].Text);
        Assert.Equal(ConditionType.FlagNotSet, nodes[1].Choices[0].Conditions.Single().Type);
        Assert.Equal(10, nodes[1].Choices[0].Actions.Single().Amount);
        Assert.Null(nodes[1].Choices[1].NextId);
        Assert.Null(nodes[2].NextId);
    }

    [Fact]
    public void Script_ReportsErrors()
    {
        DialogueScript.Parse("""
            Lyra: Salut [danse]
            > Oui -> nulle_part
            """, out var errors);
        Assert.Contains(errors, e => e.Contains("danse"));
        Assert.Contains(errors, e => e.Contains("nulle_part"));
    }

    [Fact]
    public void Script_RoundTripsAllSampleDialogues()
    {
        foreach (var dialogue in GameDatabase.Default.Content.Dialogues)
        {
            var text = DialogueScript.Write(dialogue.Nodes);
            var parsed = DialogueScript.Parse(text, out var errors);
            Assert.True(errors.Count == 0, $"{dialogue.Id} : {string.Join(" / ", errors)}\n{text}");
            Assert.Equal(DialogueScript.Write(parsed), text);
            Assert.Equal(dialogue.Nodes.Count, parsed.Count);
        }
    }

    [Fact]
    public void Content_JsonRoundTrip()
    {
        var content = GameDatabase.Default.Content;
        var json = ContentSerializer.ToJson(content);
        var copy = ContentSerializer.FromJson(json);
        Assert.Equal(json, ContentSerializer.ToJson(copy));
        Assert.Equal(content.Npcs.Count, copy.Npcs.Count);
        Assert.Contains("\"Recruit\"", json); // les énumérations sont écrites en texte lisible
        Assert.Empty(new GameDatabase(copy).Validate());
    }

    [Fact]
    public void Validate_FindsBrokenReferences()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Monsters.RemoveAll(m => m.Id == "loup");
        content.Items.Add(new ItemDef { Id = "potion", Name = "Doublon" });
        var errors = new GameDatabase(content).Validate();
        Assert.Contains(errors, e => e.Contains("loup"));
        Assert.Contains(errors, e => e.Contains("double"));
    }

    [Fact]
    public void Save_FromOlderContent_IsSanitized()
    {
        var state = NewGame().State;
        state.Inventory["objet_supprime"] = 2;
        state.Party[0].WeaponId = "arme_supprimee";
        state.CurrentLocationId = "lieu_supprime";
        state.Quests["quete_supprimee"] = new QuestProgress();
        var s = new GameSession(GameDatabase.Default, state);
        Assert.False(state.Inventory.ContainsKey("objet_supprime"));
        Assert.Null(state.Party[0].WeaponId);
        Assert.Equal("havrefort", state.CurrentLocationId);
        Assert.Empty(state.Quests);
        Assert.NotNull(s.CurrentLocation);
    }

    [Fact]
    public void Balance_ChangesFormulas()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Balance.XpPerLevel = 5;
        content.Balance.SellPercent = 100;
        var s = NewGame(new GameDatabase(content));
        Assert.Equal(5, s.XpToNextLevel(1));
        Assert.Equal(20, s.SellPrice(s.Db.Items["potion"]));
    }

    [Fact]
    public void Actions_TeleportAndGiveXp()
    {
        var s = NewGame();
        s.Execute(new GameAction(ActionType.Teleport, "bourg_brume"));
        Assert.Equal("bourg_brume", s.State.CurrentLocationId);
        Assert.Equal("bourg_brume", s.State.LastCityId);
        s.Execute(new GameAction(ActionType.GiveXp, amount: 1000));
        Assert.True(s.State.Party[0].Level > 5);
        var battle = s.Execute(new GameAction(ActionType.StartBattle, "loup, gobelin"));
        Assert.Equal(new[] { "loup", "gobelin" }, battle!);
    }
}

public class WorldLayoutTests
{
    [Fact]
    public void Layout_PlacesEveryLocationWithoutOverlap_AndNeighborsAreClose()
    {
        var db = GameDatabase.Default;
        var layout = WorldLayout.Compute(db);
        // Seuls les lieux du royaume sont sur la carte (pas les sous-lieux).
        Assert.Equal(db.Content.Locations.Count(l => db.ParentOf(l) is null), layout.Count);
        Assert.Equal(layout.Count, layout.Values.Distinct().Count());
        Assert.Equal(new Hex(0, 0), layout[db.Start.LocationId]);
        foreach (var loc in db.Content.Locations)
            foreach (var next in loc.ConnectedIds)
                Assert.True(layout[loc.Id].DistanceTo(layout[next]) <= 2, $"{loc.Id} → {next} trop éloignés");
    }

    [Fact]
    public void Layout_KeepsFixedPositions()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Locations.First(l => l.Id == "crypte").HexQ = 5;
        content.Locations.First(l => l.Id == "crypte").HexR = -2;
        var layout = WorldLayout.Compute(new GameDatabase(content));
        Assert.Equal(new Hex(5, -2), layout["crypte"]);
        Assert.Equal(layout.Count, layout.Values.Distinct().Count());
    }

    [Fact]
    public void Hex_GridAndSpiral()
    {
        Assert.Equal(19, Hex.Grid(2).Count());
        Assert.Equal(19, Hex.Spiral(new Hex(0, 0), 2).Distinct().Count());
        Assert.Equal(2, new Hex(0, 0).DistanceTo(new Hex(1, 1)));
    }
}
