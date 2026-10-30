namespace SiliconDraft.Core;

/// <summary>
/// The built-in minimal FT catalog. Every entry declares its business meaning via its
/// name and unit, physically plausible distribution defaults, and a default failure
/// Soft Bin. The catalog is versioned because exported data records it.
/// </summary>
internal static class FtCatalog
{
    public const string Version = "ft-catalog-v1";

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
        Functional(5001, "ScanFT", failSoftBin: 400, passRate: 0.995),
        Functional(5002, "BistFT", failSoftBin: 401, passRate: 0.997),
    ];

    /// <summary>
    /// FT shares the CP step's Hard/Soft Bin numbering so cross-step yield analysis sees
    /// one pass Bin and one failure taxonomy; only the failure Soft Bins are FT-specific.
    /// </summary>
    public static TestProgram CreateProgram(TestBins bins) =>
        new(Version, DefaultItems, bins);

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
