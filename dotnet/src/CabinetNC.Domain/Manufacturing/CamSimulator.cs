namespace CabinetNC.Domain.Manufacturing;

public sealed record CamFrame(
    int OpIndex,
    int PointIndex,
    double X,
    double Y,
    CutOp Op,
    string Kind);

/// <summary>Point-level CAM playhead — port of src/cam_sim.js.</summary>
public static class CamSimulator
{
    public static IReadOnlyList<CamFrame> ExpandFrames(IEnumerable<CutOp> ops)
    {
        var frames = new List<CamFrame>();
        var list = ops.Where(o => o.Placed).ToList();
        for (var opIndex = 0; opIndex < list.Count; opIndex++)
        {
            var op = list[opIndex];
            if (op.Op == "drill" && op.SheetX is double sx && op.SheetY is double sy)
            {
                frames.Add(new(opIndex, 0, sx, sy, op, "drill"));
                continue;
            }

            if (op.Path is not { Count: > 0 } path) continue;
            var closed = op.Op == "contour" && path.Count >= 3;
            var n = closed ? path.Count + 1 : path.Count;
            for (var i = 0; i < n; i++)
            {
                var p = path[i % path.Count];
                frames.Add(new(opIndex, i, p.X, p.Y, op, op.Op));
            }
        }
        return frames;
    }

    public static int Step(int index, int count, int delta)
    {
        if (count <= 0) return 0;
        return ((index + delta) % count + count) % count;
    }

    public static string Describe(CamFrame? frame, int index, int count)
    {
        if (frame is null) return $"— / {count}";
        return $"{index + 1}/{count} · {frame.Kind} {frame.Op.PanelId} pt{frame.PointIndex} " +
               $"@({Math.Round(frame.X)},{Math.Round(frame.Y)})";
    }
}
