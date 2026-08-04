namespace CabinetNC.Domain.Manufacturing;

using System.Text.Json;
using CabinetNC.Domain;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Nesting;

/// <summary>Pluggable NC post (Day 10). RC dialects wrap NcEmitter.</summary>
public interface IPostProcessor
{
    string Id { get; }
    string Emit(IEnumerable<CutOp> ops, MachineProfile profile);
}

public sealed class GenericMmPostProcessor : IPostProcessor
{
    public string Id => "generic_mm";
    public string Emit(IEnumerable<CutOp> ops, MachineProfile profile)
    {
        var p = CloneProfile(profile, dialect: "generic", programEnd: profile.ProgramEnd);
        return NcEmitter.OpsToNc(ops, p);
    }

    internal static MachineProfile CloneProfile(MachineProfile profile, string dialect, string? programEnd) =>
        new()
        {
            Id = profile.Id,
            Name = profile.Name,
            Dialect = dialect,
            ProgramEnd = programEnd ?? profile.ProgramEnd,
            SafeZMm = profile.SafeZMm,
            FeedXyMmMin = profile.FeedXyMmMin,
            FeedZMmMin = profile.FeedZMmMin,
            SpindleRpm = profile.SpindleRpm,
            ToolDiameterMm = profile.ToolDiameterMm,
            ContourDepthMm = profile.ContourDepthMm,
            ContourStepdownMm = profile.ContourStepdownMm,
            DrillPeckMm = profile.DrillPeckMm,
            EnableContour = profile.EnableContour,
            EnableDrill = profile.EnableDrill,
            EnableGroove = profile.EnableGroove,
            OriginNote = profile.OriginNote,
        };
}

public sealed class FanucLikePostProcessor : IPostProcessor
{
    public string Id => "fanuc_like";
    public string Emit(IEnumerable<CutOp> ops, MachineProfile profile)
    {
        var p = GenericMmPostProcessor.CloneProfile(profile, "fanuc_like", "M30");
        return NcEmitter.OpsToNc(ops, p);
    }
}

public static class PostProcessorCatalog
{
    public static IPostProcessor Resolve(MachineProfile profile) =>
        profile.Dialect == "fanuc_like"
            ? new FanucLikePostProcessor()
            : new GenericMmPostProcessor();
}

public sealed class SheetArtifact
{
    public required int SheetIndex { get; init; }
    public required string NcFileName { get; init; }
    public required string DxfFileName { get; init; }
    public required string NcText { get; init; }
    public required string DxfText { get; init; }
    public required string ManifestJson { get; init; }
    public int OpCount { get; init; }
    public IReadOnlyList<string> PanelIds { get; init; } = [];
    public IReadOnlyList<string> ToolIds { get; init; } = [];
}

public sealed class ExportBundle
{
    public required string JobId { get; init; }
    public required string PostId { get; init; }
    public required IReadOnlyList<SheetArtifact> Sheets { get; init; }
    public required string RootManifestJson { get; init; }
    public string? JobSheetHtml { get; init; }
    public string? BomCsv { get; init; }
    public string? LabelsHtml { get; init; }
    public IReadOnlyList<WorkpieceLabel> Labels { get; init; } = [];
}

