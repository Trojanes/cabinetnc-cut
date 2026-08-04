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
            {"schema":"cabinetnc.cut-package","schemaVersion":1,"panels":[{"panelId":"P1","thicknessMm":18,"outline":{"points":[[0,0],[100,0],[100,50],[0,50]],"closed":true},"features":[{"featureId":"H1","kind":"holeVertical","x":10,"y":10,"diameterMm":5}]}]}
            """;
        Assert.True(s.OpenPackageJson(json).Ok);
        return s;
    }

    [Fact]
    public void ReplacePanel_marks_dirty_and_undo_restores()
    {
        var s = SessionWithPanel();
        Assert.False(s.ManufacturingDirty);
        var panel = s.Package!.Panels[0];
        var moved = new Panel
        {
            PanelId = panel.PanelId,
            Name = panel.Name,
            Material = panel.Material,
            ThicknessMm = panel.ThicknessMm,
            Quantity = panel.Quantity,
            AllowedRotations = panel.AllowedRotations,
            GrainDirection = panel.GrainDirection,
            Outline = panel.Outline,
            Features =
            [
                new PanelFeature
                {
                    FeatureId = "H1",
                    Kind = "holeVertical",
                    X = 40,
                    Y = 10,
                    DiameterMm = 5,
                },
            ],
            Identity = panel.Identity,
            Orientation = panel.Orientation,
        };
        s.ReplacePanel(moved);
        Assert.True(s.ManufacturingDirty);
        Assert.Equal(40, s.Package!.Panels[0].Features[0].X);
        Assert.True(s.TryUndo());
        Assert.Equal(10, s.Package!.Panels[0].Features[0].X);
        Assert.True(s.ManufacturingDirty); // still dirty until nest rebuild
        Assert.True(s.TryRedo());
        Assert.Equal(40, s.Package!.Panels[0].Features[0].X);
        s.MarkManufacturingClean();
        Assert.False(s.ManufacturingDirty);
    }
}
