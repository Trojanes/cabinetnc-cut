using CabinetNC.Domain.Nesting;

namespace CabinetNC.Domain.Tests;

public class NestValidatorTests
{
    [Fact]
    public void Detects_aabb_gap_violation()
    {
        var parts = new[]
        {
            new NestPart { PanelId = "A", WidthMm = 100, HeightMm = 100 },
            new NestPart { PanelId = "B", WidthMm = 100, HeightMm = 100 },
        };
        var placements = new[]
        {
            new NestPlacement { PanelId = "A", SheetIndex = 0, OffsetX = 0, OffsetY = 0 },
            new NestPlacement { PanelId = "B", SheetIndex = 0, OffsetX = 50, OffsetY = 0 },
        };
        var hits = NestValidator.FindAabbCollisions(parts, placements, spacingMm: 12);
        Assert.Single(hits);
        Assert.Equal("A", hits[0].PanelIdA);
        Assert.Equal("B", hits[0].PanelIdB);
    }

    [Fact]
    public void Ok_when_gap_respected()
    {
        var parts = new[]
        {
            new NestPart { PanelId = "A", WidthMm = 100, HeightMm = 100 },
            new NestPart { PanelId = "B", WidthMm = 100, HeightMm = 100 },
        };
        var placements = new[]
        {
            new NestPlacement { PanelId = "A", SheetIndex = 0, OffsetX = 0, OffsetY = 0 },
            new NestPlacement { PanelId = "B", SheetIndex = 0, OffsetX = 120, OffsetY = 0 },
        };
        Assert.Empty(NestValidator.FindAabbCollisions(parts, placements, spacingMm: 12));
    }
}
