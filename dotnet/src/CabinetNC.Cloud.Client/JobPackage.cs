namespace CabinetNC.Cloud.Client;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CabinetNC.Cloud.Contracts;

public sealed record JobPackageEntry(
    string SourcePath,
    string PackagePath,
    JobFileRole Role,
    int? SheetIndex = null);

/// <summary>Write / verify the job zip that travels Omni → cloud → OmniLight.</summary>
public static class JobPackage
{
    public static JobManifest Write(JobManifest manifest, IEnumerable<JobPackageEntry> entries, string zipPath)
    {
        var list = entries.ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in list)
        {
            ValidatePackagePath(e.PackagePath);
            if (!seen.Add(e.PackagePath))
                throw new ArgumentException($"Duplicate package path: {e.PackagePath}");
            if (!File.Exists(e.SourcePath))
                throw new FileNotFoundException("Job file missing", e.SourcePath);
        }

        manifest.Files = list.Select(e => new JobFile
        {
            Path = e.PackagePath,
            Role = e.Role,
            SheetIndex = e.SheetIndex,
            SizeBytes = new FileInfo(e.SourcePath).Length,
            Sha256 = HashFile(e.SourcePath),
        }).ToList();

        var dir = Path.GetDirectoryName(Path.GetFullPath(zipPath))!;
        Directory.CreateDirectory(dir);
        var tmp = zipPath + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            foreach (var e in list)
                zip.CreateEntryFromFile(e.SourcePath, e.PackagePath, CompressionLevel.Optimal);
            var entry = zip.CreateEntry(JobManifest.FileName, CompressionLevel.Optimal);
            using var s = entry.Open();
            JsonSerializer.Serialize(s, manifest, CloudJson.Options);
        }
        File.Move(tmp, zipPath, overwrite: true);
        return manifest;
    }

    public static JobManifest ReadManifest(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        return ReadManifest(zip);
    }

    /// <summary>Throws <see cref="InvalidDataException"/> unless every listed file is present with a matching SHA-256.</summary>
    public static JobManifest Verify(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var manifest = ReadManifest(zip);
        var problems = new List<string>();
        foreach (var f in manifest.Files)
        {
            var entry = zip.GetEntry(f.Path);
            if (entry is null)
            {
                problems.Add($"missing {f.Path}");
                continue;
            }
            using var s = entry.Open();
            var actual = Convert.ToHexStringLower(SHA256.HashData(s));
            if (!string.Equals(actual, f.Sha256, StringComparison.OrdinalIgnoreCase))
                problems.Add($"checksum mismatch {f.Path}");
        }
        if (problems.Count > 0)
            throw new InvalidDataException("Job package failed verification: " + string.Join("; ", problems));
        return manifest;
    }

    internal static JobManifest ReadManifest(ZipArchive zip)
    {
        var entry = zip.GetEntry(JobManifest.FileName)
            ?? throw new InvalidDataException($"Job package has no {JobManifest.FileName}");
        using var s = entry.Open();
        var manifest = JsonSerializer.Deserialize<JobManifest>(s, CloudJson.Options)
            ?? throw new InvalidDataException("Empty job manifest");
        if (manifest.Schema != JobManifest.SchemaName)
            throw new InvalidDataException($"Not an Omni job package (schema '{manifest.Schema}')");
        if (manifest.SchemaVersion > JobManifest.CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Job package schema v{manifest.SchemaVersion} is newer than this app (v{JobManifest.CurrentSchemaVersion}) — update OmniLight");
        foreach (var f in manifest.Files)
            ValidatePackagePath(f.Path);
        return manifest;
    }

    internal static void ValidatePackagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.Contains('\\')
            || path.StartsWith('/')
            || path.Contains(':')
            || path.Split('/').Any(seg => seg is "" or "." or ".."))
            throw new InvalidDataException($"Invalid package path: '{path}'");
        if (path.Equals(JobManifest.FileName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"'{JobManifest.FileName}' is reserved");
    }

    static string HashFile(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }
}
