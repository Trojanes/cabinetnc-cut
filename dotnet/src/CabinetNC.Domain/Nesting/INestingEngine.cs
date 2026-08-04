namespace CabinetNC.Domain.Nesting;

using CabinetNC.Domain.Parts;
using System.Diagnostics;

/// <summary>Pluggable nesting engine. RC authority remains BLF (AABB), not NFP.</summary>
public interface INestingEngine
{
    string Name { get; }
    NestResult Pack(
        IReadOnlyList<Panel> panels,
        NestSettings settings,
        IReadOnlyList<NestSheetSpec> stockTemplates,
        Func<Panel, (double w, double h)> sizeOf,
        CancellationToken ct = default);
}

public sealed class BlfNestingEngine : INestingEngine
{
    public string Name => "grouped_blf_v0";

    public NestResult Pack(
        IReadOnlyList<Panel> panels,
        NestSettings settings,
        IReadOnlyList<NestSheetSpec> stockTemplates,
        Func<Panel, (double w, double h)> sizeOf,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return GroupedBlfNester.Pack(panels, settings, stockTemplates, sizeOf);
    }
}

/// <summary>
/// Advanced engine prototype — intentionally fails / times out so fallback path is testable.
/// Does NOT claim NFP. Part-in-part model is placeholder only.
/// </summary>
public sealed class AdvancedNestingEngineStub : INestingEngine
{
    public string Name => "advanced_stub_v0";
    public bool AlwaysFail { get; init; } = true;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMilliseconds(1);

    public NestResult Pack(
        IReadOnlyList<Panel> panels,
        NestSettings settings,
        IReadOnlyList<NestSheetSpec> stockTemplates,
        Func<Panel, (double w, double h)> sizeOf,
        CancellationToken ct = default)
    {
        if (AlwaysFail)
            throw new InvalidOperationException("advanced_stub: not implemented (no NFP)");
        // Simulate timeout path
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(Timeout);
        linked.Token.ThrowIfCancellationRequested();
        throw new TimeoutException("advanced_stub: timeout");
    }
}

public sealed class NestEngineRequest
{
    public required IReadOnlyList<Panel> Panels { get; init; }
    public required NestSettings Settings { get; init; }
    public required IReadOnlyList<NestSheetSpec> StockTemplates { get; init; }
    public required Func<Panel, (double w, double h)> SizeOf { get; init; }
    /// <summary>preferred | blf | advanced</summary>
    public string EnginePreference { get; init; } = "preferred";
    public TimeSpan AdvancedTimeout { get; init; } = TimeSpan.FromSeconds(2);
}

public sealed class NestEngineRunLog
{
    public required string SelectedEngine { get; init; }
    public string? AttemptedEngine { get; init; }
    public string? FallbackReason { get; init; }
    public long ElapsedMs { get; init; }
    public double? UtilizationHintPct { get; init; }
}

/// <summary>Runs preferred engine with automatic BLF fallback.</summary>
public sealed class NestEngineRouter
{
    readonly INestingEngine _blf;
    readonly INestingEngine _advanced;

    public NestEngineRouter(INestingEngine? blf = null, INestingEngine? advanced = null)
    {
        _blf = blf ?? new BlfNestingEngine();
        _advanced = advanced ?? new AdvancedNestingEngineStub();
    }

    public (NestResult Result, NestEngineRunLog Log) Run(NestEngineRequest req, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var pref = (req.EnginePreference ?? "preferred").Trim().ToLowerInvariant();

        if (pref is "blf" or "grouped_blf" or "grouped_blf_v0")
        {
            var r = _blf.Pack(req.Panels, req.Settings, req.StockTemplates, req.SizeOf, ct);
            sw.Stop();
            return (TagEngine(r, _blf.Name), new NestEngineRunLog
            {
                SelectedEngine = _blf.Name,
                AttemptedEngine = _blf.Name,
                ElapsedMs = sw.ElapsedMilliseconds,
                UtilizationHintPct = UtilHint(r),
            });
        }

        if (pref is "advanced" or "preferred")
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(req.AdvancedTimeout);
                var adv = _advanced.Pack(req.Panels, req.Settings, req.StockTemplates, req.SizeOf, cts.Token);
                sw.Stop();
                return (TagEngine(adv, _advanced.Name), new NestEngineRunLog
                {
                    SelectedEngine = _advanced.Name,
                    AttemptedEngine = _advanced.Name,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    UtilizationHintPct = UtilHint(adv),
                });
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or OperationCanceledException)
            {
                var fallback = _blf.Pack(req.Panels, req.Settings, req.StockTemplates, req.SizeOf, ct);
                sw.Stop();
                var tagged = TagEngine(fallback, "blf_fallback");
                return (tagged, new NestEngineRunLog
                {
                    SelectedEngine = "blf_fallback",
                    AttemptedEngine = _advanced.Name,
                    FallbackReason = ex.GetType().Name + ": " + ex.Message,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    UtilizationHintPct = UtilHint(tagged),
                });
            }
        }

        // Unknown preference → BLF
        var def = _blf.Pack(req.Panels, req.Settings, req.StockTemplates, req.SizeOf, ct);
        sw.Stop();
        return (TagEngine(def, _blf.Name), new NestEngineRunLog
        {
            SelectedEngine = _blf.Name,
            AttemptedEngine = pref,
            FallbackReason = "unknown_preference",
            ElapsedMs = sw.ElapsedMilliseconds,
            UtilizationHintPct = UtilHint(def),
        });
    }

    static NestResult TagEngine(NestResult r, string engine) =>
        new()
        {
            Engine = engine,
            Placements = r.Placements,
            SheetCount = r.SheetCount,
            Unplaced = r.Unplaced,
            UnplacedReasons = r.UnplacedReasons,
            GroupReports = r.GroupReports,
            SheetsUsed = r.SheetsUsed,
        };

    static double? UtilHint(NestResult r)
    {
        if (r.GroupReports.Count == 0) return null;
        return r.GroupReports.Average(g => g.UtilizationPct);
    }
}

/// <summary>Part-in-part placeholder (not enabled in RC packer).</summary>
public sealed class PartInPartSlot
{
    public required string HostPanelId { get; init; }
    public required string ChildPanelId { get; init; }
    public bool Enabled { get; init; }
}
