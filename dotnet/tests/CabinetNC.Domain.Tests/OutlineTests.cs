using CabinetNC.Domain.Geometry;

namespace CabinetNC.Domain.Tests;

public class OutlineTests
{
    [Fact]
    public void Point2_holds_mm()
    {
        var p = new Point2(600, 400);
        Assert.Equal(600, p.X);
        Assert.Equal(400, p.Y);
    }
}
