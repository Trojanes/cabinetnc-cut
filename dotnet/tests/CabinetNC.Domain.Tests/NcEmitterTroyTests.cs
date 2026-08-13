using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;

namespace CabinetNC.Domain.Tests;

public class NcEmitterTroyTests
{
    static MachineProfile Machine() => MachineCatalog.Get("nesting_router_6");

    static string Troy(params CutOp[] ops) =>
        NcEmitter.OpsToNc(ops, Machine(), recipe: PostRecipe.TroyDefault());

    static CutOp Drill(string panel = "P1", bool through = true, double depth = 18, double th = 18) => new()
    {
        Op = "drill",
        PanelId = panel,
        FeatureId = "H1",
        ToolId = "T3",
        Placed = true,
        SheetX = 30,
        SheetY = 40,
        DiameterMm = 3,
        DepthMm = depth,
        ThicknessMm = th,
        Through = through,
    };

    static CutOp Tongue() => new()
    {
        Op = "groove",
        PanelId = "P1",
        FeatureId = "TG1",
        ToolId = "T1",
        Placed = true,
        IsTongue = true,
        DepthMm = 9,
        ThicknessMm = 18,
        Path = [(10, 10), (190, 10)],
        ClosePath = false,
    };

    static CutOp Pocket() => new()
    {
        Op = "pocket",
        PanelId = "P1",
        FeatureId = "PK1",
        ToolId = "T2",
        Placed = true,
        DepthMm = 12,
        ThicknessMm = 18,
        ClosePath = false,
        PathSegments = [new (double X, double Y)[] { (20, 20), (80, 20) }],
        FinishLoop = [(22, 22), (78, 22), (78, 48), (22, 48), (22, 22)],
    };

    static CutOp Outer(string panel = "P1") => new()
    {
        Op = "contour",
        PanelId = panel,
        ToolId = "T2",
        Placed = true,
        ClosePath = true,
        Through = true,
        ThicknessMm = 18,
        DepthMm = 18.5,
        Path = [(0, 0), (200, 0), (200, 100), (0, 100)],
    };

    static CutOp Inner() => new()
    {
        Op = "contour",
        PanelId = "P1",
        FeatureId = "W1",
        ToolId = "T2",
        Placed = true,
        ClosePath = true,
        Through = true,
        ThicknessMm = 18,
        DepthMm = 18.5,
        Path = [(40, 30), (80, 30), (80, 70), (40, 70)],
    };

    static string[] Lines(string nc) => nc.Replace("\r\n", "\n").Split('\n');

