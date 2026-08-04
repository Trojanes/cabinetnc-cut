using CabinetNC.Domain.Nesting;

namespace CabinetNC.Domain.Tests;

public class BlfNesterTests
{
    [Fact]
    public void Packs_two_rects_on_one_sheet()
    {
        var result = BlfNester.Pack(new NestRequest
        {
            Parts =
            [
                new NestPart { PanelId = "A", WidthMm = 600, HeightMm = 400 },
                new NestPart { PanelId = "B", WidthMm = 500, HeightMm = 300 },
            ],
            SheetWidthMm = 1220,
            SheetLengthMm = 2440,
            SpacingMm = 12,
            BorderMm = 15,
            AllowRotation = true,
        });

        Assert.Equal(2, result.Placements.Count);
        Assert.Empty(result.Unplaced);
        Assert.Equal(1, result.SheetCount);
        Assert.Equal("worker_blf_v0", result.Engine);
    }
}
