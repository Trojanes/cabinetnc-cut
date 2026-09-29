namespace CabinetNC.Cloud.Client;

using System.Net.Http.Json;
using CabinetNC.Cloud.Contracts;

public sealed class OmniCloudClient
{
    readonly HttpClient _http;

    public OmniCloudClient(HttpClient http)
    {
        if (http.BaseAddress is null)
            throw new ArgumentException("HttpClient.BaseAddress must be the cloud base URL.", nameof(http));
        _http = http;
    }

    public static OmniCloudClient Create(CloudSettings settings)
    {
        if (!settings.TryGetBaseUri(out var baseUri))
            throw new InvalidOperationException(
                $"云端地址未配置：在 {CloudSettingsStore.DefaultPath()} 写入 cloudBaseUrl");
        return new OmniCloudClient(new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(15) });
    }

    public async Task<HealthResponse> GetHealthAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<HealthResponse>(Relative(CloudRoutes.Health), CloudJson.Options, ct).ConfigureAwait(false)
        ?? throw new InvalidDataException("Empty health response");

    public async Task<VersionResponse> GetVersionAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<VersionResponse>(Relative(CloudRoutes.Version), CloudJson.Options, ct).ConfigureAwait(false)
        ?? throw new InvalidDataException("Empty version response");

    // Leading '/' would discard any path prefix on the base URL.
    static string Relative(string route) => route.TrimStart('/');
}
