using System.Text.Json.Serialization;

namespace SiliconDraft.Core;

/// <summary>The kind of measurement a Test Item produces.</summary>
public enum TestItemKind
{
    Numeric,
    Functional,
}

/// <summary>
/// A Test Item the configuration adds beyond the built-in CP or FT catalog. Its complete
/// definition is recorded in the Resolved Config so the Dataset can be reproduced.
/// Site Offsets are empty for FT, which runs on a single Tester Site.
/// </summary>
public sealed record CustomTestItem(
    int TestNumber,
    string Name,
    string Unit,
    Limits SpecLimits,
    Limits TestLimits,
    int FailSoftBin,
    TestItemKind Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Distribution? Distribution,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? PassRate,
    IReadOnlyList<double> SiteOffsets);
