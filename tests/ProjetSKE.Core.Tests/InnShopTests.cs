using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;

namespace ProjetSKE.Core.Tests;

/// <summary>Auberge et boutique réglées lieu par lieu.</summary>
public class InnShopTests
{
    [Fact]
    public void OldContent_KeepsInnAndShop_InMainCities_Only()
    {
        var city = new LocationDef { Id = "v", Type = LocationType.City };
        var inner = new LocationDef { Id = "t", Type = LocationType.City, ParentId = "v" };
        var wild = new LocationDef { Id = "f", Type = LocationType.Wild };
        Assert.True(city.HasInn && city.HasShop);
        Assert.False(inner.HasInn || inner.HasShop);
        Assert.False(wild.HasInn || wild.HasShop);
    }

    [Fact]
    public void Configured_InnAndShop_FollowTheLocation()
    {
        var content = ContentSerializer.Clone(GameDatabase.Default.Content);
        var start = content.Locations.First(l => l.Id == content.Start.LocationId);
        start.Inn = false; // ville sans auberge
        start.Shop = true;
        var s = GameSession.NewGame(new GameDatabase(content), "aldric", new Random(1));
        s.State.Gold = 500;
        Assert.False(s.HasInn);
        Assert.False(s.Rest());
        Assert.True(s.HasShop);
        Assert.NotEmpty(s.ShopStock);

        start.Shop = false;
        Assert.Empty(s.ShopStock);
        Assert.False(s.Buy(start.ShopItemIds[0]));
    }
}
