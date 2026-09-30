using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Passifs vérifiés à chaque coup : effets en cours (poison...) et comparaison avec l'adversaire.</summary>
public class CombatConditionTests
{
    private static (GameSession S, Battle B) Game(PassiveDef passive, int monsterLevel = 1)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Passives.Add(passive);
        content.Characters.First(c => c.Id == "aldric").Passives.Add(new PassiveUnlock(1, passive.Id));
        content.Monsters[0].Level = monsterLevel;
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        return (s, s.StartBattle([content.Monsters[0].Id]));
    }

    [Fact]
    public void PoisonedBearer_DealsMoreDamage()
    {
        var (s, b) = Game(new PassiveDef
        {
            Id = "venin", Name = "Rage du venin", DamagePercent = 50,
            Conditions = [new(ConditionType.HasEffect, "@soi") { Arg2 = "poison" }],
        });
        var hero = b.Allies[0];
        var foe = b.Enemies[0];
        Assert.Equal(1.0, s.PassiveDamageFactor(b, hero, foe));
        hero.Effects.Add(new ActiveEffect { Def = new SkillEffect { Type = EffectType.Poison, Amount = 3, Turns = 2 }, Source = "test", TurnsLeft = 2 });
        Assert.Equal(1.5, s.PassiveDamageFactor(b, hero, foe));
        Assert.Equal(1.0, s.PassiveDamageFactor(b, foe, hero)); // le monstre n'a pas ce passif
    }

    [Fact]
    public void StrongerOpponent_ByFiveLevels_BoostsDamage()
    {
        var passive = new PassiveDef
        {
            Id = "outsider", Name = "Outsider", DamagePercent = 30,
            Conditions = [new(ConditionType.CompareStats, "@cible", 5) { Arg2 = "niveau", Op = CompareOp.AtLeast, Other = "@soi", OtherStat = "niveau" }],
        };
        var (s, b) = Game(passive, monsterLevel: 3);
        Assert.Equal(1.0, s.PassiveDamageFactor(b, b.Allies[0], b.Enemies[0])); // 3 < 1 + 5
        var (s2, b2) = Game(passive, monsterLevel: 6);
        Assert.Equal(1.3, s2.PassiveDamageFactor(b2, b2.Allies[0], b2.Enemies[0]), 3); // 6 ≥ 1 + 5
    }

    [Fact]
    public void DamageTaken_AndLowHp_InBattle()
    {
        var (s, b) = Game(new PassiveDef
        {
            Id = "dos_au_mur", Name = "Dos au mur", DamageTakenPercent = -50,
            Conditions = [new(ConditionType.Stat, "@soi", 30) { Arg2 = "pv%", Op = CompareOp.Less }],
        });
        var hero = b.Allies[0];
        Assert.Equal(1.0, s.PassiveDamageFactor(b, b.Enemies[0], hero));
        hero.Hp = 1; // PV du combat, pas ceux de la fiche
        Assert.Equal(0.5, s.PassiveDamageFactor(b, b.Enemies[0], hero));
    }

    [Fact]
    public void PassiveLookingAtItsOwnStats_DoesNotLoop()
    {
        var (s, _) = Game(new PassiveDef
        {
            Id = "force_tranquille", Name = "Force tranquille", Bonus = new StatBlock(Attack: 5),
            Conditions = [new(ConditionType.Stat, "@soi", 1) { Arg2 = "atq" }],
        });
        var hero = s.State.Party[0];
        Assert.Equal(s.DefOf(hero).BaseStats.Attack + 5 + EquipAttack(s, hero), s.GetStats(hero).Attack);
    }

    private static int EquipAttack(GameSession s, Core.State.CharacterState c) =>
        Enum.GetValues<EquipSlot>().Select(c.GetEquipped).Where(id => id is not null && s.Db.Items.ContainsKey(id)).Sum(id => s.Db.Items[id!].Bonus.Attack);

    [Fact]
    public void OutsideBattle_CombatConditionsAreFalse()
    {
        var s = GameSession.NewGame(GameDatabase.Default, "aldric", new Random(1));
        Assert.False(s.Check(new Condition(ConditionType.HasEffect, "@soi") { Arg2 = "poison" }));
        Assert.False(s.Check(new Condition(ConditionType.CompareStats, "@cible", 0) { Arg2 = "niveau", Other = "@soi" }));
        // Deux PJ hors combat : comparaison possible.
        Assert.True(s.Check(new Condition(ConditionType.CompareStats, "@heros", 0) { Arg2 = "niveau", Op = CompareOp.Equal, Other = "@parle" }));
    }

    [Fact]
    public void Script_RoundTrips()
    {
        var nodes = DialogueScript.Parse("- Test.\n> A {effet poison @soi} {compare niveau @cible >= niveau @soi +5} -> fin", out var errors);
        Assert.Empty(errors);
        var conds = nodes[0].Choices[0].Conditions;
        Assert.Equal(ConditionType.HasEffect, conds[0].Type);
        Assert.Equal("poison", conds[0].Arg2);
        var cmp = conds[1];
        Assert.Equal(("niveau", "@cible", CompareOp.AtLeast, "niveau", "@soi", 5), (cmp.Arg2, cmp.Arg, cmp.Op, cmp.OtherStat, cmp.Other, cmp.Amount));
        var text = DialogueScript.Write(nodes);
        Assert.Contains("{effet poison @soi}", text);
        Assert.Contains("{compare niveau @cible >= niveau @soi +5}", text);
    }
}
