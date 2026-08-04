using CabinetNC.Compute.Contracts;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;
using Grpc.Core;

namespace CabinetNC.ComputeWorker.Services;

public sealed class PostProcessorServiceImpl : PostProcessor.PostProcessorBase
{
    public override Task<GenerateNcReply> GenerateNc(GenerateNcRequest request, ServerCallContext context)
    {
        try
        {
            var panels = request.Panels.Select(p => new Panel
            {
                PanelId = p.PanelId,
                Outline = new Outline
                {
                    Points = p.Outline.Select(pt => new Point2(pt.X, pt.Y)).ToList(),
                },
                Features = p.Features.Select(f => new PanelFeature
                {
                    FeatureId = f.FeatureId,
                    Kind = f.Kind,
                    X = f.X,
                    Y = f.Y,
                    DiameterMm = f.DiameterMm,
                    DepthMm = f.DepthMm,
                }).ToList(),
            }).ToList();

            var placements = request.Placements.Select(p => new NestPlacement
            {
                PanelId = p.PanelId,
                SheetIndex = p.SheetIndex,
                OffsetX = p.OffsetX,
                OffsetY = p.OffsetY,
                RotationDeg = p.RotationDeg,
            }).ToList();

            var profile = MachineCatalog.Get(request.MachineId);
            var ops = OpsPlanner.AttachToNest(OpsPlanner.FeaturesToOps(panels), placements).ToList();

            var nc = NcEmitter.OpsToNc(ops, profile);
            var lines = nc.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            return Task.FromResult(new GenerateNcReply
            {
                Ok = true,
                NcText = nc,
                MachineId = profile.Id,
                MachineName = profile.Name,
                LineCount = lines,
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new GenerateNcReply { Ok = false, Error = ex.Message });
        }
    }
}
