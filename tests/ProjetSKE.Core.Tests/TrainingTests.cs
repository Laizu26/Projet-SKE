using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Combats d'entraînement : sans risque, XP réduite, PV et PM rendus.</summary>
public class TrainingTests
{
    private static GameSession Game()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var start = content.Locations.First(l => l.Id == content.Start.LocationId);
        start.Training = true;
        start.TrainingOpponentIds = [content.Monsters[0].Id];
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        return GameSession.NewGame(db, "aldric", new Random(1));
    }

    [Fact]
    public void Victory_GivesReducedXp_NoGold_AndRestoresVitals()
    {
        var s = Game();
        var hero = s.State.Party[0];
        var monster = Assert.Single(s.TrainingOpponents);
        var gold = s.State.Gold;
        var hp = hero.CurrentHp;
        var battle = s.StartTraining([monster.Id]);
        Assert.True(battle.IsTraining);
        Assert.True(battle.CanFlee);
        hero.CurrentHp = 1; // blessé pendant l'entraînement
        var rewards = s.ApplyVictory(battle);
        Assert.Equal(monster.Xp * s.Balance.TrainingXpPercent / 100, rewards.Xp);
        Assert.Equal(0, rewards.Gold);
        Assert.Empty(rewards.ItemIds);
        Assert.Equal(gold, s.State.Gold);
        Assert.Equal(hp, hero.CurrentHp);
    }

    [Fact]
    public void Defeat_HasNoConsequence_EvenWithGameOverRule()
    {
        var s = Game();
        s.Config.Defeat = DefeatRule.GameOver;
        var here = s.State.CurrentLocationId;
        var gold = s.State.Gold;
        var hero = s.State.Party[0];
        var hp = hero.CurrentHp;
        var battle = s.StartTraining([s.TrainingOpponents[0].Id]);
        hero.CurrentHp = 0;
        var result = s.ApplyDefeat(battle);
        Assert.False(result.IsGameOver);
        Assert.Equal(here, s.State.CurrentLocationId);
        Assert.Equal(gold, s.State.Gold);
        Assert.Equal(hp, hero.CurrentHp);
    }

    [Fact]
    public void NoTrainingGround_NoOpponents()
    {
        var s = GameSession.NewGame(GameDatabase.Default, "aldric", new Random(1));
        Assert.Empty(s.TrainingOpponents);
    }
}
