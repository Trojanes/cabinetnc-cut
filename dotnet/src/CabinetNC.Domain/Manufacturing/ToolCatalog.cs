namespace CabinetNC.Domain.Manufacturing;

/// <summary>Shop tool preset (Day 7). Domain-owned so CAM does not depend on Desktop library JSON.</summary>
public sealed class ToolDefinition
{
    public required string ToolId { get; init; }
    public required string Name { get; init; }
    public double DiameterMm { get; init; }
    public double FeedXyMmMin { get; init; } = 3000;
    public double FeedZMmMin { get; init; } = 500;
    public double SpindleRpm { get; init; } = 18000;
    /// <summary>contour | drill | groove | pocket | any</summary>
    public string Role { get; init; } = "any";
}

public static class ToolCatalog
{
    /// <summary>ASSUMED RC presets — shop may renumber later.</summary>
    public static IReadOnlyList<ToolDefinition> DefaultPresets { get; } =
    [
        new()
        {
            ToolId = "T1", Name = "6.35 Router", DiameterMm = 6.35,
            Role = "contour", FeedXyMmMin = 4500, FeedZMmMin = 800, SpindleRpm = 18000,
        },
        new()
        {
            ToolId = "T2", Name = "10 Router", DiameterMm = 10,
            Role = "groove", FeedXyMmMin = 3500, FeedZMmMin = 600, SpindleRpm = 16000,
        },
        new()
        {
            ToolId = "T3", Name = "3 Drill", DiameterMm = 3,
            Role = "drill", FeedXyMmMin = 1200, FeedZMmMin = 400, SpindleRpm = 6000,
        },
    ];

    public static Dictionary<string, ToolDefinition> DefaultMap() =>
        DefaultPresets.ToDictionary(t => t.ToolId, t => t, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Binds each CutOp to a ToolId from role defaults or explicit map.</summary>
public static class ToolBinder
{
    /// <summary>op kind → preferred ToolId.</summary>
    public static IReadOnlyDictionary<string, string> DefaultRoleMap { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["contour"] = "T2",
            ["pocket"] = "T2",
            ["groove"] = "T2",
            ["drill"] = "T3",
        };

    public static CutOp Bind(CutOp op, IReadOnlyDictionary<string, string>? roleMap = null)
    {
        if (!string.IsNullOrWhiteSpace(op.ToolId)) return op;
        if (op.IsTongue) return op with { ToolId = "T1" };
        var map = roleMap ?? DefaultRoleMap;
        if (!map.TryGetValue(op.Op, out var toolId))
            return op;
        return op with { ToolId = toolId };
    }

    public static IReadOnlyList<CutOp> BindAll(
        IEnumerable<CutOp> ops,
        IReadOnlyDictionary<string, string>? roleMap = null) =>
        ops.Select(o => Bind(o, roleMap)).ToList();

    public static IReadOnlyList<string> MissingToolIds(IEnumerable<CutOp> ops) =>
        ops.Where(o => o.Enabled && string.IsNullOrWhiteSpace(o.ToolId))
            .Select(o => $"{o.Op}:{o.PanelId}:{o.FeatureId ?? "-"}")
            .Distinct()
            .ToList();
}
