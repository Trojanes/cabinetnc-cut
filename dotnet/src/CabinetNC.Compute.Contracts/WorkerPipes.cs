namespace CabinetNC.Compute.Contracts;

/// <summary>Shared Named Pipe endpoint for Desktop ↔ ComputeWorker gRPC.</summary>
public static class WorkerPipes
{
    public const string Name = "cabinetnc.compute.v1";
    public const string ContractVersion = "1";
}
