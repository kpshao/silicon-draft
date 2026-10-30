using System.Text.Json.Serialization;

namespace SiliconDraft.Core;

/// <summary>
/// Effective CP settings after defaults are applied: the Tester Site count, the
/// Hard/Soft Bin numbering, and the per-item overrides recorded in the Resolved Config.
/// </summary>
public sealed record CpConfig
{
    /// <summary>
    /// STDF reserves SITE_NUM 255 for "all sites", so a real Tester Site number is 1-254.
    /// </summary>
    public const int MinSiteCount = 1;

    public const int MaxSiteCount = 254;

    internal CpConfig(
        TestBins bins, IReadOnlyList<CpTestItemOverride> testItems, int siteCount)
    {
        Bins = bins;
        TestItems = testItems;
        SiteCount = siteCount;
    }

    public TestBins Bins { get; }

    public IReadOnlyList<CpTestItemOverride> TestItems { get; }

    /// <summary>Number of parallel Tester Sites the CP step runs; 8 by default.</summary>
    public int SiteCount { get; }
}

public sealed record CpTestItemOverride(
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Limits? SpecLimits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Limits? TestLimits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? FailSoftBin,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<double>? SiteOffsets);
