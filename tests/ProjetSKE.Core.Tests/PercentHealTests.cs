using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Soins en % : potion = fixe + % des PV max ; passif = PV par tour en % des PV max.</summary>
public class PercentHealTests
{
    [Fact]
    public void Potion_HealsFlatPlusPercentOfMaxHp()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Items.Add(new ItemDef { Id = "elixir", Name = "Élixir", Type = ItemType.Consumable, HealHp = 10, HealHpPercent = 25 });
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        var hero = s.State.Party[0];
        var max = s.GetStats(hero).MaxHp;
        hero.CurrentHp = 1;
        s.AddItem("elixir");
        Assert.True(s.UseItem("elixir", hero));
        Assert.Equal(Math.Min(max, 1 + 10 + max * 25 / 100), hero.CurrentHp);
    }

    [Fact]
    public void Passive_RegeneratesAPercentOfMaxHpEachTurn()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Passives.Add(new PassiveDef { Id = "regen", Name = "Régénération", HpPerTurnPercent = 10 });
        content.Characters.First(c => c.Id == "aldric").Passives.Add(new PassiveUnlock(1, "regen"));
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        var b = s.StartBattle([content.Monsters[0].Id]);
        var hero = b.Allies[0];
        hero.Hp = 1;
        b.Defend(); // jusqu'au prochain tour du héros
        Assert.Contains(b.Log, l => l.StartsWith("Régénération :") && l.Contains($"+{hero.Stats.MaxHp * 10 / 100} PV"));
    }
}
