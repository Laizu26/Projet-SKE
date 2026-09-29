using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Donjon : succession d'étapes (combats, dialogues, effets) entrée par une porte dans un lieu.</summary>
public class DungeonTests
{
    private static GameSession Game(Action<DungeonDef>? tweak = null)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var dungeon = new DungeonDef
        {
            Id = "crypte", Name = "Crypte",
            Steps =
            [
                new() { Name = "Entrée", Type = DungeonStepType.Dialogue, DialogueId = content.Dialogues[0].Id },
                new() { Name = "Gardien", Type = DungeonStepType.Battle, MonsterIds = [content.Monsters[0].Id], Actions = [new(ActionType.SetFlag, "gardien")] },
                new() { Name = "Salle secrète", Type = DungeonStepType.Effects, Conditions = [new(ConditionType.FlagSet, "cle")], Actions = [new(ActionType.GiveGold, "", 100)] },
                new() { Name = "Trésor", Type = DungeonStepType.Effects, Actions = [new(ActionType.GiveGold, "", 10)] },
            ],
            CompleteActions = [new(ActionType.SetFlag, "crypte_finie")],
        };
        tweak?.Invoke(dungeon);
        content.Dungeons.Add(dungeon);
        // Porte dans un sous-lieu comme dans un lieu : ici, le lieu de départ.
        content.Locations.First(l => l.Id == content.Start.LocationId).DungeonIds.Add("crypte");
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        return GameSession.NewGame(db, "aldric", new Random(1));
    }

    [Fact]
    public void Steps_ArePlayedInOrder_SkippingThoseWhoseConditionsFail()
    {
        var s = Game();
        var door = Assert.Single(s.DungeonsHere);
        Assert.True(s.CanEnterDungeon(door));
        Assert.True(s.EnterDungeon("crypte"));
        Assert.Equal("Entrée", s.DungeonStep!.Name);
        Assert.False(s.CompleteDungeonStep());
        Assert.Equal("Gardien", s.DungeonStep!.Name);
        var gold = s.State.Gold;
        Assert.False(s.CompleteDungeonStep());
        Assert.True(s.HasFlag("gardien"));
        Assert.Equal("Trésor", s.DungeonStep!.Name); // salle secrète sautée (pas de clé)
        Assert.True(s.CompleteDungeonStep());
        Assert.Equal(gold + 10, s.State.Gold);
        Assert.Null(s.State.Dungeon);
        Assert.True(s.HasFlag("crypte_finie"));
        Assert.True(s.Check(new Condition(ConditionType.DungeonDone, "crypte")));
        Assert.False(s.CanEnterDungeon(door)); // pas refaisable
    }

    [Fact]
    public void Leaving_OrDefeat_LosesTheProgress()
    {
        var s = Game(d => d.Repeatable = true);
        s.EnterDungeon("crypte");
        s.CompleteDungeonStep();
        s.LeaveDungeon();
        Assert.Null(s.State.Dungeon);
        s.EnterDungeon("crypte");
        Assert.Equal("Entrée", s.DungeonStep!.Name);
        s.ApplyDefeat();
        Assert.Null(s.State.Dungeon);
    }

    [Fact]
    public void LockedDoor_NeedsItsConditions()
    {
        var s = Game(d => d.Conditions = [new(ConditionType.FlagSet, "sceau_brise")]);
        Assert.False(s.EnterDungeon("crypte"));
        s.Execute(new GameAction(ActionType.SetFlag, "sceau_brise"));
        Assert.True(s.EnterDungeon("crypte"));
    }
}
