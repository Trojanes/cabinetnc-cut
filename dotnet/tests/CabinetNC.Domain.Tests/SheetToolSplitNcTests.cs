using CabinetNC.Domain;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class SheetToolSplitNcTests
{
    static Panel ContourDrillGroove(string id) => new()
    {
        PanelId = id,
        Material = "oak",
        ThicknessMm = 18,
        Outline = new Outline { Points = [new(0, 0), new(200, 0), new(200, 120), new(0, 120)] },
        Features =
        [
            new PanelFeature
            {
                FeatureId = "H1", Kind = "holeVertical",
                X = 40, Y = 40, DiameterMm = 3, DepthMm = 12,
            },
            new PanelFeature
            {
                FeatureId = "G1", Kind = "grooveVertical", Purpose = "tongue",
                DepthMm = 6,
                Path = [new(20, 20), new(160, 20)],
            },
        ],
    };

    [Fact]
    public void Two_sheets_three_tools_yield_six_single_tool_nc_files()
    {
        var panels = new[] { ContourDrillGroove("A"), ContourDrillGroove("B") };
        var pkg = new CutPackage { SchemaName = CutPackage.Schema, JobId = "JOB", Panels = panels };
        var places = new[]
        {
            new NestPlacement { PanelId = "A", SheetIndex = 0, OffsetX = 20, OffsetY = 20 },
            new NestPlacement { PanelId = "B", SheetIndex = 1, OffsetX = 20, OffsetY = 20 },
        };
        var ops = OpsPlanner.AttachToNest(OpsPlanner.FeaturesToOps(panels), places);
        var catalog = ToolCatalog.DefaultMap();
        var bundle = SheetBundleBuilder.Build(pkg, places, ops, MachineCatalog.Get("nesting_router_6"));

        Assert.Equal(2, bundle.Sheets.Count);
        var ncNames = bundle.Sheets.SelectMany(s => s.ToolPrograms.Select(t => t.NcFileName)).OrderBy(x => x).ToList();
        Assert.Equal(
            new[]
            {
                "JOB_S1_T1.nc", "JOB_S1_T2.nc", "JOB_S1_T3.nc",
                "JOB_S2_T1.nc", "JOB_S2_T2.nc", "JOB_S2_T3.nc",
            },
            ncNames);

        foreach (var sheet in bundle.Sheets)
        {
            Assert.Equal($"JOB_S{sheet.SheetIndex + 1}.dxf", sheet.DxfFileName);
            Assert.Equal(3, sheet.ToolPrograms.Count);
            foreach (var prog in sheet.ToolPrograms)
            {
                var toolLines = prog.NcText.Replace("\r\n", "\n").Split('\n')
                    .Where(l => l.StartsWith("(tool ", StringComparison.Ordinal)).ToList();
                Assert.Single(toolLines);
                Assert.Equal($"(tool {prog.ToolId})", toolLines[0]);

                var def = catalog[prog.ToolId];
                Assert.Contains($"(sheet S{sheet.SheetIndex + 1}", prog.NcText);
                Assert.Contains($"ToolId={prog.ToolId}", prog.NcText);
                Assert.Contains($"DiameterMm={Fmt(def.DiameterMm)}", prog.NcText);
                Assert.Contains($"FeedXY={Fmt(def.FeedXyMmMin)}", prog.NcText);
                Assert.Contains($"FeedZ={Fmt(def.FeedZMmMin)}", prog.NcText);
                Assert.Contains($"RPM={Math.Round(def.SpindleRpm)}", prog.NcText);
                Assert.Contains($"S{Math.Round(def.SpindleRpm)}", prog.NcText);
                if (prog.ToolId == "T2")
                    Assert.Contains($"F{Fmt(def.FeedXyMmMin)}", prog.NcText);
                if (prog.ToolId == "T3")
                    Assert.Contains($"F{Fmt(def.FeedZMmMin)}", prog.NcText);
            }
            Assert.Contains("JOB_S" + (sheet.SheetIndex + 1) + "_T1.nc", sheet.ManifestJson);
            Assert.Contains("\"programs\"", sheet.ManifestJson);
        }
    }

    [Fact]
    public void Missing_tool_id_blocks_split_export()
    {
        var ops = new[]
        {
            new CutOp
            {
                Op = "contour", PanelId = "P", Placed = true, SheetIndex = 0,
                ToolId = null, DepthMm = 18.5,
                Path = [(0, 0), (10, 0), (10, 10), (0, 10)],
            },
        };
        var pkg = new CutPackage
        {
            SchemaName = CutPackage.Schema,
            JobId = "x",
            Panels =
            [
                new Panel
                {
                    PanelId = "P", ThicknessMm = 18,
                    Outline = new Outline { Points = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)] },
                },
            ],
        };
        var places = new[] { new NestPlacement { PanelId = "P", SheetIndex = 0, OffsetX = 0, OffsetY = 0 } };
        Assert.ThrowsAny<Exception>(() =>
            SheetBundleBuilder.Build(pkg, places, ops, MachineCatalog.Get("nesting_router_6")));
    }

    [Fact]
    public void Tool_change_post_interface_exists_but_emits_no_m6_by_default()
    {
        IToolChangePost post = new NullToolChangePost();
        Assert.Null(post.EmitToolChange(ToolCatalog.DefaultPresets[0], MachineCatalog.Get("nesting_router_6")));
        Assert.Equal("none", post.Id);
    }

    static string Fmt(double n) => Math.Round(n, 3).ToString("0.###");
}