/// <summary>Per-sheet NC/DXF/manifest bundle (Day 10).</summary>
public static class SheetBundleBuilder
{
    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static ExportBundle Build(
        CutPackage package,
        IReadOnlyList<NestPlacement> placements,
        IReadOnlyList<CutOp> ops,
        MachineProfile profile,
        IPostProcessor? post = null,
        string? jobSheetHtml = null)
    {
        post ??= PostProcessorCatalog.Resolve(profile);
        var jobId = package.JobId ?? "job";
        var sheetIndexes = placements.Select(p => p.SheetIndex).Distinct().OrderBy(i => i).ToList();
        if (sheetIndexes.Count == 0 && ops.Any(o => o.Placed))
            sheetIndexes = ops.Where(o => o.Placed).Select(o => o.SheetIndex).Distinct().OrderBy(i => i).ToList();

        var sheets = new List<SheetArtifact>();
        foreach (var si in sheetIndexes)
        {
            var sheetOps = ops.Where(o => o.Placed && o.SheetIndex == si).ToList();
            var sheetPlaces = placements.Where(p => p.SheetIndex == si).ToList();
            var nc = post.Emit(sheetOps, profile);
            var dxf = NestDxfWriter.Write(package, placements, si);
            var panelIds = sheetPlaces.Select(p => p.PanelId).Distinct().OrderBy(x => x).ToList();
            var toolIds = sheetOps.Select(o => o.ToolId ?? "?").Distinct().OrderBy(x => x).ToList();
            var manifest = new
            {
                schema = "cabinetnc.sheet-manifest",
                schemaVersion = 1,
                jobId,
                sheetIndex = si,
                sheetLabel = $"S{si + 1}",
                post = post.Id,
                machineId = profile.Id,
                panelIds,
                toolIds,
                opCount = sheetOps.Count,
                files = new
                {
                    nc = $"{jobId}_S{si + 1}.nc",
                    dxf = $"{jobId}_S{si + 1}.dxf",
                },
            };
            sheets.Add(new SheetArtifact
            {
                SheetIndex = si,
                NcFileName = $"{jobId}_S{si + 1}.nc",
                DxfFileName = $"{jobId}_S{si + 1}.dxf",
                NcText = nc,
                DxfText = dxf,
                ManifestJson = JsonSerializer.Serialize(manifest, JsonOpts),
                OpCount = sheetOps.Count,
                PanelIds = panelIds,
                ToolIds = toolIds,
            });
        }

        var root = new
        {
            schema = "cabinetnc.export-bundle",
            schemaVersion = 1,
            jobId,
            post = post.Id,
            machineId = profile.Id,
            sheetCount = sheets.Count,
            sheets = sheets.Select(s => new
            {
                sheetIndex = s.SheetIndex,
                nc = s.NcFileName,
                dxf = s.DxfFileName,
                manifest = $"{jobId}_S{s.SheetIndex + 1}.manifest.json",
                opCount = s.OpCount,
                panelIds = s.PanelIds,
                toolIds = s.ToolIds,
            }),
        };

        var labels = LabelBomBuilder.BuildLabels(package, placements);
        var bom = LabelBomBuilder.ToCsv(labels, ops);
        var labelsHtml = LabelBomBuilder.ToLabelsHtml(labels);

        return new ExportBundle
        {
            JobId = jobId,
            PostId = post.Id,
            Sheets = sheets,
            RootManifestJson = JsonSerializer.Serialize(root, JsonOpts),
            JobSheetHtml = jobSheetHtml,
            BomCsv = bom,
            LabelsHtml = labelsHtml,
            Labels = labels,
        };
    }

    public static IReadOnlyList<string> WriteToDirectory(ExportBundle bundle, string directory)
    {
        Directory.CreateDirectory(directory);
        var written = new List<string>();
        foreach (var s in bundle.Sheets)
        {
            var ncPath = Path.Combine(directory, s.NcFileName);
            var dxfPath = Path.Combine(directory, s.DxfFileName);
            var manPath = Path.Combine(directory, $"{bundle.JobId}_S{s.SheetIndex + 1}.manifest.json");
            File.WriteAllText(ncPath, s.NcText);
            File.WriteAllText(dxfPath, s.DxfText);
            File.WriteAllText(manPath, s.ManifestJson);
            written.Add(ncPath);
            written.Add(dxfPath);
            written.Add(manPath);
        }
        var rootPath = Path.Combine(directory, $"{bundle.JobId}.bundle.json");
        File.WriteAllText(rootPath, bundle.RootManifestJson);
        written.Add(rootPath);
        if (!string.IsNullOrWhiteSpace(bundle.JobSheetHtml))
        {
            var htmlPath = Path.Combine(directory, $"{bundle.JobId}_sheet.html");
            File.WriteAllText(htmlPath, bundle.JobSheetHtml);
            written.Add(htmlPath);
        }
        if (!string.IsNullOrWhiteSpace(bundle.BomCsv))
        {
            var bomPath = Path.Combine(directory, $"{bundle.JobId}_bom.csv");
            File.WriteAllText(bomPath, bundle.BomCsv);
            written.Add(bomPath);
        }
        if (!string.IsNullOrWhiteSpace(bundle.LabelsHtml))
        {
            var labPath = Path.Combine(directory, $"{bundle.JobId}_labels.html");
            File.WriteAllText(labPath, bundle.LabelsHtml);
            written.Add(labPath);
        }
        return written;
    }
}
