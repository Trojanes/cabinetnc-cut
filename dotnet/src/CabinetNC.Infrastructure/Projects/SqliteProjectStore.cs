using System.Text.Json;
using CabinetNC.Domain;
using CabinetNC.FusionPackage;
using Microsoft.Data.Sqlite;

namespace CabinetNC.Infrastructure.Projects;

/// <summary>
/// Local project DB: one file `project.db` holding package JSON + nest + machine.
/// ponytail: single-table store — upgrade to revision history later.
/// </summary>
public sealed class SqliteProjectStore
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string DbPathForFolder(string projectFolder) =>
        Path.Combine(projectFolder, "project.db");

    public void EnsureSchema(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS project (
              id INTEGER PRIMARY KEY CHECK (id = 1),
              name TEXT NOT NULL,
              package_json TEXT NOT NULL,
              machine_id TEXT NOT NULL,
              nest_json TEXT,
              nc_text TEXT,
              updated_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Save(string dbPath, ProjectDocument doc)
    {
        EnsureSchema(dbPath);
        using var conn = Open(dbPath);
        using var tx = conn.BeginTransaction();
        using (var clear = conn.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM project WHERE id = 1;";
            clear.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                """
                INSERT INTO project (id, name, package_json, machine_id, nest_json, nc_text, updated_at)
                VALUES (1, $name, $pkg, $machine, $nest, $nc, $updated);
                """;
            cmd.Parameters.AddWithValue("$name", doc.Name);
            cmd.Parameters.AddWithValue("$pkg", doc.PackageJson);
            cmd.Parameters.AddWithValue("$machine", doc.MachineId);
            cmd.Parameters.AddWithValue("$nest", (object?)doc.NestPlacementsJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$nc", (object?)doc.NcText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$updated", doc.UpdatedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public ProjectDocument? Load(string dbPath)
    {
        if (!File.Exists(dbPath)) return null;
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT name, package_json, machine_id, nest_json, nc_text, updated_at FROM project WHERE id = 1;";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new ProjectDocument
        {
            Name = reader.GetString(0),
            PackageJson = reader.GetString(1),
            MachineId = reader.GetString(2),
            NestPlacementsJson = reader.IsDBNull(3) ? null : reader.GetString(3),
            NcText = reader.IsDBNull(4) ? null : reader.GetString(4),
            UpdatedAt = DateTimeOffset.TryParse(reader.GetString(5), out var t) ? t : DateTimeOffset.UtcNow,
        };
    }

    public static string SerializeNest(IEnumerable<NestPlacementDto> placements) =>
        JsonSerializer.Serialize(placements.ToList(), JsonOpts);

    public static IReadOnlyList<NestPlacementDto> DeserializeNest(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<NestPlacementDto>>(json, JsonOpts) ?? [];
    }

    public static PackageImportResult ImportPackage(ProjectDocument doc) =>
        CutPackageImporter.FromJson(doc.PackageJson);

    static SqliteConnection Open(string dbPath)
    {
        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        return conn;
    }
}
