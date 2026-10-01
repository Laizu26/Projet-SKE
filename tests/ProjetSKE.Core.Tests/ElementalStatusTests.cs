using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Embrasé (feu), Paralysé (foudre), Suffoqué (air) : effets élémentaires soumis aux résistances.</summary>
public class ElementalStatusTests
{
    private static (GameSession S, Battle B) Game(List<ElementModifier>? heroResistances = null)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        if (heroResistances is not null) content.Characters.First(c => c.Id == "aldric").Resistances = heroResistances;
        content.Passives.RemoveAll(p => true);
        foreach (var c in content.Characters) c.Passives.Clear();
        var db = new GameDatabase(content);
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        return (s, s.StartBattle([content.Monsters[0].Id]));
    }

    private static ActiveEffect Effect(EffectType type, int amount = 10) =>
        new() { Def = new SkillEffect { Type = type, Amount = amount, Turns = 3 }, Source = "test", TurnsLeft = 3 };

    [Fact]
    public void Paralysis_BlocksPhysicalSkills_Only()
    {
        var (_, b) = Game();
        var hero = b.Allies[0];
        var physical = hero.Skills.FirstOrDefault(sk => sk.Kind == SkillKind.Physical && Battle.CanAfford(hero, sk));
        Assert.NotNull(physical);
        hero.Effects.Add(Effect(EffectType.Paralysis));
        Assert.False(Battle.CanAfford(hero, physical!));
        Assert.All(hero.Skills.Where(sk => sk.Kind != SkillKind.Physical && hero.Mana >= sk.ManaCost && sk.HpCost == 0),
            sk => Assert.True(Battle.CanAfford(hero, sk)));
        Assert.Contains("Paralysé", hero.StatusText);
    }

    [Fact]
    public void Burn_DamageFollowsFireResistance()
    {
        int BurnDamage(List<ElementModifier>? res)
        {
            var (_, b) = Game(res);
            var hero = b.Allies[0];
            hero.Effects.Add(Effect(EffectType.Burn, 20));
            var log = b.Log.Count;
            var before = hero.Hp;
            // Fait passer les tours jusqu'au prochain tour du héros (dégâts de brûlure au début de son tour).
            b.Defend();
            var line = b.Log.Skip(log).FirstOrDefault(l => l.Contains("brûle") && l.StartsWith(hero.Name));
            Assert.NotNull(line);
            return int.Parse(line!.Split('-')[^1].Split(' ')[0]);
        }
        Assert.Equal(20, BurnDamage(null));
        Assert.Equal(10, BurnDamage([new ElementModifier { Element = "Feu", Percent = 50 }]));
        Assert.Equal(40, BurnDamage([new ElementModifier { Element = "feu", Percent = 200 }]));
    }

    [Fact]
    public void ImmuneToAir_CannotSuffocate()
    {
        var (s, b) = Game([new ElementModifier { Element = "air", Percent = 0 }]);
        var content = s.Db.Content;
        var skill = new SkillDef { Id = "asphyxie", Name = "Asphyxie", Kind = SkillKind.Status, Effects = [new SkillEffect { Type = EffectType.Suffocation, Amount = 10, Turns = 3, OnSelf = true }] };
        var hero = b.Allies[0];
        hero.Effects.Clear();
        // Applique directement via un effet de compétence sur soi.
        var method = typeof(Battle).GetMethod("ApplyEffect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var text = (string)method.Invoke(b, [hero, skill.Effects[0], "test"])!;
        Assert.Contains("immunisé", text);
        Assert.DoesNotContain(hero.Effects, e => e.Def.Type == EffectType.Suffocation);
    }
}
