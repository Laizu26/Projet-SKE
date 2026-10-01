using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Passifs en combat : déclencheurs (bouclier sur un allié en danger) et passifs revérifiés à chaque tour.</summary>
public class PassiveTriggerTests
{
    private static (GameSession S, Battle B) Game(PassiveDef passive, string bearer = "lyra")
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Passives.Add(passive);
        content.Characters.First(c => c.Id == bearer).Passives.Add(new PassiveUnlock(1, passive.Id));
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        s.Recruit("lyra");
        foreach (var c in s.State.Party) c.IsActive = true;
        return (s, s.StartBattle([content.Monsters[0].Id]));
    }

    private static PassiveDef Guardian() => new()
    {
        Id = "gardienne", Name = "Gardienne",
        Triggers =
        [
            new PassiveTrigger
            {
                When = PassiveTriggerWhen.AllyHpBelow, Threshold = 30, Target = PassiveTriggerTarget.Concerned,
                Effects = [new SkillEffect { Type = EffectType.Shield, Amount = 25, Turns = 3 }],
            },
        ],
    };

    [Fact]
    public void AllyFallingUnder30Percent_GetsAShield_Once()
    {
        var (_, b) = Game(Guardian());
        var aldric = b.Allies.First(a => a.Id == "aldric");
        Assert.DoesNotContain(b.Log, l => l.Contains("Gardienne"));
        aldric.Hp = aldric.Stats.MaxHp / 5; // 20 %
        b.Defend(); // une action : le combat revérifie les passifs
        Assert.True(aldric.Shield >= 25 || !aldric.IsAlive);
        Assert.Contains(b.Log, l => l.Contains("Gardienne"));
        var count = b.Log.Count(l => l.Contains("Gardienne"));
        b.Defend();
        Assert.Equal(count, b.Log.Count(l => l.Contains("Gardienne"))); // une seule fois
    }

    [Fact]
    public void Bearer_IsNotItsOwnAlly()
    {
        var (_, b) = Game(Guardian());
        var lyra = b.Allies.First(a => a.Id == "lyra");
        lyra.Hp = 1;
        b.Defend();
        Assert.DoesNotContain(b.Log, l => l.Contains("Gardienne"));
    }

    [Fact]
    public void StatBonus_ActivatesMidBattle_WhenHpDrops()
    {
        var (_, b) = Game(new PassiveDef
        {
            Id = "rage", Name = "Rage", Bonus = new StatBlock(Attack: 50),
            Conditions = [new(ConditionType.Stat, "@soi", 30) { Arg2 = "pv%", Op = CompareOp.Less }],
        }, bearer: "aldric");
        var aldric = b.Allies.First(a => a.Id == "aldric");
        var atq = aldric.Stats.Attack;
        var maxHp = aldric.Stats.MaxHp;
        aldric.Hp = 1;
        b.Defend();
        Assert.Equal(atq + 50, aldric.Stats.Attack);
        Assert.Equal(maxHp, aldric.Stats.MaxHp);
        Assert.Contains(b.Log, l => l.Contains("Rage") && l.Contains("s'active"));
    }
}
