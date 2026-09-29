namespace CabinetNC.Cloud.Contracts;

using System.Text.Json;

public static class CloudJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}
