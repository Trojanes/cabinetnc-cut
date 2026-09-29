using System.IO.Compression;
using CabinetNC.Cloud.Client;
using CabinetNC.Cloud.Contracts;

namespace CabinetNC.Cloud.Tests;

public class JobPlacerTests
{
    static PlacementTargets Targets(TempDir tmp) => new()
    {
        CutDirectory = Path.Combine(tmp.Path, "machine", "NC"),
        LabelDirectory = Path.Combine(tmp.Path, "machine", "Label"),
    };

    static string Package(TempDir tmp, params JobPackageEntry[] entries)
    {
        var zip = Path.Combine(tmp.Path, "job.zip");
        JobPackage.Write(new JobManifest { JobId = "j", ProjectName = "p" }, entries, zip);
        return zip;
    }

    [Fact]
    public void Places_by_role_and_flattens_labels()
    {
        using var tmp = new TempDir();
        var zip = Package(tmp,
            new(tmp.File("s/a.nc", "N1 M30"), "cut/sheet1/a.nc", JobFileRole.Cut),
            new(tmp.File("s/OHC_OH_D0_2.bmp", "BM"), "label/OHC_OH_D0_2.bmp", JobFileRole.Label),
            new(tmp.File("s/a.dxf", "0"), "preview/a.dxf", JobFileRole.Preview));
        var t = Targets(tmp);

        var placed = JobPlacer.Place(zip, t);

        Assert.Equal(2, placed.Count);
        Assert.Equal("N1 M30", File.ReadAllText(Path.Combine(t.CutDirectory, "a.nc")));
        Assert.True(File.Exists(Path.Combine(t.LabelDirectory, "OHC_OH_D0_2.bmp")));
        Assert.False(Directory.Exists(Path.Combine(t.LabelDirectory, "label")));
        Assert.False(File.Exists(Path.Combine(t.CutDirectory, "a.dxf")));
        Assert.Empty(Directory.GetFiles(Path.Combine(tmp.Path, "machine"), "*.omni-tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Overwrites_same_name_and_keeps_other_jobs()
    {
        using var tmp = new TempDir();
        var t = Targets(tmp);
        Directory.CreateDirectory(t.CutDirectory);
        File.WriteAllText(Path.Combine(t.CutDirectory, "a.nc"), "old");
        File.WriteAllText(Path.Combine(t.CutDirectory, "other_job.nc"), "keep");
        var zip = Package(tmp, new JobPackageEntry(tmp.File("s/a.nc", "new"), "cut/a.nc", JobFileRole.Cut));

        JobPlacer.Place(zip, t);

        Assert.Equal("new", File.ReadAllText(Path.Combine(t.CutDirectory, "a.nc")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(t.CutDirectory, "other_job.nc")));
    }

    [Fact]
    public void Tampered_package_places_nothing()
    {
        using var tmp = new TempDir();
        var zip = Package(tmp,
            new(tmp.File("s/a.nc", "N1"), "cut/a.nc", JobFileRole.Cut),
            new(tmp.File("s/b.nc", "N2"), "cut/b.nc", JobFileRole.Cut));
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
        {
            archive.GetEntry("cut/b.nc")!.Delete();
            using var w = new StreamWriter(archive.CreateEntry("cut/b.nc").Open());
            w.Write("changed");
        }
        var t = Targets(tmp);

        Assert.Throws<InvalidDataException>(() => JobPlacer.Place(zip, t));
        Assert.False(Directory.Exists(t.CutDirectory));
    }

    [Fact]
    public void Rejects_two_files_flattening_to_same_name()
    {
        using var tmp = new TempDir();
        var zip = Package(tmp,
            new(tmp.File("s/1/a.nc", "1"), "cut/s1/a.nc", JobFileRole.Cut),
            new(tmp.File("s/2/a.nc", "2"), "cut/s2/a.nc", JobFileRole.Cut));

        Assert.Throws<InvalidDataException>(() => JobPlacer.Place(zip, Targets(tmp)));
    }

    [Fact]
    public void Missing_target_folder_is_an_error()
    {
        using var tmp = new TempDir();
        var zip = Package(tmp, new JobPackageEntry(tmp.File("s/x.bmp", "BM"), "label/x.bmp", JobFileRole.Label));

        Assert.Throws<InvalidOperationException>(() => JobPlacer.Place(zip, new PlacementTargets
        {
            CutDirectory = Path.Combine(tmp.Path, "NC"),
            LabelDirectory = "",
        }));
    }
}
