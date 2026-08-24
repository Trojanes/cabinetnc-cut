namespace CabinetNC.Domain.Manufacturing;

using CabinetNC.Domain.Materials;
using CabinetNC.Domain.Parts;

public sealed class NcReverseOptions
{
    public double ThicknessMm { get; init; } = 18;
    public double SheetWidthMm { get; init; } = 1220;
    public double SheetLengthMm { get; init; } = 2440;
    public IReadOnlyDictionary<string, ToolDefinition>? Tools { get; init; }
}

public sealed class NcReverseResult
{
    public IReadOnlyList<OsaiLine> Lines { get; init; } = [];
    public IReadOnlyList<ToolStroke> Strokes { get; init; } = [];
    public IReadOnlyList<CutOp> Ops { get; init; } = [];
    public IReadOnlyList<Panel> Panels { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public double ThicknessMm { get; init; } = 18;
    public double SafeZMm { get; init; } = TroyRecipe.SafeZMm;
}

/// <summary>OSAI-Troy .anc → strokes → CutOps → panels (tool offset removed).</summary>
public static class NcReverse
{
    public static NcReverseResult FromText(string nc, NcReverseOptions? options = null)
    {
        var opt = options ?? new NcReverseOptions();
        var replay = OsaiTroyParser.Replay(nc);
        var ops = NcProcessInfer.Infer(replay, opt.ThicknessMm);
        var panels = NcToPanels.Recover(ops, opt.Tools);
        var warnings = new List<string>();
        if (replay.Strokes.Count == 0)
            warnings.Add("no_motion");
        if (ops.Count(o => o.Op == "contour") == 0)
            warnings.Add("no_contour");
        if (panels.Count == 0)
            warnings.Add("no_panel");
        return new NcReverseResult
        {
            Lines = replay.Lines,
            Strokes = replay.Strokes,
            Ops = ops,
            Panels = panels,
            Warnings = warnings,
            ThicknessMm = opt.ThicknessMm,
            SafeZMm = replay.SafeZMm,
        };
    }

    public static CutPackage ToPackage(NcReverseResult result, string? jobId = null)
    {
        var th = result.ThicknessMm > 0 ? result.ThicknessMm : 18;
        return new CutPackage
        {
            SchemaName = CutPackage.Schema,
            Version = CutPackage.SchemaVersion,
            JobId = string.IsNullOrWhiteSpace(jobId) ? "nc-recut" : jobId,
            Sheets =
            [
                new SheetStock
                {
                    SheetId = "S1",
                    ThicknessMm = th,
                    WidthMm = 1220,
                    LengthMm = 2440,
                },
            ],
            Panels = result.Panels,
        };
    }
}
