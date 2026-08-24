using CabinetNC.Domain;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Materials;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class MaterialCorrectTests
{
    static Panel Box(
        string id,
        double thickness,
        string? material = "carcass",
        params PanelFeature[] features) =>
        new()
        {
            PanelId = id,
            Name = "Side",
            Material = material,
            ThicknessMm = thickness,
            Quantity = 1,
            Outline = new Outline
            {
                Points = [new Point2(0, 0), new Point2(100, 0), new Point2(100, 50), new Point2(0, 50)],
                Closed = true,
            },
            Features = features,
        };

    static PanelFeature Feat(
        string id,
        string kind,
        double depth,
        bool through = false,
        string? purpose = null,
        double? diameter = null) =>
        new()
        {
            FeatureId = id,
            Kind = kind,
            Through = through,
            Purpose = purpose,
            DepthMm = depth,
            DiameterMm = diameter,
            X = 10,
            Y = 10,
            Path = kind.Contains("groove", StringComparison.OrdinalIgnoreCase)
                ? [new Point2(0, 10), new Point2(80, 10)]
                : null,
        };

    static CutPackage Pkg(params Panel[] panels) => new()
    {
        SchemaName = CutPackage.Schema,
        JobId = "Club",
        Sheets =
        [
            new SheetStock { SheetId = "S15", Material = "carcass", ThicknessMm = 15, WidthMm = 1220, LengthMm = 2440 },
            new SheetStock { SheetId = "S145", Material = "carcass", ThicknessMm = 14.5, WidthMm = 1220, LengthMm = 2440 },
        ],
        Panels = panels,
    };

    static NestGroupKey K(double t) => NestGroupKey.From("carcass", t);

    [Fact]
    public void Merge_rewrites_through_and_full_slot_to_target_thickness()
    {
        var pkg = Pkg(
            Box("A", 15),
            Box("B", 14.5,
                features:
                [
                    Feat("H1", "holeVertical", 14.5, through: true),
                    Feat("S1", "grooveVertical", 14.5, through: true),
                ]));
        var merged = MaterialCorrect.MergeKinds(pkg, [K(15), K(14.5)], K(15), BlindFeatureDepthPolicy.Keep);
        var b = merged.Panels.Single(p => p.PanelId == "B");
        Assert.Equal(15, b.ThicknessMm);
        Assert.Equal("carcass", b.Material);
        Assert.Equal(15, b.Features.Single(f => f.FeatureId == "H1").DepthMm);
        Assert.True(b.Features.Single(f => f.FeatureId == "H1").Through);
        Assert.Equal(15, b.Features.Single(f => f.FeatureId == "S1").DepthMm);
        Assert.Single(merged.Sheets);
        Assert.Equal(15, merged.Sheets[0].ThicknessMm);
    }

    [Fact]
    public void Keep_leaves_half_slot_and_hinge_depth()
    {
        var pkg = Pkg(
            Box("A", 15),
            Box("B", 14.5,
                features:
                [
                    Feat("T1", "grooveVertical", 7.25, purpose: "tongue"),
                    Feat("C1", "holeVertical", 12, purpose: "hinge", diameter: 35),
                ]));
        var merged = MaterialCorrect.MergeKinds(pkg, [K(15), K(14.5)], K(15), BlindFeatureDepthPolicy.Keep);
        var b = merged.Panels.Single(p => p.PanelId == "B");
        Assert.Equal(7.25, b.Features.Single(f => f.FeatureId == "T1").DepthMm);
        Assert.Equal(12, b.Features.Single(f => f.FeatureId == "C1").DepthMm);
    }

    [Fact]
    public void Scale_adjusts_half_slot_and_hinge_with_thickness()
    {
        var pkg = Pkg(
            Box("A", 15),
            Box("B", 14.5,
                features:
                [
                    Feat("T1", "grooveVertical", 7.25, purpose: "tongue"),
                    Feat("C1", "holeVertical", 12, purpose: "hinge", diameter: 35),
                ]));
        var merged = MaterialCorrect.MergeKinds(pkg, [K(15), K(14.5)], K(15), BlindFeatureDepthPolicy.ScaleWithThickness);
        var b = merged.Panels.Single(p => p.PanelId == "B");
        Assert.Equal(7.5, b.Features.Single(f => f.FeatureId == "T1").DepthMm!.Value, 3);
        Assert.Equal(12 * 15 / 14.5, b.Features.Single(f => f.FeatureId == "C1").DepthMm!.Value, 3);
    }

    [Fact]
    public void HasHalfSlotOrHinge_false_when_only_through()
    {
        var panels = new[]
        {
            Box("B", 14.5, features: [Feat("H1", "holeVertical", 14.5, through: true)]),
        };
        Assert.False(MaterialCorrect.HasHalfSlotOrHinge(panels));
    }

    [Fact]
    public void Target_panels_are_left_alone()
    {
        var pkg = Pkg(Box("A", 15, features: [Feat("T1", "grooveVertical", 7.5, purpose: "tongue")]));
        var merged = MaterialCorrect.MergeKinds(pkg, [K(15), K(14.5)], K(15), BlindFeatureDepthPolicy.ScaleWithThickness);
        Assert.Equal(7.5, merged.Panels[0].Features[0].DepthMm);
        Assert.Equal(15, merged.Panels[0].ThicknessMm);
    }
}
