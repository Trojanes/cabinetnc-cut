using CabinetNC.Cloud.Client;
using CabinetNC.Cloud.Contracts;

namespace CabinetNC.Cloud.Tests;

public class CloudSettingsTests
{
    static Func<string, string?> Env(Dictionary<string, string>? vars = null) =>
        k => vars is not null && vars.TryGetValue(k, out var v) ? v : null;

    [Fact]
    public void Missing_file_is_unconfigured_with_default_shop()
    {
        using var tmp = new TempDir();
        var s = CloudSettingsStore.Load(Path.Combine(tmp.Path, "cloud.json"), Env());

        Assert.False(s.IsConfigured);
        Assert.Equal(CloudDefaults.ShopId, s.ShopId);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "Omni", "cloud.json");
        CloudSettingsStore.Save(new CloudSettings
        {
            CloudBaseUrl = "https://omni-api-123.australia-southeast1.run.app",
            ShopId = "au-main",
        }, path);

        var s = CloudSettingsStore.Load(path, Env());

        Assert.True(s.IsConfigured);
        Assert.Equal("au-main", s.ShopId);
        Assert.Contains("\"cloudBaseUrl\"", File.ReadAllText(path));
    }

    [Fact]
    public void Env_overrides_file_path_and_base_url()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "dev.json");
        CloudSettingsStore.Save(new CloudSettings { CloudBaseUrl = "https://prod.example" }, path);

        var s = CloudSettingsStore.Load(getEnv: Env(new()
        {
            [CloudSettingsStore.ConfigPathEnv] = path,
            [CloudSettingsStore.BaseUrlEnv] = "http://localhost:5080",
        }));

        Assert.Equal("http://localhost:5080", s.CloudBaseUrl);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_unconfigured()
    {
        using var tmp = new TempDir();
        var path = tmp.File("cloud.json", "{ not json");

        Assert.False(CloudSettingsStore.Load(path, Env()).IsConfigured);
    }

    [Theory]
    [InlineData("https://api.example.com/omni", "https://api.example.com/omni/")]
    [InlineData("https://api.example.com/", "https://api.example.com/")]
    public void Base_uri_keeps_path_prefix(string raw, string expected)
    {
        Assert.True(new CloudSettings { CloudBaseUrl = raw }.TryGetBaseUri(out var uri));
        Assert.Equal(expected, uri.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://files.example.com")]
    public void Rejects_unusable_urls(string raw) =>
        Assert.False(new CloudSettings { CloudBaseUrl = raw }.IsConfigured);
}
