using System.Text.Json.Serialization;

namespace SiliconDraft.Core;

/// <summary>
/// Effective CP settings after defaults are applied: the Hard/Soft Bin numbering and
/// the per-item overrides recorded in the Resolved Config.
/// </summary>
public sealed record CpConfig
{
    internal CpConfig(CpBins bins, IReadOnlyList<CpTestItemOverride> testItems)
    {
        Bins = bins;
        TestItems = testItems;
    }

    public CpBins Bins { get; }

    public IReadOnlyList<CpTestItemOverride> TestItems { get; }
}

public sealed record CpBins(
    int PassHardBin,
    int PassSoftBin,
    int ParametricFailHardBin,
    int FunctionalFailHardBin)
{
    internal static CpBins Default { get; } = new(1, 1, 2, 3);
}

public sealed record CpTestItemOverride(
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CpLimits? SpecLimits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CpLimits? TestLimits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? FailSoftBin);

/// <summary>Inclusive lower and upper limits for one Test Item.</summary>
public sealed record CpLimits(double Low, double High);
