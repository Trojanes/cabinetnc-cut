namespace CabinetNC.Cloud.Client;

using System.Text.Json;
using CabinetNC.Cloud.Contracts;

/// <summary>
/// Machine-wide cloud settings shared by Omni, OmniLight and CabLab:
/// <c>%ProgramData%\Omni\cloud.json</c>. The server address lives only here so
/// moving region, provider or domain is a one-line edit, not a rebuild.
/// </summary>
public sealed class CloudSettings
{
    public string? CloudBaseUrl { get; set; }
    public string ShopId { get; set; } = CloudDefaults.ShopId;

    public bool IsConfigured => TryGetBaseUri(out _);

    public bool TryGetBaseUri(out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(CloudBaseUrl)) return false;
        var raw = CloudBaseUrl.Trim();
        if (!raw.EndsWith('/')) raw += "/";
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) return false;
        uri = parsed;
        return true;
    }
}

public static class CloudSettingsStore
{
    public const string ConfigPathEnv = "OMNI_CLOUD_CONFIG";
    public const string BaseUrlEnv = "OMNI_CLOUD_BASE_URL";

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Omni",
            "cloud.json");

    public static CloudSettings Load(string? path = null, Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        path ??= NonEmpty(getEnv(ConfigPathEnv)) ?? DefaultPath();

        var settings = new CloudSettings();
        try
        {
            if (File.Exists(path))
                settings = JsonSerializer.Deserialize<CloudSettings>(File.ReadAllText(path), CloudJson.Options)
                    ?? new CloudSettings();
        }
        catch (JsonException)
        {
            /* corrupt → unconfigured; caller shows "cloud not configured" */
        }

        if (string.IsNullOrWhiteSpace(settings.ShopId))
            settings.ShopId = CloudDefaults.ShopId;
        if (NonEmpty(getEnv(BaseUrlEnv)) is { } url)
            settings.CloudBaseUrl = url;
        return settings;
    }

    public static void Save(CloudSettings settings, string? path = null)
    {
        path ??= DefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, CloudJson.Options));
    }

    static string? NonEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
