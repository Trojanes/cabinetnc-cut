namespace CabinetNC.Domain.Parts;

using CabinetNC.Domain.Geometry;

public sealed class PanelFeature
{
    public required string FeatureId { get; init; }
    public required string Kind { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double? DiameterMm { get; init; }
    public double? DepthMm { get; init; }
    public double? WidthMm { get; init; }
    /// <summary>Groove polyline in panel-local mm.</summary>
    public IReadOnlyList<Point2>? Path { get; init; }
}

public sealed class Panel
{
    public required string PanelId { get; init; }
    public string? Name { get; init; }
    public string? Material { get; init; }
    public double ThicknessMm { get; init; }
    public int Quantity { get; init; } = 1;
    /// <summary>Allowed nest rotations in degrees (woodjob). Null = unconstrained.</summary>
    public IReadOnlyList<int>? AllowedRotations { get; init; }
    public string? GrainDirection { get; init; }
    public required Outline Outline { get; init; }
    public IReadOnlyList<PanelFeature> Features { get; init; } = [];

    /// <summary>True if 90° (or 270°) nest rotation is allowed.</summary>
    public bool MayRotate90 =>
        AllowedRotations is null
        || AllowedRotations.Count == 0
        || AllowedRotations.Any(r => Math.Abs(((r % 360) + 360) % 360 - 90) < 1e-6
            || Math.Abs(((r % 360) + 360) % 360 - 270) < 1e-6);
}
