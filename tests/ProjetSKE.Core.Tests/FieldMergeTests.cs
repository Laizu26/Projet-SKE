using ProjetSKE.Core.Cloud;
using ProjetSKE.Core.Data;

namespace ProjetSKE.Core.Tests;

/// <summary>Un même élément modifié des deux côtés : la fusion se fait réglage par réglage.</summary>
public class FieldMergeTests
{
    [Fact]
    public void SameNpcChangedOnBothSides_DifferentFields_KeepsBoth()
    {
        var baseline = ContentSerializer.Clone(GameDatabase.Default.Content);
        var local = ContentSerializer.Clone(baseline);
        var remote = ContentSerializer.Clone(baseline);
        local.Npcs.First(n => n.Id == "capitaine_hardin").LocationId = "taverne_sanglier"; // ici : le lieu
        remote.Npcs.First(n => n.Id == "capitaine_hardin").Description = "Changé ailleurs"; // ailleurs : la description

        var result = ContentMerger.Merge(baseline, local, remote);
        var npc = result.Merged.Npcs.First(n => n.Id == "capitaine_hardin");
        Assert.Equal("taverne_sanglier", npc.LocationId);
        Assert.Equal("Changé ailleurs", npc.Description);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void SameFieldChangedDifferently_OnlineWins_AndMineIsKept()
    {
        var baseline = ContentSerializer.Clone(GameDatabase.Default.Content);
        var local = ContentSerializer.Clone(baseline);
        var remote = ContentSerializer.Clone(baseline);
        local.Npcs.First(n => n.Id == "capitaine_hardin").LocationId = "taverne_sanglier";
        remote.Npcs.First(n => n.Id == "capitaine_hardin").LocationId = "route_roi";
        local.Npcs.First(n => n.Id == "capitaine_hardin").Description = "Ma description";

        var result = ContentMerger.Merge(baseline, local, remote);
        var npc = result.Merged.Npcs.First(n => n.Id == "capitaine_hardin");
        Assert.Equal("route_roi", npc.LocationId);          // même réglage des deux côtés : en ligne
        Assert.Equal("Ma description", npc.Description);    // réglage changé seulement ici : gardé
        var conflict = Assert.Single(result.Conflicts);
        Assert.Contains("taverne_sanglier", conflict.LocalJson); // ma version reste restaurable
    }
}
