using CabinetNC.Compute.Contracts;
using Grpc.Core;

namespace CabinetNC.ComputeWorker.Services;

public sealed class WorkerHealthService : WorkerHealth.WorkerHealthBase
{
    public const string WorkerVersion = "0.1.0-desktop-p0";

    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        return Task.FromResult(new PingReply
        {
            Ok = true,
            Message = string.IsNullOrEmpty(request.Token) ? "pong" : $"pong:{request.Token}",
            UnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
    }

    public override Task<VersionReply> GetWorkerVersion(VersionRequest request, ServerCallContext context)
    {
        return Task.FromResult(new VersionReply
        {
            WorkerVersion = WorkerVersion,
            ContractVersion = WorkerPipes.ContractVersion,
        });
    }
}
