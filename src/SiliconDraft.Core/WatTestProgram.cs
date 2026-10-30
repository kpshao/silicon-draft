namespace SiliconDraft.Core;

/// <summary>
/// The effective WAT Test Program: the ordered Test Items measured at every PCM Site
/// on every Wafer.
/// </summary>
public sealed record WatProgram(string CatalogVersion, IReadOnlyList<WatTestItem> Items);

/// <summary>
/// A named WAT measurement with a business meaning, a unit, spec and test limits, and
/// the distribution its values are sampled from.
/// </summary>
public sealed record WatTestItem(
    int TestNumber,
    string Name,
    string Unit,
    Limits SpecLimits,
    Limits TestLimits,
    Distribution Distribution);

/// <summary>One WAT Test Item value at one PCM Site on one Wafer.</summary>
public sealed record WatMeasurement(PcmSite Site, WatTestItem Item, double Value);
