using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

public class GameTests
{
    private static GameSession NewGame(int seed = 42) =>
        GameSession.NewGame(GameDatabase.Default, "aldric", new Random(seed));

    /// <summary>Joue le combat en utilisant toujours la première compétence sur la première cible.</summary>
    private static void AutoPlay(Battle battle)
    {
        for (var i = 0; i < 500 && battle.Outcome == BattleOutcome.Ongoing; i++)
        {
            var skill = battle.CurrentActor!.Skills[0];
            battle.UseSkill(skill, battle.TargetsFor(skill).FirstOrDefault());
        }
    }

    [Fact]
    public void SampleContent_IsValid()
    {
        Assert.Empty(GameDatabase.Default.Validate());
    }

    [Fact]
    public void NewGame_StartsWithHeroInCapital()
    {
        var s = NewGame();
        Assert.Single(s.State.Party);
        Assert.Equal("aldric", s.State.Party[0].DefId);
        Assert.True(s.InCity);
        Assert.Equal(100, s.State.Gold);
        Assert.Equal(3, s.CountItem("potion"));
        Assert.Equal("epee_courte", s.State.Party[0].WeaponId);
        Assert.Contains("aldric", s.State.SeenCharacters);
        Assert.Contains("havrefort", s.State.SeenLocations);
    }

    [Fact]
    public void Stats_IncludeEquipmentAndLevel()
    {
        var s = NewGame();
        var hero = s.State.Party[0];
        Assert.Equal(14 + 4, s.GetStats(hero).Attack);
        s.GiveXp(hero, GameSession.XpToNextLevel(1));
        Assert.Equal(2, hero.Level);
        Assert.Equal(14 + 2 + 4, s.GetStats(hero).Attack);
        Assert.Equal(s.GetStats(hero).MaxHp, hero.CurrentHp);
    }

    [Fact]
    public void Travel_OnlyToConnectedLocations()
    {
        var s = NewGame();
        Assert.False(s.Travel("crypte").Success);
        s.Config.TravelEncounters = TravelEncounterMode.None;
        var result = s.Travel("route_roi");
        Assert.True(result.Success);
        Assert.Null(result.BattleMonsterIds);
        Assert.Equal("route_roi", s.State.CurrentLocationId);
        Assert.Equal("havrefort", s.State.LastCityId);
    }

    [Fact]
    public void Travel_FirstVisitTriggersDialogue()
    {
        var s = NewGame();
        s.Config.TravelEncounters = TravelEncounterMode.None;
        s.Travel("route_roi");
        var result = s.Travel("bourg_brume");
        Assert.Equal("rencontre_lyra", result.DialogueId);
        Assert.Equal("bourg_brume", s.State.LastCityId);
    }

    [Fact]
    public void Travel_FixedBattleOnlyWhenModeAllows()
    {
        var s = NewGame();
        s.State.CurrentLocationId = "bourg_brume";
        s.Config.TravelEncounters = TravelEncounterMode.RandomOnly;
        var none = s.Travel("col_corbeaux");
        Assert.Null(none.FixedBattleId);

        s.State.CurrentLocationId = "bourg_brume";
        s.Config.TravelEncounters = TravelEncounterMode.FixedOnly;
        var fixedResult = s.Travel("col_corbeaux");
        Assert.Equal("garrick", fixedResult.FixedBattleId);
        Assert.Equal("intro_garrick", fixedResult.DialogueId);
        Assert.NotNull(s.PendingFixedBattle);
    }

    [Fact]
    public void Shop_BuyAndSell()
    {
        var s = NewGame();
        Assert.True(s.Buy("potion"));
        Assert.Equal(80, s.State.Gold);
        Assert.Equal(4, s.CountItem("potion"));
        Assert.True(s.Sell("potion"));
        Assert.Equal(90, s.State.Gold);
        Assert.False(s.Buy("epee_longue")); // trop cher
        s.BrowseShop();
        Assert.Contains("epee_longue", s.State.SeenWeapons);
    }

    [Fact]
    public void Shop_ClosedOutsideCity()
    {
        var s = NewGame();
        s.Config.TravelEncounters = TravelEncounterMode.None;
        s.Travel("route_roi");
        Assert.Empty(s.ShopStock);
        Assert.False(s.Buy("potion"));
        Assert.False(s.Rest());
    }

    [Fact]
    public void UniqueRelic_CanOnlyBeOwnedOnce()
    {
        var s = NewGame();
        Assert.True(s.AddItem("amulette_valdor"));
        Assert.False(s.AddItem("amulette_valdor"));
        Assert.True(s.Equip(s.State.Party[0], "amulette_valdor"));
        Assert.False(s.AddItem("amulette_valdor"));
        Assert.Contains("amulette_valdor", s.State.SeenRelics);
    }

    [Fact]
    public void QuestRelic_IsNotEquipableNorSellable()
    {
        var item = GameDatabase.Default.Items["fragment_couronne"];
        Assert.False(item.IsEquipable);
        Assert.False(item.IsSellable);
    }

    [Fact]
    public void Equip_SwapsWithBag()
    {
        var s = NewGame();
        var hero = s.State.Party[0];
        s.AddItem("epee_longue");
        Assert.True(s.Equip(hero, "epee_longue"));
        Assert.Equal("epee_longue", hero.WeaponId);
        Assert.Equal(1, s.CountItem("epee_courte"));
        Assert.Equal(0, s.CountItem("epee_longue"));
        Assert.True(s.Unequip(hero, EquipSlot.Weapon));
        Assert.Null(hero.WeaponId);
        Assert.Equal(1, s.CountItem("epee_longue"));
    }

