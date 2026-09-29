namespace CabinetNC.Cloud.Contracts;

using System.Text.Json.Serialization;

/// <summary>
/// <c>manifest.json</c> at the root of a job zip sent from Omni / CabLab to an
/// OmniLight machine PC. OmniLight places files by <see cref="JobFile.Role"/>,
/// never by the folder layout inside the zip.
/// </summary>
public sealed class JobManifest
{
    public const string SchemaName = "omni.cloud-job";
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "manifest.json";

    public string Schema { get; set; } = SchemaName;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public required string JobId { get; set; }
    public required string ProjectName { get; set; }
    public string ShopId { get; set; } = CloudDefaults.ShopId;
    /// <summary><c>omni</c> or <c>caplab</c>.</summary>
    public string Source { get; set; } = "omni";
    public string? TargetDeviceId { get; set; }
    /// <summary>Post-processor that produced the cut files, e.g. <c>osai_troy</c>.</summary>
    public string? PostId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<JobFile> Files { get; set; } = [];
}

public sealed class JobFile
{
    /// <summary>Forward-slash path inside the zip, e.g. <c>cut/Kitchen_S1_T1.nc</c>.</summary>
    public required string Path { get; set; }
    public JobFileRole Role { get; set; }
    public int? SheetIndex { get; set; }
    public long SizeBytes { get; set; }
    /// <summary>Lower-case hex SHA-256 of the file bytes.</summary>
    public required string Sha256 { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<JobFileRole>))]
public enum JobFileRole
{
    /// <summary>Controller program — goes to the machine's program folder.</summary>
    Cut,
    /// <summary>Label artwork — flattened into the labeler's picture folder.</summary>
    Label,
    /// <summary>DXF / job sheet / previews — shown in OmniLight, not placed on the machine.</summary>
    Preview,
}

[JsonConverter(typeof(JsonStringEnumConverter<JobStatus>))]
public enum JobStatus
{
    Uploaded,
    Received,
    Placed,
    Failed,
}

public static class CloudDefaults
{
    public const string ShopId = "main";
}
