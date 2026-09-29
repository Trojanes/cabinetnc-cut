using System.IO.Compression;
using CabinetNC.Cloud.Client;
using CabinetNC.Cloud.Contracts;

namespace CabinetNC.Cloud.Tests;

public class JobPackageTests
{
    static JobManifest NewManifest() => new() { JobId = "job-1", ProjectName = "22'6 Club Lounge" };

    static string BuildSample(TempDir tmp)
    {
        var zip = Path.Combine(tmp.Path, "out", "job-1.zip");
        JobPackage.Write(NewManifest(),
        [
            new(tmp.File("src/Kitchen_S1_T1.nc", "N1 G90\r\nN2 M30\r\n"), "cut/Kitchen_S1_T1.nc", JobFileRole.Cut, 0),
            new(tmp.File("src/OHC_OH_D0_2.bmp", "BM-fake"), "label/OHC_OH_D0_2.bmp", JobFileRole.Label, 0),
            new(tmp.File("src/Kitchen_S1.dxf", "0\nEOF\n"), "preview/Kitchen_S1.dxf", JobFileRole.Preview, 0),
        ], zip);
        return zip;
    }

    [Fact]
    public void Write_then_verify_round_trips_manifest()
    {
        using var tmp = new TempDir();
        var zip = BuildSample(tmp);

        var m = JobPackage.Verify(zip);

        Assert.Equal(JobManifest.SchemaName, m.Schema);
        Assert.Equal("22'6 Club Lounge", m.ProjectName);
        Assert.Equal(3, m.Files.Count);
        var cut = m.Files.Single(f => f.Role == JobFileRole.Cut);
        Assert.Equal(64, cut.Sha256.Length);
        Assert.Equal(new FileInfo(tmp.File("src/Kitchen_S1_T1.nc")).Length, cut.SizeBytes);
        Assert.False(File.Exists(zip + ".tmp"));
    }

    [Fact]
    public void Manifest_json_uses_string_roles()
    {
        using var tmp = new TempDir();
        var zip = BuildSample(tmp);
        using var archive = ZipFile.OpenRead(zip);
        using var reader = new StreamReader(archive.GetEntry(JobManifest.FileName)!.Open());
        var json = reader.ReadToEnd();

        Assert.Contains("\"role\": \"Cut\"", json);
        Assert.Contains("\"schema\": \"omni.cloud-job\"", json);
    }

    [Fact]
    public void Verify_rejects_tampered_file()
    {
        using var tmp = new TempDir();
        var zip = BuildSample(tmp);
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
        {
            archive.GetEntry("cut/Kitchen_S1_T1.nc")!.Delete();
            using var w = new StreamWriter(archive.CreateEntry("cut/Kitchen_S1_T1.nc").Open());
            w.Write("N1 G0 Z-50\r\n");
        }

        var ex = Assert.Throws<InvalidDataException>(() => JobPackage.Verify(zip));
        Assert.Contains("checksum mismatch cut/Kitchen_S1_T1.nc", ex.Message);
    }

    [Fact]
    public void Verify_rejects_missing_entry()
    {
        using var tmp = new TempDir();
        var zip = BuildSample(tmp);
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
            archive.GetEntry("label/OHC_OH_D0_2.bmp")!.Delete();

        var ex = Assert.Throws<InvalidDataException>(() => JobPackage.Verify(zip));
        Assert.Contains("missing label/OHC_OH_D0_2.bmp", ex.Message);
    }

    [Theory]
    [InlineData("../evil.nc")]
    [InlineData("/abs.nc")]
    [InlineData("cut\\win.nc")]
    [InlineData("C:/x.nc")]
    [InlineData("cut//x.nc")]
    [InlineData("manifest.json")]
    public void Write_rejects_unsafe_paths(string packagePath)
    {
        using var tmp = new TempDir();
        var src = tmp.File("a.nc", "x");

        Assert.Throws<InvalidDataException>(() => JobPackage.Write(NewManifest(),
            [new(src, packagePath, JobFileRole.Cut)], Path.Combine(tmp.Path, "j.zip")));
    }

    [Fact]
    public void Write_rejects_duplicate_paths()
    {
        using var tmp = new TempDir();
        var src = tmp.File("a.nc", "x");

        Assert.Throws<ArgumentException>(() => JobPackage.Write(NewManifest(),
        [
            new(src, "cut/A.nc", JobFileRole.Cut),
            new(src, "cut/a.nc", JobFileRole.Cut),
        ], Path.Combine(tmp.Path, "j.zip")));
    }

    [Fact]
    public void Read_rejects_newer_schema()
    {
        using var tmp = new TempDir();
        var zip = Path.Combine(tmp.Path, "future.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var w = new StreamWriter(archive.CreateEntry(JobManifest.FileName).Open()))
            w.Write("""{"schema":"omni.cloud-job","schemaVersion":99,"jobId":"j","projectName":"p","files":[]}""");

        var ex = Assert.Throws<InvalidDataException>(() => JobPackage.ReadManifest(zip));
        Assert.Contains("update OmniLight", ex.Message);
    }
}
