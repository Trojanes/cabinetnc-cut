namespace CabinetNC.Domain.Manufacturing;

using System.Text;
using CabinetNC.Domain;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

public sealed class WorkpieceLabel
{
    public required string WorkpieceId { get; init; }
    public string? ProjectId { get; init; }
    public string? ModuleId { get; init; }
    public string? Material { get; init; }
    public double ThicknessMm { get; init; }
    public int? SheetIndex { get; init; }
    public string? Side { get; init; }
    public string? EdgeBandingSummary { get; init; }
    public string? PanelId { get; init; }
}

/// <summary>Labels + BOM CSV sharing WorkpieceId with sheet manifests (Day 12).</summary>
public static class LabelBomBuilder
{
    public static IReadOnlyList<WorkpieceLabel> BuildLabels(
        CutPackage package,
        IReadOnlyList<NestPlacement> placements)
    {
        var placeByPanel = placements
            .GroupBy(p => p.PanelId)
            .ToDictionary(g => g.Key, g => g.First().SheetIndex);
        var labels = new List<WorkpieceLabel>();
        foreach (var panel in package.Panels)
        {
            var wid = panel.Identity?.WorkpieceId ?? panel.PanelId;
            placeByPanel.TryGetValue(panel.PanelId, out var sheet);
            var edge = panel.EdgeBanding;
            var edgeSummary = edge is null
                ? null
                : $"F={edge.Front ?? "-"} B={edge.Back ?? "-"} L={edge.Left ?? "-"} R={edge.Right ?? "-"}";
            labels.Add(new WorkpieceLabel
            {
                WorkpieceId = wid,
                ProjectId = panel.Identity?.ProjectId,
                ModuleId = panel.Identity?.ModuleId,
                Material = panel.Material,
                ThicknessMm = panel.ThicknessMm,
                SheetIndex = placeByPanel.ContainsKey(panel.PanelId) ? sheet : null,
                Side = panel.Side ?? panel.Orientation?.MillingFace,
                EdgeBandingSummary = edgeSummary,
                PanelId = panel.PanelId,
            });
        }
        return labels;
    }

    public static string ToCsv(IReadOnlyList<WorkpieceLabel> labels, IReadOnlyList<CutOp>? ops = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("workpieceId,panelId,projectId,moduleId,material,thicknessMm,sheet,side,edge,qty,tools");
        var toolsByPanel = (ops ?? [])
            .Where(o => o.Placed)
            .GroupBy(o => o.PanelId)
            .ToDictionary(
                g => g.Key,
                g => string.Join("|", g.Select(o => o.ToolId ?? "?").Distinct().OrderBy(x => x)));
        foreach (var l in labels)
        {
            toolsByPanel.TryGetValue(l.PanelId ?? "", out var tools);
            sb.Append(Csv(l.WorkpieceId)).Append(',')
                .Append(Csv(l.PanelId)).Append(',')
                .Append(Csv(l.ProjectId)).Append(',')
                .Append(Csv(l.ModuleId)).Append(',')
                .Append(Csv(l.Material)).Append(',')
                .Append(l.ThicknessMm.ToString("0.###")).Append(',')
                .Append(l.SheetIndex is int s ? (s + 1).ToString() : "").Append(',')
                .Append(Csv(l.Side)).Append(',')
                .Append(Csv(l.EdgeBandingSummary)).Append(',')
                .Append('1').Append(',')
                .Append(Csv(tools))
                .AppendLine();
        }
        return sb.ToString();
    }

    public static string ToLabelsHtml(IReadOnlyList<WorkpieceLabel> labels)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>labels</title>");
        sb.AppendLine("<style>body{font:12px/1.3 sans-serif}.label{border:1px solid #333;padding:8px;margin:8px;width:220px;display:inline-block;vertical-align:top}.id{font-size:16px;font-weight:700}</style></head><body>");
        foreach (var l in labels)
        {
            sb.Append("<div class=\"label\">");
            sb.Append($"<div class=\"id\">{Esc(l.WorkpieceId)}</div>");
            sb.Append($"<div>{Esc(l.ProjectId)} / {Esc(l.ModuleId)}</div>");
            sb.Append($"<div>{Esc(l.Material)} · {l.ThicknessMm:0.##} mm</div>");
            sb.Append($"<div>Sheet {(l.SheetIndex is int s ? s + 1 : "-")} · Side {Esc(l.Side ?? "-")}</div>");
            if (!string.IsNullOrWhiteSpace(l.EdgeBandingSummary))
                sb.Append($"<div>{Esc(l.EdgeBandingSummary)}</div>");
            sb.Append("</div>");
        }
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    static string Csv(string? v)
    {
        v ??= "";
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    static string Esc(string? v) =>
        (v ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
