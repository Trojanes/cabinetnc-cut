namespace CabinetNC.Application.Projects;

using CabinetNC.Domain;
using CabinetNC.Domain.Parts;
using CabinetNC.FusionPackage;

public sealed class ProjectSession
{
    public CutPackage? Package { get; private set; }
    public string? SourcePath { get; private set; }
    public string? PackageJson { get; private set; }
    public string? ProjectDbPath { get; private set; }
    public string MachineId { get; set; } = "nesting_router_6";
    public IReadOnlyList<ValidationIssue> LastWarnings { get; private set; } = [];
    public IReadOnlyList<ValidationIssue> LastErrors { get; private set; } = [];

    public PackageImportResult OpenPackageFile(string path)
    {
        var result = PackageImporter.FromPath(path);
        LastWarnings = result.Warnings;
        LastErrors = result.Errors;
        if (result.Ok && result.Package is not null)
        {
            Package = result.Package;
            // ponytail: project.db still stores flat cut-package JSON; woodjob zip stays on SourcePath.
            PackageJson = CutPackageJson.Serialize(result.Package);
            SourcePath = path;
            ProjectDbPath = null;
        }
        return result;
    }

    public PackageImportResult OpenPackageJson(string json, string? sourceLabel = null)
    {
        var result = CutPackageImporter.FromJson(json);
        LastWarnings = result.Warnings;
        LastErrors = result.Errors;
        if (result.Ok && result.Package is not null)
        {
            Package = result.Package;
            PackageJson = json;
            SourcePath = sourceLabel;
        }
        return result;
    }

    public void SetProjectDbPath(string? path) => ProjectDbPath = path;

    public void ReplacePanel(Panel panel)
    {
        if (Package is null) return;
        Package = Package.WithPanel(panel);
        // ponytail: PackageJson stays stale until Save; geom edits are in-memory until project save.
    }

    public void Clear()
    {
        Package = null;
        SourcePath = null;
        PackageJson = null;
        ProjectDbPath = null;
        LastWarnings = [];
        LastErrors = [];
    }
}
