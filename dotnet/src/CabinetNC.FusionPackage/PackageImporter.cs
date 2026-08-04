namespace CabinetNC.FusionPackage;

/// <summary>
/// Opens on-disk packages. Primary: <c>cabinetnc.woodjob</c> (folder/.zip).
/// Legacy: single-file <c>cabinetnc.cut-package</c> JSON.
/// </summary>
public static class PackageImporter
{
    public static PackageImportResult FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new PackageImportResult
            {
                Ok = false,
                Errors = [new ValidationIssue("path", "$", "empty path")],
            };

        if (Directory.Exists(path) || WoodJobImporter.LooksLikeWoodJobZip(path))
            return WoodJobImporter.FromPath(path);

        if (File.Exists(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return CutPackageImporter.FromFile(path);

        if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return WoodJobImporter.FromZip(path);

        return new PackageImportResult
        {
            Ok = false,
            Errors =
            [
                new ValidationIssue(
                    "path",
                    path,
                    "unsupported package — use woodjob folder/.zip or cut-package .json"),
            ],
        };
    }
}
