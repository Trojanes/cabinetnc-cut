namespace CabinetNC.Domain.Machines;

public sealed class MachineProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Dialect { get; init; } = "generic";
    public string ProgramEnd { get; init; } = "M2";
    public double SafeZMm { get; init; } = 5;
    public double FeedXyMmMin { get; init; } = 3000;
    public double FeedZMmMin { get; init; } = 500;
    public double SpindleRpm { get; init; } = 18000;
    public double ToolDiameterMm { get; init; } = 6;
    public double ContourDepthMm { get; init; } = 18;
    public double ContourStepdownMm { get; init; }
    public double DrillPeckMm { get; init; }
    public bool EnableContour { get; init; } = true;
    public bool EnableDrill { get; init; } = true;
    public bool EnableGroove { get; init; } = true;
    public string? OriginNote { get; init; }
}

public static class MachineCatalog
{
    public static IReadOnlyList<MachineProfile> All { get; } =
    [
        new()
        {
            Id = "generic_cnc_mm",
            Name = "Generic CNC (mm)",
            Dialect = "generic",
            ProgramEnd = "M2",
            SafeZMm = 5,
            FeedXyMmMin = 3000,
            FeedZMmMin = 500,
            SpindleRpm = 18000,
            ToolDiameterMm = 12,
        },
        new()
        {
            Id = "nesting_router_6",
            Name = "Nesting router Ø6",
            Dialect = "generic",
            ProgramEnd = "M2",
            SafeZMm = 8,
            FeedXyMmMin = 4000,
            FeedZMmMin = 800,
            SpindleRpm = 18000,
            ToolDiameterMm = 6,
        },
        new()
        {
            Id = "fanuc_like_m30",
            Name = "Fanuc-like (M30 end)",
            Dialect = "fanuc_like",
            ProgramEnd = "M30",
            SafeZMm = 10,
            FeedXyMmMin = 2500,
            FeedZMmMin = 400,
            SpindleRpm = 16000,
            ToolDiameterMm = 10,
        },
    ];

    public static MachineProfile Get(string? id) =>
        All.FirstOrDefault(p => p.Id == id) ?? All[1];
}
