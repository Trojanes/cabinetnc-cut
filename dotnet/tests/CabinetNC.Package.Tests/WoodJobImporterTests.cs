using CabinetNC.Domain;
using CabinetNC.FusionPackage;

namespace CabinetNC.Package.Tests;

public class WoodJobImporterTests
{
    static string FixtureZip()
    {
        var walk = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            var p = Path.Combine(walk, "Fixtures", "demo_woodjob_120.zip");
            if (File.Exists(p)) return p;
            var alt = Path.GetFullPath(Path.Combine(walk, "..", "..", "..", "Fixtures", "demo_woodjob_120.zip"));
            if (File.Exists(alt)) return alt;
            var samples = Path.GetFullPath(Path.Combine(walk, "..", "..", "..", "..", "..", "public", "samples", "demo_woodjob_120.zip"));
            if (File.Exists(samples)) return samples;
            var parent = Directory.GetParent(walk);
            if (parent is null) break;
            walk = parent.FullName;
        }
        throw new FileNotFoundException("demo_woodjob_120.zip not found");
    }

    [Fact]
    public void Imports_demo_woodjob_zip()
    {
        var result = PackageImporter.FromPath(FixtureZip());
        Assert.True(result.Ok, string.Join("; ", result.Errors.Select(e => $"{e.Path}:{e.Message}")));
        Assert.NotNull(result.Package);
        Assert.Equal(CutPackage.WoodJobFormat, result.Package!.SchemaName);
        Assert.Equal(120, result.Package.Panels.Count);
        Assert.Equal(4, result.Package.Sheets.Count);
        Assert.Contains(result.Package.Panels, p => p.Features.Any(f => f.Kind == "holeVertical"));
        Assert.Contains(result.Package.Panels, p => p.Features.Any(f => f.Kind == "grooveVertical"));
        Assert.Contains(result.Package.Panels, p => p.Features.Any(f => f.Kind == "throughCutout" && f.Path is { Count: >= 3 }));
        Assert.True(result.Package.Sheets[0].WidthMm > 0);
        Assert.True(result.Package.Sheets[0].LengthMm > 0);
        Assert.True(result.Package.Sheets[0].KerfMm > 0);
        var grainLocked = result.Package.Panels.First(p => p.GrainDirection == "Y");
        Assert.False(grainLocked.MayRotate90);
    }

    [Fact]
    public void Roundtrips_to_cut_package_json()
    {
        var imported = PackageImporter.FromPath(FixtureZip());
        Assert.True(imported.Ok);
        var json = CutPackageJson.Serialize(imported.Package!);
        var again = CutPackageImporter.FromJson(json);
        Assert.True(again.Ok, string.Join("; ", again.Errors.Select(e => e.Message)));
        Assert.Equal(120, again.Package!.Panels.Count);
    }
}
