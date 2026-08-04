using CabinetNC.Compute.Contracts;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;
using Grpc.Core;

namespace CabinetNC.ComputeWorker.Services;

public sealed class NestingServiceImpl : Nesting.NestingBase
{
    public override Task<StartNestingReply> StartNesting(StartNestingRequest request, ServerCallContext context)
    {
        try
        {
            var parts = request.Parts.Select(p => new NestPart
            {
                PanelId = p.PanelId,
                WidthMm = p.WidthMm,
                HeightMm = p.HeightMm,
                MayRotate = p.MayRotate,
            }).ToList();

            var spacing = request.SpacingMm > 0 ? request.SpacingMm : 12;
            var result = BlfNester.Pack(new NestRequest
            {
                Parts = parts,
                SheetWidthMm = request.SheetWidthMm > 0 ? request.SheetWidthMm : 1220,
                SheetLengthMm = request.SheetLengthMm > 0 ? request.SheetLengthMm : 2440,
                SpacingMm = spacing,
                BorderMm = request.BorderMm > 0 ? request.BorderMm : 15,
                AllowRotation = request.AllowRotation,
            });

            var collisions = NestValidator.FindAabbCollisions(parts, result.Placements, spacing);

            var reply = new StartNestingReply
            {
                Ok = true,
                Engine = result.Engine,
                SheetCount = result.SheetCount,
            };
            reply.Unplaced.AddRange(result.Unplaced);
            foreach (var p in result.Placements)
            {
                reply.Placements.Add(new NestPlacementMsg
                {
                    PanelId = p.PanelId,
                    SheetIndex = p.SheetIndex,
                    OffsetX = p.OffsetX,
                    OffsetY = p.OffsetY,
                    RotationDeg = p.RotationDeg,
                });
            }
            foreach (var c in collisions)
            {
                reply.Warnings.Add(new NestWarningMsg
                {
                    Code = "aabb_gap",
                    Message = $"spacing/collision {c.PanelIdA} × {c.PanelIdB} on sheet {c.SheetIndex}",
                    PanelIdA = c.PanelIdA,
                    PanelIdB = c.PanelIdB,
                    SheetIndex = c.SheetIndex,
                });
            }
            return Task.FromResult(reply);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new StartNestingReply { Ok = false, Error = ex.Message });
        }
    }
}

public sealed class OperationsServiceImpl : Operations.OperationsBase
{
    public override Task<GenerateOperationsReply> GenerateOperations(
        GenerateOperationsRequest request,
        ServerCallContext context)
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

            var ops = OpsPlanner.AttachToNest(OpsPlanner.FeaturesToOps(panels), placements);
            var reply = new GenerateOperationsReply
            {
                Ok = true,
                ContourCount = ops.Count(o => o.Op == "contour"),
                DrillCount = ops.Count(o => o.Op == "drill"),
            };
            foreach (var op in ops)
            {
                reply.Ops.Add(new CutOpMsg
                {
                    Op = op.Op,
                    PanelId = op.PanelId,
                    FeatureId = op.FeatureId ?? "",
                    Placed = op.Placed,
                    SheetIndex = op.SheetIndex,
                    SheetX = op.SheetX ?? 0,
                    SheetY = op.SheetY ?? 0,
                    DiameterMm = op.DiameterMm ?? 0,
                    DepthMm = op.DepthMm ?? 0,
                    PathPointCount = op.Path?.Count ?? 0,
                });
            }
            return Task.FromResult(reply);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new GenerateOperationsReply { Ok = false, Error = ex.Message });
        }
    }
}
