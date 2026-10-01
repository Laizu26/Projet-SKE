using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Pénétration d'armure (% de DEF ignoré) et de bouclier (% des dégâts qui passe sur les PV).</summary>
public class PenetrationTests
{
    private static (Battle B, SkillDef Skill) Game(int armorPen, int shieldPen)
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Balance.DamageVariancePercent = 0;
        content.Skills.Add(new SkillDef { Id = "perce", Name = "Perce", Kind = SkillKind.Physical, Power = 3, ArmorPenetration = armorPen, ShieldPenetration = shieldPen });
        content.Characters.First(c => c.Id == "aldric").Skills.Add(new SkillUnlock(1, "perce"));
        var m = content.Monsters[0];
        m.Stats.MaxHp = 5000;
        m.Stats.Defense = 20;
        m.Resistances = [];
        var db = new GameDatabase(content);
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        return (s.StartBattle([m.Id]), db.Skills["perce"]);
    }

    [Fact]
    public void ArmorPenetration_IgnoresPartOfTheDefense()
    {
        int Hit(int pen)
        {
            var (b, skill) = Game(pen, 0);
            var foe = b.Enemies[0];
            var before = foe.Hp;
            b.UseSkill(skill, foe);
            return before - foe.Hp;
        }
        Assert.True(Hit(100) > Hit(50));
        Assert.True(Hit(50) > Hit(0));
    }

    [Fact]
    public void ShieldPenetration_HitsHpThroughTheShield()
    {
        var (b, skill) = Game(0, 50);
        var foe = b.Enemies[0];
        foe.Shield = 10000;
        var before = foe.Hp;
        b.UseSkill(skill, foe);
        Assert.True(foe.Hp < before); // la moitié des dégâts passe le bouclier
        var (b2, skill2) = Game(0, 0);
        var foe2 = b2.Enemies[0];
        foe2.Shield = 10000;
        b2.UseSkill(skill2, foe2);
        Assert.Equal(foe2.Stats.MaxHp, foe2.Hp); // sans pénétration : tout dans le bouclier
    }
}
