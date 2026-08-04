namespace CabinetNC.Domain;

using CabinetNC.Domain.Materials;
using CabinetNC.Domain.Parts;

/// <summary>
/// Runtime package model. On-disk primary input is <c>cabinetnc.woodjob</c>;
/// legacy <c>cabinetnc.cut-package</c> still imports into this shape.
/// </summary>
public sealed class CutPackage
{
    public const string Schema = "cabinetnc.cut-package";
    public const int SchemaVersion = 1;
    public const string WoodJobFormat = "cabinetnc.woodjob";
    public const int WoodJobSchemaVersion = 2;

    public required string SchemaName { get; init; }
    public int Version { get; init; } = SchemaVersion;
    public string? JobId { get; init; }
    public string Units { get; init; } = "mm";
    public IReadOnlyList<SheetStock> Sheets { get; init; } = [];
    public required IReadOnlyList<Panel> Panels { get; init; }

    public CutPackage WithPanel(Panel panel)
    {
        var list = Panels.ToList();
        var i = list.FindIndex(p => p.PanelId == panel.PanelId);
        if (i >= 0) list[i] = panel;
        else list.Add(panel);
        return new CutPackage
        {
            SchemaName = SchemaName,
            Version = Version,
            JobId = JobId,
            Units = Units,
            Sheets = Sheets,
            Panels = list,
        };
    }
}
