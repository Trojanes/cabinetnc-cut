using CabinetNC.Domain.Machines;

namespace CabinetNC.Domain.Tests;

public class MachineCatalogTests
{
    [Fact]
    public void Has_nesting_router_default()
    {
        var p = MachineCatalog.Get("nesting_router_6");
        Assert.Equal("nesting_router_6", p.Id);
        Assert.True(MachineCatalog.All.Count >= 3);
    }
}
