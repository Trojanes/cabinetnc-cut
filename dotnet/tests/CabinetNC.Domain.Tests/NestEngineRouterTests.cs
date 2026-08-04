using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class NestEngineRouterTests
{
    static Panel Rect(string id, string mat, double th) => new()
    {
        PanelId = id,
        Material = mat,
        ThicknessMm = th,
        Outline = new Outline
        {
            Points = [new(0, 0), new(200, 0), new(200, 150), new(0, 150)],
        },
    };

    static NestEngineRequest Req(string pref = "preferred") => new()
    {
        Panels = [Rect("A", "oak", 18), Rect("B", "oak", 18)],
        Settings = new NestSettings(),
        StockTemplates =
        [
            new NestSheetSpec
            {
                WidthMm = 1220, LengthMm = 2440, BorderMm = 15,
                Material = "oak", ThicknessMm = 18,
            },
        ],
        SizeOf = GroupedBlfNester.SizeOfOutline,
        EnginePreference = pref,
    };

    [Fact]
    public void Default_preferred_falls_back_to_blf_when_advanced_fails()
    {
        var router = new NestEngineRouter();
        var (result, log) = router.Run(Req("preferred"));
        Assert.Equal("blf_fallback", result.Engine);
        Assert.Equal("blf_fallback", log.SelectedEngine);
        Assert.Equal("advanced_stub_v0", log.AttemptedEngine);
        Assert.False(string.IsNullOrWhiteSpace(log.FallbackReason));
        Assert.Equal(2, result.Placements.Count);
    }

    [Fact]
    public void Explicit_blf_stays_grouped_blf()
    {
        var router = new NestEngineRouter();
        var (result, log) = router.Run(Req("blf"));
        Assert.Equal("grouped_blf_v0", result.Engine);
        Assert.Null(log.FallbackReason);
        Assert.Equal(2, result.Placements.Count);
    }

    [Fact]
    public void Advanced_timeout_also_falls_back()
    {
        var advanced = new AdvancedNestingEngineStub { AlwaysFail = false, Timeout = TimeSpan.Zero };
        var router = new NestEngineRouter(advanced: advanced);
        var req = Req("advanced");
        req = new NestEngineRequest
        {
            Panels = req.Panels,
            Settings = req.Settings,
            StockTemplates = req.StockTemplates,
            SizeOf = req.SizeOf,
            EnginePreference = "advanced",
            AdvancedTimeout = TimeSpan.FromMilliseconds(1),
        };
        var (result, log) = router.Run(req);
        Assert.Equal("blf_fallback", result.Engine);
        Assert.NotNull(log.FallbackReason);
    }
}
