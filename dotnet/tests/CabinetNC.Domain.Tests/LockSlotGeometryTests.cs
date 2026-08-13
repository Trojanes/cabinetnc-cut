using CabinetNC.Domain.Geometry;

namespace CabinetNC.Domain.Tests;

public class LockSlotGeometryTests
{
    static readonly Point2[] SharpLockSized =
    [
        new(0, 0),
        new(55, 0),
        new(55, 15.5),
        new(0, 15.5),
    ];

    [Fact]
    public void EnsureStadium_keeps_sharp_quad_without_lock_intent()
    {
        var kept = LockSlotGeometry.EnsureStadium(SharpLockSized);

        Assert.Equal(4, kept.Count);
    }

    [Fact]
    public void EnsureStadium_upgrades_sharp_quad_when_lock_tagged()
    {
        var stadium = LockSlotGeometry.EnsureStadium(SharpLockSized, purpose: "lock_cutout");

        Assert.True(stadium.Count > 8);
        Assert.Equal(0, stadium.Min(p => p.X), 3);
        Assert.Equal(55, stadium.Max(p => p.X), 3);
        Assert.Equal(0, stadium.Min(p => p.Y), 3);
        Assert.Equal(15.5, stadium.Max(p => p.Y), 3);
    }

    [Fact]
    public void EnsureStadium_upgrades_sharp_quad_when_hasArc_without_tag()
    {
        var stadium = LockSlotGeometry.EnsureStadium(SharpLockSized, hasArc: true);

        Assert.True(stadium.Count > 8);
    }

    [Fact]
    public void EnsureStadium_keeps_already_tessellated_stadium()
    {
        var points = LockSlotGeometry.CapsuleFromAabb(0, 55, 0, 15.5);
        Assert.True(points.Count > 8);

        var kept = LockSlotGeometry.EnsureStadium(points, purpose: "lock_cutout");

        Assert.Same(points, kept);
    }
}
