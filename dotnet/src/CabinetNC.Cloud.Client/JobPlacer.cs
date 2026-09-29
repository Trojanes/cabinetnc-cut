namespace CabinetNC.Cloud.Client;

using System.IO.Compression;
using CabinetNC.Cloud.Contracts;

/// <summary>Machine-PC folders, configured on the OmniLight side only.</summary>
public sealed class PlacementTargets
{
    public required string CutDirectory { get; init; }
    public required string LabelDirectory { get; init; }
}

public sealed record PlacedFile(string PackagePath, JobFileRole Role, string Destination);

/// <summary>
/// Drop a verified job zip into the machine folders. Files are flattened to the
/// folder root (labelers resolve <c>{folder}\{stem}.bmp</c>), written to a temp
/// name first so the controller never sees a half-written program, and only
/// same-named files are overwritten — other jobs in the folder are left alone.
/// </summary>
public static class JobPlacer
{
    const string TempSuffix = ".omni-tmp";

    public static IReadOnlyList<PlacedFile> Place(string zipPath, PlacementTargets targets)
    {
        var manifest = JobPackage.Verify(zipPath);

        var plan = new List<(JobFile File, string Destination)>();
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in manifest.Files)
        {
            var dir = f.Role switch
            {
                JobFileRole.Cut => targets.CutDirectory,
                JobFileRole.Label => targets.LabelDirectory,
                _ => null,
            };
            if (dir is null) continue;
            if (string.IsNullOrWhiteSpace(dir))
                throw new InvalidOperationException($"No target folder configured for {f.Role} files");

            var name = f.Path[(f.Path.LastIndexOf('/') + 1)..];
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException($"File name not valid on this PC: '{name}'");
            var dest = Path.Combine(dir, name);
            if (!destinations.Add(Path.GetFullPath(dest)))
                throw new InvalidDataException($"Two job files would land on the same path: {dest}");
            plan.Add((f, dest));
        }

        using var zip = ZipFile.OpenRead(zipPath);
        var placed = new List<PlacedFile>(plan.Count);
        foreach (var (f, dest) in plan)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            var tmp = dest + TempSuffix;
            zip.GetEntry(f.Path)!.ExtractToFile(tmp, overwrite: true);
            File.Move(tmp, dest, overwrite: true);
            placed.Add(new PlacedFile(f.Path, f.Role, dest));
        }
        return placed;
    }
}
