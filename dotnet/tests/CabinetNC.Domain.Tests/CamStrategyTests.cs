using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Parts;
using CabinetNC.Domain.Geometry;

namespace CabinetNC.Domain.Tests;

public class CamStrategyTests
{
    [Fact]
    public void Classifies_feature_ops_into_three_strategies()
    {
        Assert.Equal(CamStrategyKind.Drilling, CamStrategy.Classify(Op("drill")));
        Assert.Equal(CamStrategyKind.AreaClearance, CamStrategy.Classify(Op("pocket")));
        Assert.Equal(CamStrategyKind.Profile, CamStrategy.Classify(Op("contour")));
        Assert.Equal(CamStrategyKind.Profile, CamStrategy.Classify(Op("contour", featureId: "CUT1")));
    }

    [Fact]
    public void Tongue_groove_is_profile_other_groove_is_clearance()
    {
        var tongue = Op("groove", width: 6.35, tongue: true);
        var other = Op("groove", width: 6.35);
        Assert.Equal(CamStrategyKind.Profile, CamStrategy.Classify(tongue, toolDiameterMm: 6.35));
        Assert.Equal(CamStrategyKind.AreaClearance, CamStrategy.Classify(other, toolDiameterMm: 6.35));
        Assert.Equal(TroyPassKind.TongueGroove, TroyPass.Classify(tongue));
        Assert.Equal(TroyPassKind.UnclassifiedGroove, TroyPass.Classify(other));
    }

    [Fact]
    public void Purpose_tongue_marks_feature_and_binds_T1()
    {
        var panel = new Panel
        {
            PanelId = "P",
            ThicknessMm = 18,
            Outline = new Outline { Points = [new(0, 0), new(100, 0), new(100, 50), new(0, 50)] },
            Features =
            [
                new PanelFeature
                {
                    FeatureId = "G1", Kind = "grooveVertical", Purpose = "tongue",
                    WidthMm = 6, DepthMm = 9,
                    Path = [new(10, 10), new(90, 10)],
                },
                new PanelFeature
                {
                    FeatureId = "G2", Kind = "grooveVertical",
                    WidthMm = 20, DepthMm = 4,
                    Path = [new(10, 30), new(90, 30)],
                },
            ],
        };
        Assert.True(PanelEdit.IsTongueGroove(panel.Features[0]));
        Assert.False(PanelEdit.IsTongueGroove(panel.Features[1]));
        var ops = OpsPlanner.FeaturesToOps([panel]);
        Assert.Contains(ops, o => o.Op == "groove" && o.FeatureId == "G1" && o.IsTongue && o.ToolId == "T1");
        Assert.Contains(ops, o => o.Op == "groove" && o.FeatureId == "G2" && !o.IsTongue && o.ToolId == "T2");
        var narrow = Assert.Single(ops, o => o.FeatureId == "G1");
        Assert.True(narrow.PathSegments is null or { Count: 0 });
        var wide = Assert.Single(ops, o => o.FeatureId == "G2");
        Assert.True(wide.FinishLoop is { Count: >= 3 } || wide.PathSegments is { Count: > 0 });
        Assert.Contains(ops, o => o.Op == "contour" && o.ToolId == "T2");
    }

    static CutOp Op(string kind, string? featureId = null, double? width = null, bool tongue = false) => new()
    {
        Op = kind,
        PanelId = "P",
        FeatureId = featureId,
        WidthMm = width,
        IsTongue = tongue,
    };
}
