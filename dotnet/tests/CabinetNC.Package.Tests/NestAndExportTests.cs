using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain;

namespace CabinetNC.Package.Tests;

public class NestAndExportTests
{
    [Fact]
    public void Blf_avoids_blocked_defect_rect()
    {
        var result = BlfNester.Pack(new NestRequest
        {
            Parts = [new NestPart { PanelId = "P1", WidthMm = 100, HeightMm = 100 }],
            Sheets =
            [
                new NestSheetSpec
                {
                    WidthMm = 300,
                    LengthMm = 300,
                    BorderMm = 10,
                    Blocked = [new NestBlockedRect { MinX = 10, MinY = 10, MaxX = 200, MaxY = 200 }],
                },
            ],
            SpacingMm = 0,
            AllowRotation = false,
        });
        Assert.True(result.Placements.Count >= 1 || result.Unplaced.Contains("P1"));
        foreach (var p in result.Placements.Where(x => x.SheetIndex == 0))
        {
            var overlap =
                p.OffsetX < 200 && p.OffsetX + 100 > 10 &&
                p.OffsetY < 200 && p.OffsetY + 100 > 10;
            Assert.False(overlap, $"sheet0 placement overlaps defect at {p.OffsetX},{p.OffsetY}");
        }
    }

    [Fact]
    public void Preflight_flags_empty_ops()
    {
        var report = NcPreflight.Check([], MachineCatalog.Get("nesting_router_6"), 1220, 2440);
        Assert.False(report.Ok);
        Assert.Contains(report.Issues, i => i.Code == "no_ops");
    }

    [Fact]
    public void Ops_include_groove()
    {
        var panel = new CabinetNC.Domain.Parts.Panel
        {
            PanelId = "P1",
            Outline = new CabinetNC.Domain.Geometry.Outline
            {
                Points =
                [
                    new(0, 0), new(100, 0), new(100, 50), new(0, 50),
                ],
            },
            Features =
            [
                new CabinetNC.Domain.Parts.PanelFeature
                {
                    FeatureId = "G1",
                    Kind = "grooveVertical",
                    Path = [new(10, 10), new(90, 10)],
                    WidthMm = 6,
                    DepthMm = 8,
                },
            ],
        };
        var ops = OpsPlanner.FeaturesToOps([panel], enableGroove: true);
        Assert.Contains(ops, o => o.Op == "groove");
    }

    [Fact]
    public void Contour_offset_expands_closed_path()
    {
        var source = new CutOp
        {
            Op = "contour",
            PanelId = "P1",
            Placed = true,
            Path = [(0, 0), (100, 0), (100, 50), (0, 50)],
        };

        var result = ContourToolOffset.Apply([source], 3);
        var path = Assert.Single(result).Path!;

        Assert.True(path.Min(p => p.X) < -2.9);
        Assert.True(path.Max(p => p.X) > 102.9);
        Assert.True(path.Min(p => p.Y) < -2.9);
        Assert.True(path.Max(p => p.Y) > 52.9);
    }

    [Fact]
    public void Poly_verify_ignores_aabb_overlap_without_polygon_overlap()
    {
        var a = new CabinetNC.Domain.Parts.Panel
        {
            PanelId = "A",
            Outline = new CabinetNC.Domain.Geometry.Outline
            {
                Points = [new(0, 0), new(100, 0), new(0, 100)],
            },
        };
        var b = new CabinetNC.Domain.Parts.Panel
        {
            PanelId = "B",
            Outline = new CabinetNC.Domain.Geometry.Outline
            {
                Points = [new(100, 100), new(100, 20), new(20, 100)],
            },
        };
        var places = new[]
        {
            new NestPlacement { PanelId = "A", SheetIndex = 0 },
            new NestPlacement { PanelId = "B", SheetIndex = 0, OffsetX = 20, OffsetY = 20 },
        };

        var hits = NestValidator.FindPolygonCollisions([a, b], places, 0);

        Assert.Empty(hits);
    }

