using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using CabinetNC.Compute.Contracts;
using Grpc.Net.Client;

namespace CabinetNC.Desktop.Worker;

public sealed class WorkerProcessHost : IAsyncDisposable
{
    Process? _process;
    GrpcChannel? _channel;

    public string? LastError { get; private set; }
    public bool IsRunning => _process is { HasExited: false };

    public async Task<bool> EnsureStartedAsync(CancellationToken ct = default)
    {
        LastError = null;
        if (IsRunning && _channel is not null) return true;

        try
        {
            await StopAsync().ConfigureAwait(false);

            var exe = ResolveWorkerExe();
            if (exe is null)
            {
                LastError = "ComputeWorker exe not found — build CabinetNC.ComputeWorker first";
                return false;
            }

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(exe)!,
                },
                EnableRaisingEvents = true,
            };
            _process.Start();

            _channel = CreateChannel(WorkerPipes.Name);

            // wait for pipe accept
            var health = new WorkerHealth.WorkerHealthClient(_channel);
            var deadline = DateTime.UtcNow.AddSeconds(8);
            Exception? last = null;
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var reply = await health.PingAsync(new PingRequest { Token = "boot" }, cancellationToken: ct);
                    if (reply.Ok) return true;
                }
                catch (Exception ex)
                {
                    last = ex;
                    await Task.Delay(200, ct).ConfigureAwait(false);
                }
            }

            LastError = last?.Message ?? "Worker ping timeout";
            return false;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public WorkerHealth.WorkerHealthClient? GetHealthClient() =>
        _channel is null ? null : new WorkerHealth.WorkerHealthClient(_channel);

    public Nesting.NestingClient? GetNestingClient() =>
        _channel is null ? null : new Nesting.NestingClient(_channel);

    public Operations.OperationsClient? GetOperationsClient() =>
        _channel is null ? null : new Operations.OperationsClient(_channel);

    public PostProcessor.PostProcessorClient? GetPostProcessorClient() =>
        _channel is null ? null : new PostProcessor.PostProcessorClient(_channel);

    public async Task StopAsync()
    {
        if (_channel is not null)
        {
            await _channel.ShutdownAsync().ConfigureAwait(false);
            _channel.Dispose();
            _channel = null;
        }

        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch { /* ignore */ }
        }
        _process?.Dispose();
        _process = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    public static GrpcChannel CreateChannel(string pipeName)
    {
        var handler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var pipe = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.WriteThrough | PipeOptions.Asynchronous);
                try
                {
                    await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    return pipe;
                }
                catch
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            },
        };

        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = handler,
        });
    }

    static string? ResolveWorkerExe()
    {
        // Prefer sibling build output when running from Desktop bin
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "CabinetNC.ComputeWorker", "bin", "Debug", "net10.0", "CabinetNC.ComputeWorker.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "CabinetNC.ComputeWorker", "bin", "Release", "net10.0", "CabinetNC.ComputeWorker.exe")),
            Path.Combine(baseDir, "CabinetNC.ComputeWorker.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