    [Fact]
    public void Recruit_GoesToReserveWhenFull()
    {
        var s = NewGame();
        s.Config.MaxActiveParty = 2;
        Assert.True(s.Recruit("lyra"));
        Assert.True(s.Recruit("tobin"));
        Assert.False(s.Recruit("tobin"));
        Assert.True(s.State.Party[1].IsActive);
        Assert.False(s.State.Party[2].IsActive);
        Assert.Equal(2, s.ActiveParty.Count());
    }

    [Fact]
    public void Battle_OrderFollowsSpeedAndCanBeWon()
    {
        var s = NewGame();
        var battle = s.StartBattle(["gobelin"]);
        Assert.True(battle.IsPlayerTurn); // Aldric (VIT 8) vs gobelin (VIT 9) : le gobelin joue d'abord
        Assert.Contains(battle.Log, l => l.StartsWith("Gobelin"));
        AutoPlay(battle);
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        var goldBefore = s.State.Gold;
        var rewards = s.ApplyVictory(battle);
        Assert.Equal(7, rewards.Xp);
        Assert.Equal(goldBefore + 8, s.State.Gold);
        Assert.Contains("gobelin", s.State.SeenMonsters);
    }

    [Fact]
    public void Battle_DuplicateMonstersGetLetters()
    {
        var battle = NewGame().StartBattle(["loup", "loup"]);
        Assert.Equal("Loup gris A", battle.Enemies[0].Name);
        Assert.Equal("Loup gris B", battle.Enemies[1].Name);
    }

    [Fact]
    public void Battle_ManaIsRequired()
    {
        var s = NewGame();
        s.State.Party[0].CurrentMana = 0;
        var battle = s.StartBattle(["gobelin"]);
        var powerful = battle.CurrentActor!.Skills.First(k => k.Id == "coup_puissant");
        Assert.False(battle.CanUse(powerful));
        Assert.False(battle.UseSkill(powerful, battle.Enemies[0]));
    }

    [Fact]
    public void Battle_FleeRules()
    {
        var s = NewGame();
        s.Config.Flee = FleeRule.Never;
        Assert.False(s.StartBattle(["gobelin"]).TryFlee());

        s.Config.Flee = FleeRule.AlwaysSucceed;
        var battle = s.StartBattle(["gobelin"]);
        Assert.True(battle.TryFlee());
        Assert.Equal(BattleOutcome.Fled, battle.Outcome);

        Assert.False(s.StartBattle(["morvath"]).CanFlee); // jamais contre un boss
    }

    [Fact]
    public void Battle_ItemHealsAndIsConsumed()
    {
        var s = NewGame();
        s.State.Party[0].CurrentHp = 10;
        var battle = s.StartBattle(["gobelin"]);
        var hero = battle.Allies[0];
        var hpBefore = hero.Hp;
        Assert.True(battle.UseItem(GameDatabase.Default.Items["potion"], hero));
        Assert.Equal(2, s.CountItem("potion"));
        Assert.True(hero.Hp > hpBefore);
    }

    [Fact]
    public void Defeat_ReturnToCityOrGameOver()
    {
        var s = NewGame();
        s.Config.TravelEncounters = TravelEncounterMode.None;
        s.Travel("route_roi");
        var result = s.ApplyDefeat();
        Assert.False(result.IsGameOver);
        Assert.Equal(20, result.GoldLost);
        Assert.Equal("havrefort", s.State.CurrentLocationId);

        s.Config.Defeat = DefeatRule.GameOver;
        Assert.True(s.ApplyDefeat().IsGameOver);
    }

    [Fact]
    public void Dialogue_ChoiceRecruitsCharacter()
    {
        var s = NewGame();
        var d = s.StartDialogue("rencontre_lyra");
        Assert.Empty(d.Choices);
        d.Continue();
        Assert.Equal(2, d.Choices.Count);
        d.Choose(0);
        Assert.True(s.IsInParty("lyra"));
        Assert.Single(d.Notifications);
        d.Continue();
        Assert.True(d.IsFinished);
    }

    [Fact]
    public void Dialogue_GivesGoldAndFlag()
    {
        var s = NewGame();
        var d = s.StartDialogue("capitaine");
        d.Choose(0);
        Assert.Equal(150, s.State.Gold);
        Assert.True(s.HasFlag("quete_crypte"));
    }

    [Fact]
    public void Save_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ske-tests-" + Guid.NewGuid());
        try
        {
            var saves = new SaveService(dir);
            var s = NewGame();
            s.Recruit("lyra");
            s.State.Journal = "Penser à acheter des potions.";
            s.Config.Defeat = DefeatRule.GameOver;
            saves.Save(1, s.State);

            var loaded = saves.Load(1)!;
            Assert.Equal(2, loaded.Party.Count);
            Assert.Equal("Penser à acheter des potions.", loaded.Journal);
            Assert.Equal(DefeatRule.GameOver, loaded.Config.Defeat);
            Assert.Contains("recruited:lyra", loaded.Flags);
            Assert.Equal(3, loaded.Inventory["potion"]);
            Assert.Null(saves.Load(2));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
