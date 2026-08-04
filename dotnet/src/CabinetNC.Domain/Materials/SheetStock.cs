namespace CabinetNC.Domain.Materials;

public sealed class SheetStock
{
    public required string SheetId { get; init; }
    public string? Material { get; init; }
    public double ThicknessMm { get; init; }
    public double WidthMm { get; init; }
    /// <summary>Sheet Y extent (woodjob heightMm maps here).</summary>
    public double LengthMm { get; init; }
    public double MarginMm { get; init; }
    public double KerfMm { get; init; }
    public double PartClearanceMm { get; init; }
    /// <summary>Defect / keep-out rectangles in sheet local mm [xmin,ymin,xmax,ymax] as polygon AABB.</summary>
    public IReadOnlyList<DefectRegion> DefectRegions { get; init; } = [];
}

public sealed class DefectRegion
{
    public required string Id { get; init; }
    public double MinX { get; init; }
    public double MinY { get; init; }
    public double MaxX { get; init; }
    public double MaxY { get; init; }
}
