using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class DesktopWorkerNestParityTests
{
    [Fact]
    public void Grouped_blf_matches_same_inputs_for_single_material()
    {
        var panels = new[]
        {
            new Panel
            {
                PanelId = "A", Material = "oak", ThicknessMm = 18,
                Outline = new Outline { Points = [new(0, 0), new(400, 0), new(400, 300), new(0, 300)] },
            },
            new Panel
            {
                PanelId = "B", Material = "oak", ThicknessMm = 18,
                Outline = new Outline { Points = [new(0, 0), new(350, 0), new(350, 250), new(0, 250)] },
            },
        };
        var settings = new NestSettings { MarginMm = 15, ClearanceMm = 12, AllowRotation = true };
        var stock = new[]
        {
            new NestSheetSpec { WidthMm = 1220, LengthMm = 2440, BorderMm = 15, ThicknessMm = 0 },
        };

        var viaRouter = new NestEngineRouter().Run(new NestEngineRequest
        {
            Panels = panels,
            Settings = settings,
            StockTemplates = stock,
            SizeOf = GroupedBlfNester.SizeOfOutline,
            EnginePreference = "blf",
        }).Result;

        var viaGroup = GroupedBlfNester.Pack(panels, settings, stock, GroupedBlfNester.SizeOfOutline);

        Assert.Equal(viaGroup.Placements.Count, viaRouter.Placements.Count);
        Assert.Equal(viaGroup.SheetCount, viaRouter.SheetCount);
        Assert.Equal("grouped_blf_v0", viaRouter.Engine);
        foreach (var g in viaGroup.Placements)
        {
            var r = viaRouter.Placements.Single(p => p.PanelId == g.PanelId);
            Assert.Equal(g.SheetIndex, r.SheetIndex);
            Assert.Equal(g.OffsetX, r.OffsetX, 3);
            Assert.Equal(g.OffsetY, r.OffsetY, 3);
            Assert.Equal(g.RotationDeg, r.RotationDeg, 3);
        }
    }
}
