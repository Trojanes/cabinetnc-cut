namespace CabinetNC.Infrastructure.Library;

using System.Text.Json;
using CabinetNC.Domain.Machines;

public static class WorkshopLibraryStore
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string DefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CabinetNC");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "library.json");
    }

    public static WorkshopLibrary Load(string? path = null)
    {
        path ??= DefaultPath();
        try
        {
            if (File.Exists(path))
            {
                var doc = JsonSerializer.Deserialize<WorkshopLibrary>(File.ReadAllText(path), JsonOpts);
                if (doc is not null && doc.SchemaName == WorkshopLibrary.Schema)
                    return EnsureDefaults(doc);
            }
        }
        catch
        {
            /* corrupt → defaults */
        }
        return EnsureDefaults(CreateDefault());
    }

    public static void Save(WorkshopLibrary lib, string? path = null)
    {
        path ??= DefaultPath();
        lib.SchemaName = WorkshopLibrary.Schema;
        lib.Version = WorkshopLibrary.SchemaVersion;
        lib.SavedAt = DateTimeOffset.UtcNow.ToString("o");
        File.WriteAllText(path, JsonSerializer.Serialize(lib, JsonOpts));
    }

    public static WorkshopLibrary CreateDefault()
    {
        var lib = new WorkshopLibrary
        {
            Materials =
            [
                new() { Id = "mat_oak", Name = "oak", ThicknessMm = 18, DensityHint = "板式" },
                new() { Id = "mat_mdf", Name = "mdf", ThicknessMm = 18, DensityHint = "板式" },
                new() { Id = "mat_ply", Name = "plywood", ThicknessMm = 15, DensityHint = "多层" },
            ],
            Tools = MachineCatalog.All.Select(p => new LibTool
            {
                Id = "tool_" + p.Id,
                Name = p.Name,
                MachineId = p.Id,
                DiameterMm = p.ToolDiameterMm,
                FeedXyMmMin = p.FeedXyMmMin,
                FeedZMmMin = p.FeedZMmMin,
                SpindleRpm = p.SpindleRpm,
            }).ToList(),
            Nest = new NestDefaults(),
        };
        return lib;
    }

    static WorkshopLibrary EnsureDefaults(WorkshopLibrary lib)
    {
        lib.Materials ??= [];
        lib.Tools ??= [];
        lib.Remnants ??= [];
        lib.Nest ??= new NestDefaults();
        if (lib.Materials.Count == 0 || lib.Tools.Count == 0)
        {
            var d = CreateDefault();
            if (lib.Materials.Count == 0) lib.Materials = d.Materials;
            if (lib.Tools.Count == 0) lib.Tools = d.Tools;
        }
        return lib;
    }
}
