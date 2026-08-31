using CabinetNC.Domain.Manufacturing;

namespace CabinetNC.Domain.Tests;

public class NcCutSimTests
{
    static string SampleNc() => """
        G90
        G40
        M6 T2
        G0 X0.0000 Y0.0000 Z30.0000
        G1 Z0.5000 F1000.0
        G1 X100.0000 Y0.0000 F12000.0
        G0 Z30.0000
        G1 Z-0.5500 F1000.0
        G1 X100.0000 Y80.0000 F20000.0
        G0 Z30.0000
        M30
        """;

    [Fact]
    public void Duration_uses_feed_for_cut_and_rapid_rate_for_g0()
    {
        var replay = OsaiTroyParser.Replay(SampleNc());
        Assert.Contains(replay.Strokes, s => !s.Rapid && Math.Abs(s.X1 - 100) < 1e-6 && Math.Abs(s.Y1) < 1e-6);

        var cut = replay.Strokes.First(s =>
            !s.Rapid && Math.Abs(s.X1 - 100) < 1e-6 && Math.Abs(s.Y1) < 1e-6 && Math.Abs(s.Z1 - 0.5) < 1e-6);
        Assert.InRange(NcCutSim.DurationSec(cut), 0.49, 0.51);

        var rapid = replay.Strokes.First(s => s.Rapid && Math.Abs(s.Z1 - 30) < 1e-6);
        Assert.True(NcCutSim.DurationSec(rapid) > 0);
        Assert.True(NcCutSim.DurationSec(rapid) < NcCutSim.DurationSec(cut));
    }

    [Fact]
    public void At_midpoint_of_horizontal_leave_cut()
    {
        var replay = OsaiTroyParser.Replay(SampleNc());
        var cut = replay.Strokes.First(s =>
            !s.Rapid && Math.Abs(s.X1 - 100) < 1e-6 && Math.Abs(s.Y1) < 1e-6 && Math.Abs(s.Z1 - 0.5) < 1e-6);
        var idx = replay.Strokes.ToList().IndexOf(cut);
        var t0 = replay.Strokes.Take(idx).Sum(NcCutSim.DurationSec);
        var pose = NcCutSim.At(replay.Strokes, t0 + NcCutSim.DurationSec(cut) * 0.5);
        Assert.Equal(idx, pose.StrokeIndex);
        Assert.InRange(pose.X, 49, 51);
        Assert.InRange(pose.Y, -0.2, 0.2);
        Assert.InRange(pose.Z, 0.4, 0.6);
        Assert.False(pose.Done);
    }

    [Fact]
    public void At_end_sits_on_last_point()
    {
        var replay = OsaiTroyParser.Replay(SampleNc());
        var total = NcCutSim.TotalSec(replay.Strokes);
        var pose = NcCutSim.At(replay.Strokes, total + 1);
        var last = replay.Strokes[^1];
        Assert.True(pose.Done);
        Assert.InRange(pose.X, last.X1 - 0.01, last.X1 + 0.01);
        Assert.InRange(pose.Y, last.Y1 - 0.01, last.Y1 + 0.01);
        Assert.InRange(pose.Z, last.Z1 - 0.01, last.Z1 + 0.01);
    }

    [Fact]
    public void KindOf_splits_rapid_leave_and_through()
    {
        var replay = OsaiTroyParser.Replay(SampleNc());
        Assert.Contains(replay.Strokes, s => NcCutSim.KindOf(s) == NcCutSim.StrokeKind.Rapid);
        Assert.Contains(replay.Strokes, s => NcCutSim.KindOf(s) == NcCutSim.StrokeKind.Leave);
        Assert.Contains(replay.Strokes, s => NcCutSim.KindOf(s) == NcCutSim.StrokeKind.Through);
    }

    [Fact]
    public void ToolDiameter_reads_catalog()
    {
        Assert.Equal(10, NcCutSim.ToolDiameterMm(2));
        Assert.Equal(6.35, NcCutSim.ToolDiameterMm(1));
        Assert.Equal(3, NcCutSim.ToolDiameterMm(3));
    }
}