    [Fact]
    public void Matches_OSAI_Troy_header_and_end()
    {
        var nc = Troy(Outer());
        var lines = Lines(nc);
        Assert.Equal("N1 G90 ", lines[0]);
        Assert.Equal("N2 G40 ", lines[1]);
        Assert.Equal("N3 G80 ", lines[2]);
        Assert.Equal("N4 (UAO,1)", lines[3]);
        Assert.Equal("N5 G79 Z0", lines[4]);
        Assert.Equal("N6 M05", lines[5]);
        Assert.Equal("N7 M52", lines[6]);
        Assert.Equal("N8 M6 T2", lines[7]);
        Assert.Equal("N9 M3 S14500", lines[8]);
        Assert.Equal("N10 (DLY,3)", lines[9]);
        Assert.Equal("N11 M49", lines[10]);
        Assert.Equal("N12 G27", lines[11]);
        Assert.Equal("N13 G17", lines[12]);
        Assert.Equal("N14 G0 X0.0000 Y0.0000", lines[13]);
        Assert.Contains("G0 Z30.0000", nc);
        Assert.DoesNotContain("G21", nc);
        Assert.DoesNotContain("S14500 M3", nc);
        var body = lines.Where(l => l.Length > 0).ToList();
        Assert.Contains("G0 X0.0000 Y0.0000", body[^5]);
        Assert.EndsWith(" G80", body[^4]);
        Assert.EndsWith(" M5", body[^3]);
        Assert.EndsWith(" G79 Z0", body[^2]);
        Assert.EndsWith(" M30", body[^1]);
        Assert.DoesNotContain("\nM2\n", "\n" + nc.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Home_xy_at_end_can_be_turned_off()
    {
        var nc = NcEmitter.OpsToNc([Outer()], Machine(), recipe: new PostRecipe { HomeXyAtEnd = false });
        var body = Lines(nc).Where(l => l.Length > 0).ToList();
        Assert.EndsWith(" G80", body[^4]);
        Assert.DoesNotContain("G0 X0.0000 Y0.0000", body[^5]);
    }

    [Fact]
    public void Drill_then_tongue_then_clearance_then_profile_with_toolchange()
    {
        var nc = Troy(Outer(), Pocket(), Drill(), Tongue(), Inner());
        var t3 = nc.IndexOf("M6 T3", StringComparison.Ordinal);
        var t1 = nc.IndexOf("M6 T1", StringComparison.Ordinal);
        var t2 = nc.IndexOf("M6 T2", StringComparison.Ordinal);
        Assert.True(t3 >= 0 && t1 >= 0 && t2 >= 0);
        Assert.True(t3 < t1);
        Assert.True(t1 < t2);
        Assert.Contains("Z0.5000", nc);
        Assert.Contains("Z-0.5500", nc);
        var f12 = nc.IndexOf("F12000.0", StringComparison.Ordinal);
        var f20 = nc.IndexOf("F20000.0", StringComparison.Ordinal);
        Assert.True(f12 >= 0 && f20 > f12);
    }

    [Fact]
    public void Shop_feeds_and_board_bottom_z()
    {
        var nc = Troy(Outer(), Tongue(), Pocket(), Drill());
        Assert.Contains("F1000.0", nc);
        Assert.Contains("F9000.0", nc);
        Assert.Contains("F12000.0", nc);
        Assert.Contains("F20000.0", nc);
        Assert.Contains("G1 Z9.0000 F1000.0", nc);
        Assert.Contains("G1 X190.0000 F9000.0", nc);
        Assert.Contains("G1 Z6.0000 F1000.0", nc);
    }

    [Fact]
    public void Blind_drill_stops_above_bottom_through_drill_overshoots()
    {
        var blind = Troy(Drill(through: false, depth: 12, th: 18));
        Assert.Contains("G1 Z6.0000 F1000.0", blind);
        Assert.DoesNotContain("Z-0.5500", blind);

        var through = Troy(Drill(through: true));
        Assert.Contains("G1 Z-0.5500 F1000.0", through);
    }

    [Fact]
    public void Last_pass_rapids_over_bridges()
    {
        var recipe = new PostRecipe
        {
            Bridges =
            [
                new ProfileBridge
                {
                    Id = "b1",
                    PanelId = "P1",
                    SheetIndex = 0,
                    ArcLengthMm = 100,
                    X = 100,
                    Y = 0,
                    WidthMm = 10,
                },
            ],
        };
        var nc = NcEmitter.OpsToNc([Outer()], Machine(), recipe: recipe);
        Assert.Contains("G0 Z30.0000", nc);
        Assert.Contains("G0 X105.0000", nc);
        Assert.Contains("Z-0.5500", nc);
        Assert.Contains("F20000.0", nc);
    }

    [Fact]
    public void First_pass_ramp_recuts_entry_at_leave_z()
    {
        var recipe = new PostRecipe { ProfileFirstRamp45 = true };
        var square = Outer() with
        {
            Path = [(0, 0), (100, 0), (100, 100), (0, 100)],
        };
        var nc = NcEmitter.OpsToNc([square], Machine(), recipe: recipe);
        Assert.Contains("G1 X29.5000 Z0.5000 F1000.0", nc);
        Assert.Contains("F12000.0", nc);
    }

    [Fact]
    public void Legacy_path_unchanged_without_recipe()
    {
        var nc = NcEmitter.OpsToNc(
            [Outer() with { Path = [(20, 20), (30, 20), (30, 30), (20, 30)] }],
            Machine());
        Assert.Contains("depth=18.5", nc);
        Assert.DoesNotContain("(UAO,1)", nc);
        Assert.DoesNotContain("G79 Z0", nc);
    }
}
