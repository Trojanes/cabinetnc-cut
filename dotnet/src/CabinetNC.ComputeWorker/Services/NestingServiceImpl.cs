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
            var border = request.BorderMm > 0 ? request.BorderMm : 15;
            var spacing = request.SpacingMm > 0 ? request.SpacingMm : 12;
            var sheetW = request.SheetWidthMm > 0 ? request.SheetWidthMm : 1220;
            var sheetL = request.SheetLengthMm > 0 ? request.SheetLengthMm : 2440;

            // Reconstruct panels so Worker uses the same GroupedBlf path as Desktop (Day 13).
            var panels = request.Parts.Select(p => new Panel
            {
                PanelId = p.PanelId,
                Material = string.IsNullOrWhiteSpace(p.Material) ? null : p.Material,
                ThicknessMm = p.ThicknessMm > 0 ? p.ThicknessMm : 0,
                AllowedRotations = p.MayRotate ? null : new[] { 0, 180 },
                Outline = new Outline
                {
                    Points =
                    [
                        new(0, 0),
                        new(p.WidthMm, 0),
                        new(p.WidthMm, p.HeightMm),
                        new(0, p.HeightMm),
                    ],
                    Closed = true,
                },
            }).ToList();

            var settings = new NestSettings
            {
                MarginMm = border,
                ClearanceMm = spacing,
                AllowRotation = request.AllowRotation,
                GrainLock = true,
            };
            var stock = new[]
            {
                new NestSheetSpec
                {
                    WidthMm = sheetW,
                    LengthMm = sheetL,
                    BorderMm = border,
                    Material = null,
                    ThicknessMm = 0,
                    Label = "STOCK",
                },
            };

            var (packed, log) = new NestEngineRouter().Run(new NestEngineRequest
            {
                Panels = panels,
                Settings = settings,
                StockTemplates = stock,
                SizeOf = GroupedBlfNester.SizeOfOutline,
                EnginePreference = "blf",
            });

            var aabbParts = panels.Select(p =>
            {
                var (w, h) = GroupedBlfNester.SizeOfOutline(p);
                return new NestPart { PanelId = p.PanelId, WidthMm = w, HeightMm = h };
            }).ToList();
            var collisions = NestValidator.FindAabbCollisions(aabbParts, packed.Placements, spacing);

            var reply = new StartNestingReply
            {
                Ok = true,
                Engine = packed.Engine,
                SheetCount = packed.SheetCount,
            };
            reply.Unplaced.AddRange(packed.Unplaced);
            foreach (var p in packed.Placements)
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
            if (!string.IsNullOrWhiteSpace(log.FallbackReason))
            {
                reply.Warnings.Add(new NestWarningMsg
                {
                    Code = "engine_fallback",
                    Message = log.FallbackReason,
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
                Material = string.IsNullOrWhiteSpace(p.Material) ? null : p.Material,
                ThicknessMm = p.ThicknessMm > 0 ? p.ThicknessMm : 18,
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
                ContourCount = ops.Count(o => o.Op is "contour" or "pocket"),
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
