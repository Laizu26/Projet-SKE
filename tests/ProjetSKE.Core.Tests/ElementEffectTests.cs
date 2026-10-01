using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Faiblesses et résistances élémentaires posées par un effet (temporaires).</summary>
public class ElementEffectTests
{
    [Fact]
    public void Weakness_Effect_DoublesFireDamage_UntilItEnds()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Balance.DamageVariancePercent = 0;
        content.Skills.Add(new SkillDef { Id = "brasier", Name = "Brasier", Kind = SkillKind.Magical, Power = 1, Element = "feu" });
        content.Skills.Add(new SkillDef
        {
            Id = "marque", Name = "Marque ardente", Kind = SkillKind.Status,
            Effects = [new SkillEffect { Type = EffectType.Element, Element = "Feu", Amount = 200, Turns = 3 }],
        });
        var aldric = content.Characters.First(c => c.Id == "aldric");
        aldric.Skills.Add(new SkillUnlock(1, "brasier"));
        aldric.Skills.Add(new SkillUnlock(1, "marque"));
        content.Monsters[0].Stats.MaxHp = 5000;
        content.Monsters[0].Resistances = [];
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());

        int FireDamage(bool marked)
        {
            var s = GameSession.NewGame(db, "aldric", new Random(1));
            var b = s.StartBattle([content.Monsters[0].Id]);
            var foe = b.Enemies[0];
            if (marked)
            {
                foe.Effects.Add(new ActiveEffect { Def = db.Skills["marque"].Effects[0], Source = "test", TurnsLeft = 3 });
                Assert.Contains("faiblesse", foe.StatusText);
            }
            var before = foe.Hp;
            b.UseSkill(db.Skills["brasier"], foe);
            return before - foe.Hp;
        }
        var normal = FireDamage(false);
        var weak = FireDamage(true);
        Assert.InRange(weak, normal * 2 - 2, normal * 2 + 2);
    }

    [Fact]
    public void ElementEffect_WithoutElement_IsReported()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Skills.Add(new SkillDef { Id = "x", Name = "X", Kind = SkillKind.Status, Effects = [new SkillEffect { Type = EffectType.Element, Amount = 50 }] });
        Assert.Contains(new GameDatabase(content).Validate(), e => e.Contains("sans élément"));
    }
}