    [Fact]
    public void Cam_sim_expands_contour_closure_and_wraps_steps()
    {
        var op = new CutOp
        {
            Op = "contour",
            PanelId = "P1",
            Placed = true,
            Path = [(0, 0), (10, 0), (10, 10)],
        };

        var frames = CamSimulator.ExpandFrames([op]);

        Assert.Equal(4, frames.Count);
        Assert.Equal(frames[0].X, frames[^1].X);
        Assert.Equal(3, CamSimulator.Step(0, frames.Count, -1));
    }

    [Fact]
    public void Rotated_ops_use_placement_as_rotated_bbox_origin()
    {
        var panel = new CabinetNC.Domain.Parts.Panel
        {
            PanelId = "P1",
            Outline = new CabinetNC.Domain.Geometry.Outline
            {
                Points = [new(0, 0), new(100, 0), new(100, 50), new(0, 50)],
            },
            Features =
            [
                new CabinetNC.Domain.Parts.PanelFeature
                {
                    FeatureId = "H1",
                    Kind = "holeVertical",
                    X = 0,
                    Y = 0,
                    DiameterMm = 3,
                },
            ],
        };
        var ops = OpsPlanner.AttachToNest(
            OpsPlanner.FeaturesToOps([panel]),
            [new NestPlacement
            {
                PanelId = "P1",
                OffsetX = 10,
                OffsetY = 20,
                RotationDeg = 90,
            }]);

        var contour = ops.First(o => o.Op == "contour").Path!;
        var drill = ops.First(o => o.Op == "drill");
        Assert.Equal(10, contour.Min(p => p.X), 6);
        Assert.Equal(60, contour.Max(p => p.X), 6);
        Assert.Equal(20, contour.Min(p => p.Y), 6);
        Assert.Equal(120, contour.Max(p => p.Y), 6);
        Assert.Equal(60, drill.SheetX!.Value, 6);
        Assert.Equal(20, drill.SheetY!.Value, 6);
    }

    [Fact]
    public void Dxf_and_job_sheet_exports_contain_shop_data()
    {
        var panel = new CabinetNC.Domain.Parts.Panel
        {
            PanelId = "P1",
            Name = "Side",
            Material = "oak",
            ThicknessMm = 18,
            Outline = new CabinetNC.Domain.Geometry.Outline
            {
                Points = [new(0, 0), new(100, 0), new(100, 50), new(0, 50)],
            },
            Features =
            [
                new CabinetNC.Domain.Parts.PanelFeature
                {
                    FeatureId = "H1",
                    Kind = "holeVertical",
                    X = 20,
                    Y = 20,
                    DiameterMm = 5,
                },
                new CabinetNC.Domain.Parts.PanelFeature
                {
                    FeatureId = "G1",
                    Kind = "grooveVertical",
                    Path = [new(10, 10), new(90, 10)],
                    WidthMm = 6,
                },
            ],
        };
        var package = new CutPackage
        {
            SchemaName = CutPackage.Schema,
            JobId = "JOB-TEST",
            Panels = [panel],
        };
        var placements = new[]
        {
            new NestPlacement
            {
                PanelId = "P1",
                SheetIndex = 0,
                OffsetX = 10,
                OffsetY = 20,
                RotationDeg = 90,
            },
        };

        var dxf = NestDxfWriter.Write(package, placements, 0);
        var html = JobSheetBuilder.BuildHtml(
            package,
            MachineCatalog.Get("nesting_router_6"),
            placements,
            new HashSet<string> { "P1" },
            "预检通过",
            50,
            0);

        Assert.Contains("LWPOLYLINE", dxf);
        Assert.Contains("CIRCLE", dxf);
        Assert.Contains("GROOVE", dxf);
        var dxfLines = dxf.Split('\n', StringSplitOptions.TrimEntries);
        for (var i = 0; i + 1 < dxfLines.Length; i++)
        {
            if (dxfLines[i] is not ("10" or "20" or "11" or "21")) continue;
            Assert.True(
                double.TryParse(
                    dxfLines[i + 1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var coordinate),
                $"invalid DXF coordinate after group {dxfLines[i]}: {dxfLines[i + 1]}");
            Assert.True(coordinate >= 0, $"negative DXF coordinate after group {dxfLines[i]}: {coordinate}");
        }
        Assert.Contains("JOB-TEST", html);
        Assert.Contains("P1", html);
        Assert.Contains("oak", html);
        Assert.Contains("预检通过", html);
    }
}
