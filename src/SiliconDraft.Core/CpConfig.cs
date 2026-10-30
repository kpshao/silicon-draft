using System.Text.Json.Serialization;

namespace SiliconDraft.Core;

/// <summary>
/// Effective CP settings after defaults are applied: the Hard/Soft Bin numbering and
/// the per-item overrides recorded in the Resolved Config.
/// </summary>
public sealed record CpConfig
{
    internal CpConfig(TestBins bins, IReadOnlyList<CpTestItemOverride> testItems)
    {
        Bins = bins;
        TestItems = testItems;
    }

    public TestBins Bins { get; }

    public IReadOnlyList<CpTestItemOverride> TestItems { get; }
}

public sealed record TestBins(
    int PassHardBin,
    int PassSoftBin,
    int ParametricFailHardBin,
    int FunctionalFailHardBin)
{
    internal static TestBins Default { get; } = new(1, 1, 2, 3);
}

public sealed record CpTestItemOverride(
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Limits? SpecLimits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Limits? TestLimits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? FailSoftBin);
