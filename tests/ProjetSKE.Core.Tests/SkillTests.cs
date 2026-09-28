using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Compétences configurables : éléments, coups multiples, critiques, effets durables, recharge, résurrection.</summary>
public class SkillTests
{
    private static (GameSession Session, GameContent Content) Setup(Action<GameContent> change)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Balance.DamageVariancePercent = 0;
        change(content);
        return (GameSession.NewGame(new GameDatabase(content), "aldric", new Random(4)), content);
    }

    private static SkillDef Skill(GameContent c, SkillDef skill)
    {
        c.Skills.Add(skill);
        c.Characters.First(x => x.Id == "aldric").Skills.Insert(0, new SkillUnlock(1, skill.Id));
        return skill;
    }

    /// <summary>Combat contre un mannequin lent et solide (Aldric joue en premier), avec la compétence à tester.</summary>
    private static (GameSession, Battle, SkillDef) Ready(SkillDef def, Action<GameSession>? before = null)
    {
        var (session, _) = Setup(x =>
        {
            x.Monsters.Add(new MonsterDef
            {
                Id = "mannequin", Name = "Mannequin", Stats = new(MaxHp: 500, Attack: 1, Defense: 0, Speed: 1),
                SkillIds = ["morsure"], Resistances = [new("feu", 200), new("glace", 0), new("poison", -100)],
            });
            Skill(x, def);
        });
        before?.Invoke(session);
        var battle = session.StartBattle(["mannequin"]);
        Assert.True(battle.IsPlayerTurn);
        return (session, battle, battle.CurrentActor!.Skills.First(k => k.Id == def.Id));
    }

    private static int Damage(Battle b) => b.Enemies[0].Stats.MaxHp - b.Enemies[0].Hp;

    [Fact]
    public void Element_WeaknessDoublesAndImmunityBlocks()
    {
        var (_, normal, s1) = Ready(new SkillDef { Id = "neutre", Name = "Neutre", Power = 1 });
        normal.UseSkill(s1, normal.Enemies[0]);
        var (_, fire, s2) = Ready(new SkillDef { Id = "brasier", Name = "Brasier", Power = 1, Element = "Feu" });
        fire.UseSkill(s2, fire.Enemies[0]);
        Assert.Equal(Damage(normal) * 2, Damage(fire));

        var (_, ice, s3) = Ready(new SkillDef { Id = "givre", Name = "Givre", Power = 1, Element = "glace" });
        ice.UseSkill(s3, ice.Enemies[0]);
        Assert.Equal(0, Damage(ice));
        Assert.Contains(ice.Log, l => l.Contains("immunisé"));
    }

    [Fact]
    public void Element_AbsorptionHealsTheTarget()
    {
        var (_, b, s) = Ready(new SkillDef { Id = "venin", Name = "Venin", Power = 1, Element = "poison" });
        b.Enemies[0].Hp = 400;
        b.UseSkill(s, b.Enemies[0]);
        Assert.True(b.Enemies[0].Hp > 400);
    }

    [Fact]
    public void MultiHitAndFlatAmountAddUp()
    {
        var (_, one, s1) = Ready(new SkillDef { Id = "a", Name = "A", Power = 1 });
        one.UseSkill(s1, one.Enemies[0]);
        var (_, three, s3) = Ready(new SkillDef { Id = "b", Name = "B", Power = 1, Hits = 3, FlatAmount = 5 });
        three.UseSkill(s3, three.Enemies[0]);
        Assert.Equal((Damage(one) + 5) * 3, Damage(three));
    }

    [Fact]
    public void GuaranteedCritAndMisses()
    {
        var (_, normal, s1) = Ready(new SkillDef { Id = "a", Name = "A", Power = 1 });
        normal.UseSkill(s1, normal.Enemies[0]);
        var (_, crit, s2) = Ready(new SkillDef { Id = "c", Name = "C", Power = 1, CritChance = 100, CritMultiplier = 2 });
        crit.UseSkill(s2, crit.Enemies[0]);
        Assert.Equal(Damage(normal) * 2, Damage(crit));

        var (_, miss, s3) = Ready(new SkillDef { Id = "m", Name = "M", Power = 1, Accuracy = 0 });
        miss.UseSkill(s3, miss.Enemies[0]);
        Assert.Equal(0, Damage(miss));
        Assert.Contains(miss.Log, l => l.Contains("raté"));
    }

    [Fact]
    public void PoisonTicksAtTheStartOfTheTargetsTurns()
    {
        var (_, b, s) = Ready(new SkillDef
        {
            Id = "dard", Name = "Dard", Kind = SkillKind.Status,
            Effects = [new() { Type = EffectType.Poison, Amount = 7, Turns = 2 }],
        });
        b.UseSkill(s, b.Enemies[0]);
        Assert.Equal(7, Damage(b)); // premier tick au tour du mannequin
        b.Wait();
        Assert.Equal(14, Damage(b));
        b.Wait();
        Assert.Equal(14, Damage(b)); // effet terminé
        Assert.Empty(b.Enemies[0].Effects);
    }

    [Fact]
    public void StunSkipsATurn()
    {
        var (_, b, s) = Ready(new SkillDef
        {
            Id = "assomme", Name = "Assommer", Kind = SkillKind.Status,
            Effects = [new() { Type = EffectType.Stun, Turns = 1 }],
        });
        b.UseSkill(s, b.Enemies[0]);
        Assert.Contains(b.Log, l => l.Contains("étourdi et perd son tour"));
        Assert.Equal(b.Allies[0].Stats.MaxHp, b.Allies[0].Hp);
    }

    [Fact]
    public void BuffRaisesDamageAndCooldownBlocksReuse()
    {
        var (_, b, buff) = Ready(new SkillDef
        {
            Id = "rage", Name = "Rage", Kind = SkillKind.Status, Target = SkillTarget.Self, Cooldown = 2,
            Effects = [new() { Type = EffectType.StatUp, Stat = StatKind.Attack, Amount = 50, Turns = 3 }],
        });
        var hero = b.Allies[0];
        var baseAtk = hero.Stat(StatKind.Attack);
        b.UseSkill(buff);
        Assert.Equal(baseAtk * 150 / 100, hero.Stat(StatKind.Attack));
        Assert.False(b.CanUse(buff));
        b.Wait();
        Assert.False(b.CanUse(buff));
        b.Wait();
        Assert.True(b.CanUse(buff));
    }

    [Fact]
    public void ShieldAbsorbsDamage()
    {
        var (_, b, s) = Ready(new SkillDef { Id = "a", Name = "A", Power = 1 });
        var enemy = b.Enemies[0];
        enemy.Shield = 1000;
        b.UseSkill(s, enemy);
        Assert.Equal(0, Damage(b));
    }

    [Fact]
    public void ReviveTargetsKnockedOutAllies()
    {
        var (_, b, revive) = Ready(new SkillDef
        {
            Id = "releve", Name = "Relève", Kind = SkillKind.Revive, Target = SkillTarget.SingleAlly, FlatAmount = 30, Power = 0,
        }, s =>
        {
            s.Recruit("lyra");
            s.State.Party.First(c => c.DefId == "lyra").CurrentHp = 0;
        });
        var lyra = b.Allies.First(a => a.Name == "Lyra");
        Assert.False(lyra.IsAlive);
        Assert.Equal([lyra], b.TargetsFor(revive));
        b.UseSkill(revive, lyra);
        Assert.True(lyra.IsAlive);
        Assert.Contains(b.Log, l => l.Contains("Lyra se relève avec 30 PV"));
    }

    [Fact]
    public void HpCostAndDrain()
    {
        var (_, b, s) = Ready(new SkillDef { Id = "sang", Name = "Sang", Power = 1, HpCost = 10 });
        var hero = b.Allies[0];
        var before = hero.Hp;
        b.UseSkill(s, b.Enemies[0]);
        Assert.True(hero.Hp <= before - 10);

        var (_, d, drain) = Ready(new SkillDef { Id = "vampire", Name = "Vampire", Power = 1, DrainPercent = 100 });
        d.Allies[0].Hp = 10;
        d.UseSkill(drain, d.Enemies[0]);
        Assert.True(d.Allies[0].Hp > 10);
    }

    [Fact]
    public void SampleContentStaysValid() => Assert.Empty(GameDatabase.Default.Validate());
}
