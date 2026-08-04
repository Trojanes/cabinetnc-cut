using CabinetNC.Domain;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class LabelBomBuilderTests
{
    [Fact]
    public void Label_ids_subset_of_manifest_workpiece_ids()
    {
        var panels = new[]
        {
            new Panel
            {
                PanelId = "P1", Material = "oak", ThicknessMm = 18,
                Identity = new WorkpieceIdentity { WorkpieceId = "WP-1", ModuleId = "M1", ProjectId = "PRJ" },
                Outline = new Outline { Points = [new(0, 0), new(100, 0), new(100, 50), new(0, 50)] },
            },
        };
        var pkg = new CutPackage { SchemaName = CutPackage.Schema, Panels = panels, JobId = "job" };
        var places = new[] { new NestPlacement { PanelId = "P1", SheetIndex = 0, OffsetX = 0, OffsetY = 0 } };
        var ops = OpsPlanner.AttachToNest(OpsPlanner.FeaturesToOps(panels), places);
        var bundle = SheetBundleBuilder.Build(pkg, places, ops, MachineCatalog.Get("nesting_router_6"));
        Assert.False(string.IsNullOrWhiteSpace(bundle.BomCsv));
        Assert.Contains("WP-1", bundle.BomCsv);
        Assert.Contains("WP-1", bundle.LabelsHtml);
        var manifestIds = bundle.Sheets
            .SelectMany(s => s.PanelIds)
            .Select(pid => panels.First(p => p.PanelId == pid).Identity!.WorkpieceId!)
            .ToHashSet();
        Assert.All(bundle.Labels, l => Assert.Contains(l.WorkpieceId, manifestIds));
    }

    [Fact]
    public void Dxf_rectangle_imports_outline()
    {
        var dxf = NestDxfWriter.Write(
            new CutPackage
            {
                SchemaName = CutPackage.Schema,
                Panels =
                [
                    new Panel
                    {
                        PanelId = "R", ThicknessMm = 18,
                        Outline = new Outline
                        {
                            Points = [new(0, 0), new(200, 0), new(200, 100), new(0, 100)],
                        },
                    },
                ],
            },
            [new NestPlacement { PanelId = "R", SheetIndex = 0, OffsetX = 0, OffsetY = 0 }],
            sheetIndex: 0,
            includeFeatures: false);
        var panel = DxfOutlineImporter.TryImportRectanglePanel(dxf, "IMP", 18);
        Assert.NotNull(panel);
        Assert.True(panel!.Outline.Points.Count >= 4);
        var (w, h) = GroupedBlfNester.SizeOfOutline(panel);
        Assert.Equal(200, w, 1);
        Assert.Equal(100, h, 1);
    }
}
