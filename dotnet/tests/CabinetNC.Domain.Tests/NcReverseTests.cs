using CabinetNC.Domain;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class NcReverseTests
{
    static MachineProfile Machine() => MachineCatalog.Get("nesting_router_6");

    static CutOp Drill() => new()
    {
        Op = "drill",
        PanelId = "P1",
        FeatureId = "H1",
        ToolId = "T3",
        Placed = true,
        SheetX = 30,
        SheetY = 40,
        DiameterMm = 3,
        DepthMm = 18,
        ThicknessMm = 18,
        Through = true,
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

    static CutOp Outer() => new()
    {
        Op = "contour",
        PanelId = "P1",
        ToolId = "T2",
        Placed = true,
        ClosePath = true,
        Through = true,
        ThicknessMm = 18,
        DepthMm = 18.5,
        Path = [(0, 0), (200, 0), (200, 100), (0, 100)],
    };

    static CutOp InnerCutout() => new()
    {
        Op = "contour",
        PanelId = "P1",
        FeatureId = "CUT-1",
        ToolId = "T2",
        Placed = true,
        ClosePath = true,
        Through = true,
        ThicknessMm = 18,
        DepthMm = 18.5,
        Path = [(40, 20), (80, 20), (80, 70), (40, 70)],
    };

    static string EmitOffset(params CutOp[] ops)
    {
        var offset = ContourToolOffset.Apply(ops, 5);
        return NcEmitter.OpsToNc(offset, Machine(), recipe: PostRecipe.TroyDefault());
    }

    [Fact]
    public void Lexer_reads_header_and_skips_pro2()
    {
        var nc = """
            ;SELECT PROCESS
            (GTO,PRO1,!PROC(0)=1)
            (GTO,PRO2,!PROC(0)=2)
            "PRO2"
            N1 LS11='x'
            N2 M701
            "PRO1"
            N1 G90
            N2 G40
            N3 (UAO,1)
            N4 M6 T2
            N5 G0 X10.0000 Y20.0000 Z30.0000
            N6 G1 Z0.5000 F1000.0
            N7 M30
            """;
        var all = OsaiTroyLexer.Lex(nc);
        Assert.Contains(all, l => l.Label == "PRO1");
        var cut = OsaiTroyLexer.CutProgram(all);
        Assert.DoesNotContain(cut, l => l.Paren is not null && l.Paren.Contains("LS11", StringComparison.Ordinal));
        Assert.Contains(cut, l => l.Words.Any(w => w.Letter == 'G' && w.Number == 90));
        Assert.Contains(cut, l => l.Words.Any(w => w.Letter == 'M' && w.Number == 6));
    }

    [Fact]
    public void Parser_replays_self_emitted_header()
    {
        var nc = EmitOffset(Outer());
        var replay = OsaiTroyParser.Replay(nc);
        Assert.True(replay.Strokes.Count > 4);
        Assert.Contains(replay.Lines, l => l.Paren == "UAO,1");
        Assert.Contains(replay.Strokes, s => s.ToolNum == 2 && !s.Rapid);
        Assert.True(replay.SafeZMm >= 20);
    }

    [Fact]
    public void Infer_merges_two_pass_contour_and_keeps_drill_tongue()
    {
        var nc = EmitOffset(Outer(), Drill(), Tongue());
        var result = NcReverse.FromText(nc);
        Assert.Contains(result.Ops, o => o.Op == "drill");
        Assert.Contains(result.Ops, o => o.Op == "groove" && o.IsTongue);
        Assert.Equal(1, result.Ops.Count(o => o.Op == "contour"));
    }

    [Fact]
    public void Reverse_recovers_panel_bounds_and_hole()
    {
        var nc = EmitOffset(Outer(), Drill());
        var result = NcReverse.FromText(nc);
        Assert.DoesNotContain("no_contour", result.Warnings);
        Assert.DoesNotContain("no_panel", result.Warnings);
        Assert.Single(result.Panels);
        var panel = result.Panels[0];
        var w = panel.Outline.Points.Max(p => p.X) - panel.Outline.Points.Min(p => p.X);
        var h = panel.Outline.Points.Max(p => p.Y) - panel.Outline.Points.Min(p => p.Y);
        Assert.InRange(w, 196, 204);
        Assert.InRange(h, 96, 104);
        Assert.Contains(panel.Features, f => f.Kind.Contains("hole", StringComparison.OrdinalIgnoreCase));
        var hole = panel.Features.First(f => f.Kind.Contains("hole", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(hole.X, 25, 35);
        Assert.InRange(hole.Y, 35, 45);
    }

    [Fact]
    public void Infer_keeps_small_closed_window_as_contour()
    {
        var nc = EmitOffset(Outer(), InnerCutout());
        var result = NcReverse.FromText(nc);
        Assert.Equal(2, result.Ops.Count(o => o.Op == "contour"));
    }

    [Fact]
    public void Reverse_recovers_inner_cutout_not_cutter_center()
    {
        var nc = EmitOffset(Outer(), InnerCutout());
        var result = NcReverse.FromText(nc);
        Assert.Single(result.Panels);
        var cut = result.Panels[0].Features.Single(f => f.Kind == "cutout");
        Assert.NotNull(cut.Path);
        var w = cut.Path!.Max(p => p.X) - cut.Path.Min(p => p.X);
        var h = cut.Path.Max(p => p.Y) - cut.Path.Min(p => p.Y);
        Assert.InRange(w, 36, 44);
        Assert.InRange(h, 46, 54);
        Assert.InRange(cut.Path.Min(p => p.X), 36, 44);
        Assert.InRange(cut.Path.Min(p => p.Y), 16, 24);
    }

    [Fact]
    public void Reverse_two_outers_become_two_panels()
    {
        var a = Outer();
        var b = a with
        {
            PanelId = "P2",
            Path = [(300, 0), (400, 0), (400, 80), (300, 80)],
        };
        var nc = EmitOffset(a, b);
        var result = NcReverse.FromText(nc);
        Assert.Equal(2, result.Panels.Count);
    }

    [Fact]
    public void Recut_keeps_only_selected_panel_at_qty_one()
    {
        var a = Outer();
        var b = a with
        {
            PanelId = "P2",
            Path = [(300, 0), (400, 0), (400, 80), (300, 80)],
        };
        var nc = EmitOffset(a, b);
        var result = NcReverse.FromText(nc);
        Assert.Equal(2, result.Panels.Count);
        var keep = result.Panels[0].WithQuantity(1);
        var pkg = NcReverse.ToPackage(result, "recut").WithPanels([keep]);
        Assert.Single(pkg.Panels);
        Assert.Equal(1, pkg.Panels[0].Quantity);
    }

    [Fact]
    public void Package_from_reverse_is_cut_package()
    {
        var nc = EmitOffset(Outer());
        var result = NcReverse.FromText(nc);
        var pkg = NcReverse.ToPackage(result, "job-anc");
        Assert.Equal("job-anc", pkg.JobId);
        Assert.Equal(CutPackage.Schema, pkg.SchemaName);
        Assert.Single(pkg.Panels);
        Assert.Single(pkg.Sheets);
    }

    [Fact]
    public void Panel_sample_shop_file_replays_when_present()
    {
        var path = @"E:\Work\CNC software\G\user\PROGRAMS\Panel Sample.anc";
        if (!File.Exists(path))
            return;
        var nc = File.ReadAllText(path);
        var result = NcReverse.FromText(nc);
        Assert.True(result.Strokes.Count > 10);
        Assert.DoesNotContain("no_motion", result.Warnings);
    }
}
