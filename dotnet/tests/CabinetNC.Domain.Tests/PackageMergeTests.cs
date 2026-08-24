using CabinetNC.Domain;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Materials;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class PackageMergeTests
{
    static Panel Box(string id, string name, string? module = null, double x = 0) => new()
    {
        PanelId = id,
        Name = name,
        ThicknessMm = 18,
        Outline = new Outline
        {
            Points = [new Point2(x, 0), new Point2(x + 100, 0), new Point2(x + 100, 50), new Point2(x, 50)],
            Closed = true,
        },
        Identity = module is null ? null : new WorkpieceIdentity { ModuleId = module, WorkpieceId = id },
    };

    static CutPackage Pkg(string job, params Panel[] panels) => new()
    {
        SchemaName = CutPackage.Schema,
        JobId = job,
        Sheets =
        [
            new SheetStock { SheetId = "S1", Material = "carcass", ThicknessMm = 18, WidthMm = 1220, LengthMm = 2440 },
        ],
        Panels = panels,
    };

    [Fact]
    public void Stamp_sets_package_without_rewriting_ids()
    {
        var stamped = PackageMerge.Stamp(Pkg("Kitchen", Box("Door.Left", "Door-Left", "Door")), "Kitchen", "Kitchen");
        Assert.Equal("Door.Left", stamped.Panels[0].PanelId);
        Assert.Equal("Kitchen", stamped.Panels[0].Identity!.PackageId);
        Assert.Equal("Kitchen", stamped.Panels[0].DisplayPackage);
        Assert.Equal("Door", stamped.Panels[0].DisplayAssembly);
    }

    [Fact]
    public void Merge_keeps_both_packages_and_prefixes_incoming_ids()
    {
        var a = PackageMerge.Stamp(Pkg("Kitchen", Box("Door.Left", "Kitchen-Left", "Carcass")), "Kitchen", "Kitchen");
        var b = Pkg("Fridge", Box("Door.Left", "Fridge-Left", "Door"));
        var merged = PackageMerge.Merge(a, b, "Fridge", "Fridge");
        Assert.Equal(2, merged.Panels.Count);
        Assert.Equal("Door.Left", merged.Panels[0].PanelId);
        Assert.Equal("Fridge/Door.Left", merged.Panels[1].PanelId);
        Assert.Equal("Kitchen", merged.Panels[0].DisplayPackage);
        Assert.Equal("Fridge", merged.Panels[1].DisplayPackage);
        Assert.Equal("Carcass", merged.Panels[0].DisplayAssembly);
        Assert.Equal("Door", merged.Panels[1].DisplayAssembly);
        Assert.Equal(2, merged.Panels.Select(p => p.DisplayPackage).Distinct().Count());
    }

    [Fact]
    public void Stock_groups_same_size_and_name_from_two_packages()
    {
        var a = PackageMerge.Stamp(Pkg("Kitchen", Box("Door.Left", "Carcass-Left", "Carcass")), "Kitchen", "Kitchen");
        var b = Pkg("Fridge", Box("Door.Left", "Carcass-Left", "Carcass"));
        var merged = PackageMerge.Merge(a, b, "Fridge", "Fridge");
        var groups = PackageMerge.GroupIdenticalStock(merged.Panels);
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Count);
        Assert.Equal(PackageMerge.StockKey(merged.Panels[0]), PackageMerge.StockKey(merged.Panels[1]));
    }

    [Fact]
    public void Merge_does_not_duplicate_same_material_sheet()
    {
        var a = PackageMerge.Stamp(Pkg("A", Box("A1", "A-Side")), "A", "A");
        var b = Pkg("B", Box("B1", "B-Side"));
        var merged = PackageMerge.Merge(a, b, "B", "B");
        Assert.Single(merged.Sheets);
    }
}
