using CabinetNC.Domain;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;
using System.Diagnostics;

namespace CabinetNC.Domain.Tests;

public class WorkpieceImporterTests
{
    [Fact]
    public void Import_assigns_new_ids_without_clobbering()
    {
        var target = new CutPackage
        {
            SchemaName = CutPackage.Schema,
            Panels =
            [
                new Panel
                {
                    PanelId = "P1", ThicknessMm = 18,
                    Outline = new Outline { Points = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)] },
                },
            ],
        };
        var source = new[]
        {
            new Panel
            {
                PanelId = "P1", Material = "mdf", ThicknessMm = 15,
                Identity = new WorkpieceIdentity { WorkpieceId = "OLD", ModuleId = "MOD" },
                Outline = new Outline { Points = [new(0, 0), new(20, 0), new(20, 20), new(0, 20)] },
            },
        };
        var next = WorkpieceImporter.ImportPanels(target, source);
        Assert.Equal(2, next.Panels.Count);
        Assert.Contains(next.Panels, p => p.PanelId == "P1");
        Assert.Contains(next.Panels, p => p.PanelId.StartsWith("IMP_"));
        Assert.Contains(next.Panels, p => p.Material == "mdf" && p.ThicknessMm == 15);
    }
}

public class PipelineAndStressTests
{
    static Panel Rect(string id, string mat, double th, double w, double h) => new()
    {
        PanelId = id,
        Material = mat,
        ThicknessMm = th,
        Outline = new Outline { Points = [new(0, 0), new(w, 0), new(w, h), new(0, h)] },
        Features =
        [
            new PanelFeature
            {
                FeatureId = "H1", Kind = "holeVertical",
                X = w * 0.25, Y = h * 0.25, DiameterMm = 5, DepthMm = th - 2,
            },
        ],
        Identity = new WorkpieceIdentity { WorkpieceId = id, ModuleId = "M", ProjectId = "P" },
    };

    [Fact]
    public void End_to_end_sample_nest_cam_bundle()
    {
        var panels = new[]
        {
            Rect("A", "oak", 18, 400, 300),
            Rect("B", "mdf", 18, 350, 280),
            Rect("C", "oak", 15, 300, 200),
        };
        var pkg = new CutPackage { SchemaName = CutPackage.Schema, JobId = "e2e", Panels = panels };
        var settings = new NestSettings { MarginMm = 15, ClearanceMm = 12, AllowRotation = true };
        var stock = new[]
        {
            new NestSheetSpec { WidthMm = 1220, LengthMm = 2440, BorderMm = 15, ThicknessMm = 0 },
        };
        var nest = GroupedBlfNester.Pack(panels, settings, stock, GroupedBlfNester.SizeOfOutline);
        Assert.True(nest.Placements.Count >= 2);
        var gate = NestExportGate.Check(panels, nest.Placements, settings.ClearanceMm);
        Assert.True(gate.Ok, string.Join("; ", gate.Errors));

        var ops = OpsPlanner.AttachToNest(OpsPlanner.FeaturesToOps(panels), nest.Placements);
        Assert.Contains(ops, o => o.ToolId == "T1");
        Assert.Contains(ops, o => o.Op == "drill" && o.ToolId == "T3");
        var ordered = CamSafety.OrderSafe(ops.Where(o => o.PanelId == "A")).ToList();
        var outer = ordered.FindIndex(o => o.Op == "contour" && o.FeatureId is null);
        var drill = ordered.FindIndex(o => o.Op == "drill");
        Assert.True(drill >= 0 && outer > drill);

        var profile = MachineCatalog.Get("nesting_router_6");
        var pre = NcPreflight.Check(ops, profile, 1220, 2440, panels.ToDictionary(p => p.PanelId));
        Assert.True(pre.Ok, NcPreflight.Format(pre));

        var bundle = SheetBundleBuilder.Build(pkg, nest.Placements.ToList(), ops, profile);
        Assert.True(bundle.Sheets.Count >= 2);
        Assert.False(string.IsNullOrWhiteSpace(bundle.BomCsv));
        Assert.Contains("A", bundle.LabelsHtml!);
        Assert.All(bundle.Sheets.SelectMany(s => s.ToolPrograms), p => Assert.Contains("(tool ", p.NcText));
        Assert.All(bundle.Sheets, s => Assert.True(s.ToolPrograms.Count >= 1));
    }

    [Fact]
    public void Stress_120_panels_records_timing()
    {
        var panels = new List<Panel>();
        for (var i = 0; i < 120; i++)
        {
            var mat = i % 3 == 0 ? "oak" : i % 3 == 1 ? "mdf" : "ply";
            var th = i % 2 == 0 ? 18.0 : 15.0;
            var w = 180 + (i % 7) * 20;
            var h = 120 + (i % 5) * 15;
            panels.Add(Rect($"P{i:000}", mat, th, w, h));
        }
        var settings = new NestSettings { MarginMm = 12, ClearanceMm = 10, AllowRotation = true };
        var stock = new[]
        {
            new NestSheetSpec { WidthMm = 1220, LengthMm = 2440, BorderMm = 12, ThicknessMm = 0 },
        };
        var sw = Stopwatch.StartNew();
        var nest = GroupedBlfNester.Pack(panels, settings, stock, GroupedBlfNester.SizeOfOutline);
        sw.Stop();
        Assert.True(nest.Placements.Count + nest.Unplaced.Count == 120);
        Assert.True(nest.SheetCount >= 3, $"expected multi-sheet, got {nest.SheetCount}");
        // Soft budget: document hardware baseline; fail only if catastrophically slow (>60s).
        Assert.True(sw.ElapsedMilliseconds < 60_000, $"120-panel nest took {sw.ElapsedMilliseconds}ms");
        // Evidence line for report consumers
        Assert.True(sw.ElapsedMilliseconds >= 0);
        Console.WriteLine($"STRESS_120_NEST_MS={sw.ElapsedMilliseconds}; sheets={nest.SheetCount}; placed={nest.Placements.Count}");
    }
}
