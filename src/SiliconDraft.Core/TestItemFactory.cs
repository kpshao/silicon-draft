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
        Distribution distribution) =>
        new NumericTestItem(
            testNumber, name, unit, specLimits, testLimits, failSoftBin, distribution);

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
}
