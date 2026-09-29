namespace CabinetNC.Cloud.Contracts;

/// <summary>
/// HTTP surface shared by Omni, OmniLight and CabLab. Clients talk only to
/// these routes — never to Cloud Storage / Firestore directly — so the backend
/// can move region or provider without a client release.
/// </summary>
public static class CloudRoutes
{
    public const string ApiVersion = "v1";

    public const string Health = "/v1/health";
    public const string Version = "/v1/version";
}

public sealed record HealthResponse(
    string Service,
    string ServerVersion,
    string ApiVersion,
    string Region,
    string Environment,
    DateTimeOffset UtcNow);

public sealed record VersionResponse(
    string ApiVersion,
    string ServerVersion,
    string MinClientVersion);
