using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Passifs des PNJ (et des monstres) : actifs en combat, et pour la condition « A un passif ».</summary>
public class NpcPassiveTests
{
    [Fact]
    public void NpcPassives_ApplyInBattle_AndInConditions()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Passives.Add(new PassiveDef
        {
            Id = "cuirasse", Name = "Cuirassé", Bonus = new StatBlock(MaxHp: 50, Defense: 5),
            Resistances = [new ElementModifier { Element = "feu", Percent = 50 }],
        });
        var npc = content.Npcs.First(n => n.Combat is not null);
        npc.PassiveIds = ["cuirasse"];
        var monster = content.Monsters[0];
        monster.PassiveIds = ["cuirasse"];
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        var s = GameSession.NewGame(db, "aldric", new Random(1));

        var battle = s.StartBattle([npc.Id, monster.Id]);
        var foe = battle.Enemies[0];
        Assert.Equal(npc.Combat!.Stats.MaxHp + 50, foe.Stats.MaxHp);
        Assert.Equal(foe.Stats.MaxHp, foe.Hp);
        Assert.Equal(npc.Combat.Stats.Defense + 5, foe.Stats.Defense);
        Assert.Contains(foe.Resistances, r => r.Element == "feu");
        Assert.Equal(monster.Stats.MaxHp + 50, battle.Enemies[1].Stats.MaxHp);

        Assert.True(s.Check(new Condition(ConditionType.HasPassive, npc.Id) { Arg2 = "cuirasse" }));
        Assert.False(s.Check(new Condition(ConditionType.HasPassive, "@heros") { Arg2 = "cuirasse" }));
    }
}
