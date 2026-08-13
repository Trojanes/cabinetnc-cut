namespace CabinetNC.Domain.Manufacturing;

/// <summary>
/// Shop post numbers from the stage-4 panes. When passed to <see cref="NcEmitter"/>,
/// Z0 is the board bottom / spoilboard top (Troy), safe Z is 30, and profile is two passes.
/// </summary>
public sealed class PostRecipe
{
    public double SafeZMm { get; init; } = TroyRecipe.SafeZMm;
    public bool Z0IsBoardBottom { get; init; } = true;

    public double TongueFeed { get; init; } = TroyRecipe.TongueFeedMmMin;
    public double TongueRpm { get; init; } = TroyRecipe.SpindleRpm;
    public double TonguePlunge { get; init; } = TroyRecipe.PlungeFeedMmMin;

    public double ClearanceFeed { get; init; } = TroyRecipe.WorkFirstFeedMmMin;
    public double ClearanceRpm { get; init; } = TroyRecipe.SpindleRpm;
    public double ClearancePlunge { get; init; } = TroyRecipe.PlungeFeedMmMin;

    public double ProfileFirstFeed { get; init; } = TroyRecipe.WorkFirstFeedMmMin;
    public double ProfileFirstRpm { get; init; } = TroyRecipe.SpindleRpm;
    public double ProfileFirstPlunge { get; init; } = TroyRecipe.PlungeFeedMmMin;
    public bool ProfileFirstRamp45 { get; init; }
    public double ProfileFirstLeaveMm { get; init; } = TroyRecipe.LastPassLeaveMm;

    public double ProfileLastFeed { get; init; } = TroyRecipe.WorkLastFeedMmMin;
    public double ProfileLastRpm { get; init; } = TroyRecipe.SpindleRpm;
    public double ProfileLastPlunge { get; init; } = TroyRecipe.PlungeFeedMmMin;
    public double ProfileThroughZMm { get; init; } = TroyRecipe.ThroughZMm;

    public double DrillPlunge { get; init; } = TroyRecipe.PlungeFeedMmMin;
    public double DrillRpm { get; init; } = TroyRecipe.SpindleRpm;
    public double DrillThroughZMm { get; init; } = TroyRecipe.ThroughZMm;

    /// <summary>After the last retract, emit <c>G0 X0 Y0</c> before G80. Default on.</summary>
    public bool HomeXyAtEnd { get; init; } = true;

    public IReadOnlyList<ProfileBridge> Bridges { get; init; } = [];

    public static PostRecipe TroyDefault() => new();
}
