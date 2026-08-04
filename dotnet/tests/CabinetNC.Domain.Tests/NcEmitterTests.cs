using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;

namespace CabinetNC.Domain.Tests;

public class NcEmitterTests
{
    [Fact]
    public void Emits_header_and_m2()
    {
        var ops = new[]
        {
            new CutOp
            {
                Op = "drill",
                PanelId = "P1",
                Placed = true,
                SheetIndex = 0,
                SheetX = 10,
                SheetY = 20,
                DiameterMm = 8,
                DepthMm = 12,
            },
        };
        var nc = NcEmitter.OpsToNc(ops, MachineCatalog.Get("nesting_router_6"));
        Assert.Contains("G21", nc);
        Assert.Contains("G90", nc);
        Assert.Contains("drill P1", nc);
        Assert.Contains("G0 X10 Y20", nc);
        Assert.Contains("M2", nc);
        Assert.Contains("nesting_router_6", nc);
    }

    [Fact]
    public void Fanuc_ends_with_m30()
    {
        var ops = new[]
        {
            new CutOp
            {
                Op = "drill",
                PanelId = "P1",
                Placed = true,
                SheetX = 1,
                SheetY = 2,
                DepthMm = 5,
            },
        };
        var nc = NcEmitter.OpsToNc(ops, MachineCatalog.Get("fanuc_like_m30"));
        Assert.Contains("M30", nc);
        Assert.Contains("G17", nc);
    }
}
