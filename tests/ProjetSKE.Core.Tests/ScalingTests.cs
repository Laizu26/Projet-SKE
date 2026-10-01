using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Montants selon une stat : dégâts = 50 + 30 % des PV max ; bouclier = 20 + 50 % des PV max de l'allié.</summary>
public class ScalingTests
{
    private static GameContent Content() => ContentSerializer.Clone(GameDatabase.Default.Content);

    [Fact]
    public void SkillDamage_AddsAPercentOfMaxHp()
    {
        var content = Content();
        // Puissance 0 : seulement « 50 + 30 % des PV max du lanceur » (moins rien d'autre), sans hasard.
        content.Balance.DamageVariancePercent = 0;
        content.Skills.Add(new SkillDef
        {
            Id = "colosse", Name = "Coup du colosse", Kind = SkillKind.Physical, Power = 0, FlatAmount = 50,
            Scalings = [new StatScaling { Stat = "pvmax", Percent = 30 }],
        });
        content.Characters.First(c => c.Id == "aldric").Skills.Add(new SkillUnlock(1, "colosse"));
        content.Monsters[0].Stats.MaxHp = 5000;
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        var b = s.StartBattle([content.Monsters[0].Id]);
        var hero = b.Allies[0];
        var foe = b.Enemies[0];
        var before = foe.Hp;
        b.UseSkill(db.Skills["colosse"], foe);
        Assert.Equal(1 + 50 + hero.Stats.MaxHp * 30 / 100, before - foe.Hp);
    }

    [Fact]
    public void Shield_ScalesWithTheTargetsMaxHp()
    {
        var content = Content();
        content.Passives.Add(new PassiveDef
        {
            Id = "egide", Name = "Égide",
            Triggers = [new PassiveTrigger
            {
                Threshold = 30,
                Effects = [new SkillEffect { Type = EffectType.Shield, Amount = 20, Turns = 3, Scalings = [new StatScaling { Stat = "pvmax", Percent = 50, OfTarget = true }] }],
            }],
        });
        content.Characters.First(c => c.Id == "lyra").Passives.Add(new PassiveUnlock(1, "egide"));
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        s.Recruit("lyra");
        foreach (var c in s.State.Party) c.IsActive = true;
        var b = s.StartBattle([content.Monsters[0].Id]);
        var aldric = b.Allies.First(a => a.Id == "aldric");
        aldric.Hp = 1;
        b.Defend();
        Assert.Contains(b.Log, l => l.Contains($"protégé ({20 + aldric.Stats.MaxHp * 50 / 100})"));
    }
}
