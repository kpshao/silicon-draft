namespace SiliconDraft.Core;

/// <summary>Builds catalog Test Items with the CP and FT catalogs' shared conventions.</summary>
internal static class TestItemFactory
{
    /// <summary>P/F values are sampled as 1 for pass and 0 for fail.</summary>
    internal const string PassFailUnit = "P/F";

    internal static readonly Limits PassFailSpec = new(0.5, 1.5);
    internal static readonly Limits PassFailTest = new(0.5, 1.5);

    public static TestItem Numeric(
        int testNumber,
        string name,
        string unit,
        Limits specLimits,
        Limits testLimits,
        int failSoftBin,
        Distribution distribution,
        IReadOnlyList<double>? siteOffsets = null) =>
        new NumericTestItem(
            testNumber,
            name,
            unit,
            specLimits,
            testLimits,
            failSoftBin,
            distribution,
            siteOffsets ?? []);

    public static TestItem Functional(
        int testNumber,
        string name,
        int failSoftBin,
        double passRate,
        Limits? specLimits = null,
        Limits? testLimits = null) =>
        new FunctionalTestItem(
            testNumber,
            name,
            PassFailUnit,
            specLimits ?? PassFailSpec,
            testLimits ?? PassFailTest,
            failSoftBin,
            passRate);

    /// <summary>
    /// Builds a Test Item the configuration defines outside a catalog. FT shares this
    /// entry point; its custom items carry an empty Site Offset list.
    /// </summary>
    public static TestItem Custom(CustomTestItem item) => item.Kind switch
    {
        TestItemKind.Numeric => Numeric(
            item.TestNumber,
            item.Name,
            item.Unit,
            item.SpecLimits,
            item.TestLimits,
            item.FailSoftBin,
            item.Distribution!,
            item.SiteOffsets),
        TestItemKind.Functional => Functional(
            item.TestNumber,
            item.Name,
            item.FailSoftBin,
            item.PassRate!.Value,
            item.SpecLimits,
            item.TestLimits),
        _ => throw new ArgumentOutOfRangeException(nameof(item)),
    };
}
