namespace SiliconDraft.Core;

/// <summary>
/// The built-in FT catalog. Every entry declares its business meaning via its name
/// and unit, physically plausible distribution defaults, and a default failure Soft Bin.
/// The catalog is versioned because exported data records it.
/// </summary>
internal static class FtCatalog
{
    public const string Version = "ft-catalog-v2";

    public static IReadOnlyList<TestItem> DefaultItems { get; } =
    [
        Numeric(4001, "Icc", "uA", new Limits(0, 21), new Limits(0, 18),
            DistributionKind.Lognormal, mean: 2.30, sigma: 0.25, failSoftBin: 300),
        Numeric(4002, "Voh", "V", new Limits(2.32, 2.68), new Limits(2.35, 2.65),
            DistributionKind.Normal, mean: 2.5, sigma: 0.06, failSoftBin: 301),
        Numeric(4003, "Vol", "V", new Limits(0, 0.35), new Limits(0, 0.325),
            DistributionKind.Normal, mean: 0.2, sigma: 0.05, failSoftBin: 302),
        Numeric(4004, "Ioh", "mA", new Limits(9.6, 14.4), new Limits(10, 14),
            DistributionKind.Normal, mean: 12, sigma: 0.8, failSoftBin: 303),
        Numeric(4005, "Iol", "mA", new Limits(9.6, 14.4), new Limits(10, 14),
            DistributionKind.Normal, mean: 12, sigma: 0.8, failSoftBin: 304),
        Numeric(4006, "Tr", "ns", new Limits(3.5, 6.5), new Limits(3.75, 6.25),
            DistributionKind.Normal, mean: 5, sigma: 0.5, failSoftBin: 305),
        Numeric(4007, "Tf", "ns", new Limits(3.5, 6.5), new Limits(3.75, 6.25),
            DistributionKind.Normal, mean: 5, sigma: 0.42, failSoftBin: 306),
        Numeric(4008, "Tpd", "ns", new Limits(4, 6), new Limits(4.25, 5.75),
            DistributionKind.Normal, mean: 5, sigma: 0.25, failSoftBin: 307),
        Numeric(4009, "Iih", "uA", new Limits(0, 10), new Limits(0, 8),
            DistributionKind.Lognormal, mean: 1.099, sigma: 0.3, failSoftBin: 308),
        Numeric(4010, "Iil", "uA", new Limits(0, 10), new Limits(0, 8),
            DistributionKind.Lognormal, mean: 1.099, sigma: 0.3, failSoftBin: 309),
        Numeric(4011, "Vil", "V", new Limits(0, 0.8), new Limits(0, 0.75),
            DistributionKind.Normal, mean: 0.4, sigma: 0.1, failSoftBin: 310),
        Numeric(4012, "Vih", "V", new Limits(1.7, 3.3), new Limits(1.8, 3.2),
            DistributionKind.Normal, mean: 2.5, sigma: 0.2, failSoftBin: 311),
        Numeric(4013, "Ron", "Ohm", new Limits(20, 80), new Limits(25, 75),
            DistributionKind.Normal, mean: 50, sigma: 8, failSoftBin: 312),
        Numeric(4014, "Cin", "pF", new Limits(8, 12), new Limits(8.5, 11.5),
            DistributionKind.Normal, mean: 10, sigma: 0.5, failSoftBin: 313),
        Numeric(4015, "Fmax", "MHz", new Limits(180, 220), new Limits(185, 215),
            DistributionKind.Normal, mean: 200, sigma: 5, failSoftBin: 314),
        Functional(5001, "ScanFT", failSoftBin: 400, passRate: 0.995),
        Functional(5002, "BistFT", failSoftBin: 401, passRate: 0.997),
        Functional(5003, "MbistFT", failSoftBin: 402, passRate: 0.997),
    ];

    public static TestItem? Find(string name) =>
        DefaultItems.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// FT shares the CP step's Hard/Soft Bin numbering so cross-step yield analysis sees
    /// one pass Bin and one failure taxonomy; only the failure Soft Bins are FT-specific.
    /// </summary>
    public static TestProgram CreateProgram(FtConfig config, TestBins bins)
    {
        var overrides = config.TestItems.ToDictionary(
            item => item.Name, StringComparer.Ordinal);
        var items = DefaultItems
            .Select(item => ApplyOverride(item, overrides.GetValueOrDefault(item.Name)))
            .Concat(config.CustomTestItems.Select(TestItemFactory.Custom))
            .ToArray();
        return new TestProgram(Version, items, bins, SiteCount: 1);
    }

    private static TestItem ApplyOverride(TestItem item, FtTestItemOverride? itemOverride) =>
        item switch
        {
            NumericTestItem numeric => TestItemFactory.Numeric(
                numeric.TestNumber,
                numeric.Name,
                numeric.Unit,
                itemOverride?.SpecLimits ?? numeric.SpecLimits,
                itemOverride?.TestLimits ?? numeric.TestLimits,
                itemOverride?.FailSoftBin ?? numeric.FailSoftBin,
                numeric.Distribution),
            FunctionalTestItem functional => TestItemFactory.Functional(
                functional.TestNumber,
                functional.Name,
                itemOverride?.FailSoftBin ?? functional.FailSoftBin,
                functional.PassRate,
                itemOverride?.SpecLimits ?? functional.SpecLimits,
                itemOverride?.TestLimits ?? functional.TestLimits),
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

    private static TestItem Numeric(
        int testNumber,
        string name,
        string unit,
        Limits specLimits,
        Limits testLimits,
        DistributionKind kind,
        double mean,
        double sigma,
        int failSoftBin) =>
        TestItemFactory.Numeric(
            testNumber,
            name,
            unit,
            specLimits,
            testLimits,
            failSoftBin,
            new Distribution(kind, mean, sigma));

    private static TestItem Functional(
        int testNumber, string name, int failSoftBin, double passRate) =>
        TestItemFactory.Functional(testNumber, name, failSoftBin, passRate);
}
