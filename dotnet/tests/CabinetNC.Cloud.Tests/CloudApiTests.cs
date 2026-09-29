using CabinetNC.Cloud.Client;
using CabinetNC.Cloud.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CabinetNC.Cloud.Tests;

public class CloudApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    readonly WebApplicationFactory<Program> _factory;

    public CloudApiTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("OMNI_REGION", "australia-southeast1");
            b.UseSetting("OMNI_ENV", "test");
            b.UseSetting("OMNI_MIN_CLIENT_VERSION", "0.1.0");
        });

    [Fact]
    public async Task Client_reads_health()
    {
        var client = new OmniCloudClient(_factory.CreateClient());

        var health = await client.GetHealthAsync();

        Assert.Equal("omni-api", health.Service);
        Assert.Equal(CloudRoutes.ApiVersion, health.ApiVersion);
        Assert.Equal("australia-southeast1", health.Region);
        Assert.Equal("test", health.Environment);
    }

    [Fact]
    public async Task Client_reads_version()
    {
        var client = new OmniCloudClient(_factory.CreateClient());

        var version = await client.GetVersionAsync();

        Assert.Equal("0.1.0", version.MinClientVersion);
        Assert.StartsWith("0.1.0", version.ServerVersion);
    }

    [Fact]
    public void Create_without_base_url_explains_where_to_configure()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => OmniCloudClient.Create(new CloudSettings()));
        Assert.Contains("cloud.json", ex.Message);
    }
}
