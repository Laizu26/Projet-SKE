using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Condition « Nombre dans le groupe » : tout le groupe, titulaires, alliés / ennemis encore debout.</summary>
public class PartyCountTests
{
    [Fact]
    public void Counts_Group_Active_AndStanding()
    {
        var s = GameSession.NewGame(GameDatabase.Default, "aldric", new Random(1));
        s.Recruit("lyra");
        s.State.Party.First(c => c.DefId == "lyra").IsActive = false; // en réserve
        Assert.True(s.Check(new Condition(ConditionType.PartySize, "", 2) { Op = CompareOp.Equal }));
        Assert.True(s.Check(new Condition(ConditionType.PartySize, "actifs", 1) { Op = CompareOp.Equal }));
        Assert.False(s.Check(new Condition(ConditionType.PartySize, "ennemis", 1))); // hors combat : 0
    }

    [Fact]
    public void LastOneStanding_InBattle()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        content.Passives.Add(new PassiveDef
        {
            Id = "dernier", Name = "Dernier debout", DamagePercent = 100,
            Conditions = [new(ConditionType.PartySize, "debout", 1) { Op = CompareOp.Equal }],
        });
        content.Characters.First(c => c.Id == "aldric").Passives.Add(new PassiveUnlock(1, "dernier"));
        var db = new GameDatabase(content);
        Assert.Empty(db.Validate());
        var s = GameSession.NewGame(db, "aldric", new Random(1));
        s.Recruit("lyra");
        foreach (var c in s.State.Party) c.IsActive = true;
        var b = s.StartBattle([content.Monsters[0].Id]);
        var aldric = b.Allies.First(a => a.Id == "aldric");
        Assert.Equal(1.0, s.PassiveDamageFactor(b, aldric, b.Enemies[0]));
        b.Allies.First(a => a.Id == "lyra").Hp = 0;
        Assert.Equal(2.0, s.PassiveDamageFactor(b, aldric, b.Enemies[0]));

        var nodes = DialogueScript.Parse("- x\n> y {taille_equipe = 1 debout} -> fin", out var errors);
        Assert.Empty(errors);
        Assert.Equal("debout", nodes[0].Choices[0].Conditions[0].Arg);
        Assert.Contains("{taille_equipe = 1 debout}", DialogueScript.Write(nodes));
    }
}
