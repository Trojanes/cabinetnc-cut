namespace CabinetNC.Domain.Geometry;

/// <summary>Production point in mm (display units). Integer µm conversion lives in GeometryConversion later.</summary>
public readonly record struct Point2(double X, double Y);

public sealed class Outline
{
    public required IReadOnlyList<Point2> Points { get; init; }
    public bool Closed { get; init; } = true;
    public string Frame { get; init; } = "panelLocal";
}
