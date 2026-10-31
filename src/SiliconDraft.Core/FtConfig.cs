using System.Text.Json.Serialization;

namespace SiliconDraft.Core;

/// <summary>
/// Effective FT settings after defaults are applied: the Assembly Loss applied per Lot,
/// the Escape rate admitted from CP-failing Die, and the Test Item overrides and custom
/// items. Recorded in the Resolved Config.
/// </summary>
public sealed record FtConfig
{
    internal FtConfig(
        int assemblyLoss,
        double escapeRate,
        IReadOnlyList<FtTestItemOverride> testItems,
        IReadOnlyList<CustomTestItem> customTestItems)
    {
        AssemblyLoss = assemblyLoss;
        EscapeRate = escapeRate;
        TestItems = testItems;
        CustomTestItems = customTestItems;
    }

    /// <summary>CP-passing Die per Lot that never become FT Units.</summary>
    public int AssemblyLoss { get; }

    /// <summary>Probability that a CP-failing Die nevertheless becomes an FT Unit.</summary>
    public double EscapeRate { get; }

    /// <summary>Overrides applied to catalog FT Test Items, in configuration order.</summary>
    public IReadOnlyList<FtTestItemOverride> TestItems { get; }

    /// <summary>FT Test Items the configuration adds beyond the catalog.</summary>
    public IReadOnlyList<CustomTestItem> CustomTestItems { get; }

    internal static FtConfig Default { get; } = new(0, 0, [], []);
}

/// <summary>An override of a catalog FT Test Item; unset fields keep the catalog value.</summary>
public sealed record FtTestItemOverride(
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Limits? SpecLimits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Limits? TestLimits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? FailSoftBin);
