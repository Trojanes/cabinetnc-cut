using CabinetNC.Domain;
using CabinetNC.FusionPackage;

namespace CabinetNC.Package.Tests;

public class CutPackageImporterTests
{
    static string DemoPath()
    {
        var walk = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            var p = Path.Combine(walk, "public", "samples", "demo_cut_package.json");
            if (File.Exists(p)) return p;
            // from tests/bin/Debug/net10.0 鈫?repo root
            var alt = Path.GetFullPath(Path.Combine(walk, "..", "..", "..", "..", "..", "public", "samples", "demo_cut_package.json"));
            if (File.Exists(alt)) return alt;
            var parent = Directory.GetParent(walk);
            if (parent is null) break;
            walk = parent.FullName;
        }
        throw new FileNotFoundException("demo_cut_package.json not found");
    }

    [Fact]
    public void Imports_demo_cut_package()
    {
        var result = CutPackageImporter.FromFile(DemoPath());
        Assert.True(result.Ok, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.NotNull(result.Package);
        Assert.Equal(CutPackage.Schema, result.Package!.SchemaName);
        Assert.True(result.Package.Panels.Count >= 1);
        Assert.True(result.Package.Panels[0].Outline.Points.Count >= 3);
    }

    [Fact]
    public void Rejects_empty_panels()
    {
        var json = """{"schema":"cabinetnc.cut-package","schemaVersion":1,"panels":[]}""";
        var result = CutPackageImporter.FromJson(json);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Code == "panels_empty");
    }
}

