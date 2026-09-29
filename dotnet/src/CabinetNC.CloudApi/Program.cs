using System.Reflection;
using CabinetNC.Cloud.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Cloud Run injects PORT; ASP.NET does not read it on its own.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

var serverVersion = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? "0.0.0";
var region = app.Configuration["OMNI_REGION"] ?? "local";
var environment = app.Configuration["OMNI_ENV"] ?? "dev";
var minClientVersion = app.Configuration["OMNI_MIN_CLIENT_VERSION"] ?? "0.0.0";

app.MapGet(CloudRoutes.Health, () => new HealthResponse(
    Service: "omni-api",
    ServerVersion: serverVersion,
    ApiVersion: CloudRoutes.ApiVersion,
    Region: region,
    Environment: environment,
    UtcNow: DateTimeOffset.UtcNow));

app.MapGet(CloudRoutes.Version, () => new VersionResponse(
    ApiVersion: CloudRoutes.ApiVersion,
    ServerVersion: serverVersion,
    MinClientVersion: minClientVersion));

app.Run();

public partial class Program;
