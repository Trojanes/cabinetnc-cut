using CabinetNC.Domain.Manufacturing;

namespace CabinetNC.Domain.Tests;

public class DoubleSideGateTests
{
    [Fact]
    public void Single_side_unaffected()
    {
        var ops = new[]
        {
            new CutOp { Op = "contour", PanelId = "P", Placed = true, Side = "A", ToolId = "T1" },
        };
        Assert.Empty(DoubleSideGate.CheckBackSideOps(ops, null));
    }

    [Fact]
    public void Back_side_without_registration_fails()
    {
        var ops = new[]
        {
            new CutOp { Op = "drill", PanelId = "P", Placed = true, Side = "B", ToolId = "T3" },
        };
        var issues = DoubleSideGate.CheckBackSideOps(ops, new FaceRegistration { Strategy = "none" });
        Assert.Contains(issues, i => i.Code == "no_registration" && i.Level == "error");
    }

    [Fact]
    public void Back_side_with_pins_allowed()
    {
        var ops = new[]
        {
            new CutOp { Op = "drill", PanelId = "P", Placed = true, Side = "B", ToolId = "T3" },
        };
        var issues = DoubleSideGate.CheckBackSideOps(ops, new FaceRegistration
        {
            Strategy = "pins",
            FlipAxis = "X",
            OriginNote = "SW after flip about X",
        });
        Assert.Empty(issues);
    }

    [Fact]
    public void Mirror_local_x()
    {
        var (x, y) = DoubleSideGate.MirrorLocal(10, 20, 100, 50, "X");
        Assert.Equal(90, x);
        Assert.Equal(20, y);
    }
}
