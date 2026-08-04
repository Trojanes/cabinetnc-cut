using CabinetNC.Application.Projects;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Parts;
using CabinetNC.FusionPackage;

namespace CabinetNC.Package.Tests;

public class ProjectSessionEditTests
{
    static ProjectSession SessionWithPanel()
    {
        var s = new ProjectSession();
        var json = """
            {"schema":"cabinetnc.cut-package","schemaVersion":1,"panels":[{"panelId":"P1","thicknessMm":18,"material":"oak","outline":{"points":[[0,0],[100,0],[100,50],[0,50]],"closed":true},"features":[{"featureId":"H1","kind":"holeVertical","x":10,"y":10,"diameterMm":5,"depthMm":12},{"featureId":"G1","kind":"grooveVertical","x":0,"y":0,"widthMm":6,"depthMm":8,"path":[[10,20],[90,20]]}],"orientation":{"millingFace":"A","grainDirection":"X","allowMirror":false},"workpieceId":"P1"}]}
            """;
        Assert.True(s.OpenPackageJson(json).Ok);
        return s;
    }

    [Fact]
    public void Undo_covers_move_param_and_add_feature()
    {
        var s = SessionWithPanel();
        var p0 = s.Package!.Panels[0];

        // 1) move hole
        s.ReplacePanel(PanelEdit.MoveHole(p0, "H1", 40, 10));
        Assert.Equal(40, s.Package!.Panels[0].Features.First(f => f.FeatureId == "H1").X);

        // 2) change depth via UpdateFeatureParams
        s.ReplacePanel(PanelEdit.UpdateFeatureParams(s.Package.Panels[0], "H1", depthMm: 16));
        Assert.Equal(16, s.Package!.Panels[0].Features.First(f => f.FeatureId == "H1").DepthMm);

        // 3) add groove
        s.ReplacePanel(PanelEdit.AddVerticalGroove(
            s.Package.Panels[0],
            [new Point2(5, 5), new Point2(40, 5)],
            widthMm: 4,
            depthMm: 6));
        Assert.Equal(3, s.Package!.Panels[0].Features.Count);

        Assert.True(s.ManufacturingDirty);
        Assert.True(s.TryUndo()); // undo add
        Assert.Equal(2, s.Package!.Panels[0].Features.Count);
        Assert.True(s.TryUndo()); // undo depth
        Assert.Equal(12, s.Package!.Panels[0].Features.First(f => f.FeatureId == "H1").DepthMm);
        Assert.True(s.TryUndo()); // undo move
        Assert.Equal(10, s.Package!.Panels[0].Features.First(f => f.FeatureId == "H1").X);
        // identity preserved across edits
        Assert.Equal("oak", s.Package.Panels[0].Material);
        Assert.NotNull(s.Package.Panels[0].Identity?.WorkpieceId);
    }

    [Fact]
    public void ReplacePanel_marks_dirty_and_undo_restores()
    {
        var s = SessionWithPanel();
        Assert.False(s.ManufacturingDirty);
        var panel = s.Package!.Panels[0];
        s.ReplacePanel(PanelEdit.MoveHole(panel, "H1", 40, 10));
        Assert.True(s.ManufacturingDirty);
        Assert.Equal(40, s.Package!.Panels[0].Features.First(f => f.FeatureId == "H1").X);
        Assert.True(s.TryUndo());
        Assert.Equal(10, s.Package!.Panels[0].Features.First(f => f.FeatureId == "H1").X);
        Assert.True(s.TryRedo());
        Assert.Equal(40, s.Package!.Panels[0].Features.First(f => f.FeatureId == "H1").X);
        s.MarkManufacturingClean();
        Assert.False(s.ManufacturingDirty);
    }
}
