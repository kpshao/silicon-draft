namespace SiliconDraft.Core;

/// <summary>
/// The built-in CP catalog. Every entry declares its business meaning via its name
/// and unit, physically plausible distribution defaults, and a default failure Soft Bin.
/// The catalog is versioned because exported data records it.
/// </summary>
internal static class CpCatalog
{
    public const string Version = "cp-catalog-v4";

    public static IReadOnlyList<CpCatalogItem> DefaultItems { get; } =
    [
        Numeric(1001, "Vt", "V", new Limits(0.39, 0.51), new Limits(0.40, 0.50),
            DistributionKind.Normal, mean: 0.45, sigma: 0.02, failSoftBin: 100,
            siteOffsets: [0, 0.0020, -0.0015, 0.0010, -0.0020, 0.0015, -0.0010, 0.0005]),
        Numeric(1002, "Idsat", "mA", new Limits(390, 510), new Limits(400, 500),
            DistributionKind.Lognormal, mean: 6.1092475827643655, sigma: 0.044444444444444446,
            failSoftBin: 101,
            siteOffsets: [0, 1.0, -0.8, 0.6, -1.1, 0.9, -0.5, 0.3]),
        Numeric(1003, "Iddq", "uA", new Limits(0, 35), new Limits(0, 30),
            DistributionKind.Lognormal, mean: 2.485, sigma: 0.20, failSoftBin: 102,
            siteOffsets: [0, 0.15, -0.12, 0.08, -0.18, 0.10, -0.06, 0.04]),
        Numeric(1004, "RdsOn", "mOhm", new Limits(73, 97), new Limits(75, 95),
            DistributionKind.Normal, mean: 85, sigma: 4, failSoftBin: 103,
            siteOffsets: [0, 0.25, -0.2, 0.15, -0.3, 0.2, -0.1, 0.08]),
        Numeric(1005, "Leakage", "nA", new Limits(0, 25), new Limits(0, 20),
            DistributionKind.Lognormal, mean: 1.609, sigma: 0.35, failSoftBin: 104,
            siteOffsets: [0, 0.08, -0.06, 0.05, -0.1, 0.07, -0.04, 0.03]),
        Numeric(1006, "Vf", "V", new Limits(1.08, 1.32), new Limits(1.10, 1.30),
            DistributionKind.Normal, mean: 1.20, sigma: 0.03, failSoftBin: 105,
            siteOffsets: [0, 0.002, -0.0015, 0.001, -0.0025, 0.0018, -0.0012, 0.0008]),
        Numeric(1007, "Vtp", "V", new Limits(-0.51, -0.39), new Limits(-0.50, -0.40),
            DistributionKind.Normal, mean: -0.45, sigma: 0.016, failSoftBin: 106,
            siteOffsets: [0, -0.0012, 0.0009, -0.0006, 0.0012, -0.0009, 0.0006, -0.0003]),
        Numeric(1008, "IdsatP", "mA", new Limits(190, 250), new Limits(200, 240),
            DistributionKind.Lognormal, mean: 5.393627546352362, sigma: 0.029545454545454545,
            failSoftBin: 107,
            siteOffsets: [0, -0.5, 0.4, -0.3, 0.6, -0.45, 0.3, -0.2]),
        Numeric(1009, "Vtlin", "V", new Limits(0.42, 0.58), new Limits(0.44, 0.56),
            DistributionKind.Normal, mean: 0.50, sigma: 0.02, failSoftBin: 108,
            siteOffsets: [0, 0.0015, -0.0012, 0.0009, -0.0018, 0.0013, -0.0009, 0.0006]),
        Numeric(1010, "Vtsat", "V", new Limits(0.36, 0.50), new Limits(0.38, 0.48),
            DistributionKind.Normal, mean: 0.43, sigma: 0.0165, failSoftBin: 109,
            siteOffsets: [0, 0.0012, -0.0010, 0.0007, -0.0015, 0.0011, -0.0007, 0.0005]),
        Numeric(1011, "Gm", "uS", new Limits(900, 1100), new Limits(920, 1080),
            DistributionKind.Normal, mean: 1000, sigma: 26, failSoftBin: 110,
            siteOffsets: [0, 2.0, -1.6, 1.2, -2.4, 1.8, -1.0, 0.6]),
        Numeric(1012, "Ioff", "pA", new Limits(0, 200), new Limits(0, 150),
            DistributionKind.Lognormal, mean: 2.996, sigma: 0.5, failSoftBin: 111,
            siteOffsets: [0, 1.0, -0.8, 0.6, -1.2, 0.9, -0.5, 0.3]),
        Numeric(1013, "Vbd", "V", new Limits(10, 14), new Limits(10.5, 13.5),
            DistributionKind.Normal, mean: 12, sigma: 0.5, failSoftBin: 112,
            siteOffsets: [0, 0.04, -0.03, 0.02, -0.05, 0.035, -0.02, 0.015]),
        Numeric(1014, "Cox", "fF", new Limits(38, 52), new Limits(40, 50),
            DistributionKind.Normal, mean: 45, sigma: 1.65, failSoftBin: 113,
            siteOffsets: [0, 0.12, -0.10, 0.08, -0.15, 0.11, -0.07, 0.04]),
        Numeric(1015, "Cin", "pF", new Limits(1.8, 2.2), new Limits(1.85, 2.15),
            DistributionKind.Normal, mean: 2.0, sigma: 0.05, failSoftBin: 114,
            siteOffsets: [0, 0.004, -0.003, 0.002, -0.005, 0.0035, -0.002, 0.0015]),
        Numeric(1016, "Rs", "Ohm/sq", new Limits(100, 140), new Limits(105, 135),
            DistributionKind.Normal, mean: 120, sigma: 5, failSoftBin: 115,
            siteOffsets: [0, 0.4, -0.3, 0.2, -0.5, 0.35, -0.2, 0.15]),
        Numeric(1017, "Rc", "Ohm", new Limits(0, 8), new Limits(0, 6),
            DistributionKind.Lognormal, mean: 1.253, sigma: 0.2, failSoftBin: 116,
            siteOffsets: [0, 0.02, -0.016, 0.012, -0.024, 0.018, -0.012, 0.008]),
        Numeric(1018, "Ssw", "mV/dec", new Limits(70, 90), new Limits(72, 88),
            DistributionKind.Normal, mean: 80, sigma: 2.6, failSoftBin: 117,
            siteOffsets: [0, 0.2, -0.16, 0.12, -0.25, 0.18, -0.1, 0.06]),
        Numeric(1019, "Dibl", "mV/V", new Limits(30, 70), new Limits(35, 65),
            DistributionKind.Normal, mean: 50, sigma: 5, failSoftBin: 118,
            siteOffsets: [0, 0.4, -0.3, 0.2, -0.5, 0.35, -0.2, 0.15]),
        Numeric(1020, "Vfb", "V", new Limits(-0.95, -0.75), new Limits(-0.92, -0.78),
            DistributionKind.Normal, mean: -0.85, sigma: 0.023, failSoftBin: 119,
            siteOffsets: [0, 0.0018, -0.0014, 0.001, -0.0022, 0.0016, -0.001, 0.0006]),
        Functional(2001, "Scan", failSoftBin: 200, passRate: 0.995),
        Functional(2002, "Bist", failSoftBin: 201, passRate: 0.998),
        Functional(2003, "Mbist", failSoftBin: 202, passRate: 0.997),
    ];

    public static CpCatalogItem? Find(string name) =>
        DefaultItems.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));

    public static TestProgram Resolve(CpConfig config)
    {
        var overrides = new Dictionary<string, CpTestItemOverride>(StringComparer.Ordinal);
        foreach (var itemOverride in config.TestItems)
        {
            overrides[itemOverride.Name] = itemOverride;
        }

        var items = DefaultItems
            .Select(item => item.Create(
                overrides.GetValueOrDefault(item.Name), config.SiteCount))
            .Concat(config.CustomTestItems.Select(TestItemFactory.Custom))
            .ToArray();
        return new TestProgram(Version, items, config.Bins, config.SiteCount);
    }

    private static CpCatalogItem Numeric(
        int testNumber,
        string name,
        string unit,
        Limits specLimits,
        Limits testLimits,
        DistributionKind kind,
        double mean,
        double sigma,
        int failSoftBin,
        IReadOnlyList<double> siteOffsets) =>
        new NumericCatalogItem(
            new CpCatalogItemDefinition(
                testNumber, name, unit, specLimits, testLimits, failSoftBin, siteOffsets),
            new Distribution(kind, mean, sigma));

    private static CpCatalogItem Functional(
        int testNumber, string name, int failSoftBin, double passRate) =>
        new FunctionalCatalogItem(
            new CpCatalogItemDefinition(
                testNumber,
                name,
                TestItemFactory.PassFailUnit,
                TestItemFactory.PassFailSpec,
                TestItemFactory.PassFailTest,
                failSoftBin,
                []),
            passRate);
}

/// <summary>The fields every catalog entry shares; overrides replace limits and Soft Bin.</summary>
internal sealed record CpCatalogItemDefinition(
    int TestNumber,
    string Name,
    string Unit,
    Limits SpecLimits,
    Limits TestLimits,
    int FailSoftBin,
    IReadOnlyList<double> SiteOffsets);

internal abstract record CpCatalogItem(CpCatalogItemDefinition Definition)
{
    public string Name => Definition.Name;

    public int FailSoftBin => Definition.FailSoftBin;

    public abstract TestItem Create(CpTestItemOverride? itemOverride, int siteCount);
}

internal sealed record NumericCatalogItem(
    CpCatalogItemDefinition Definition,
    Distribution Distribution)
    : CpCatalogItem(Definition)
{
    public override TestItem Create(CpTestItemOverride? itemOverride, int siteCount) =>
        TestItemFactory.Numeric(
            Definition.TestNumber,
            Definition.Name,
            Definition.Unit,
            itemOverride?.SpecLimits ?? Definition.SpecLimits,
            itemOverride?.TestLimits ?? Definition.TestLimits,
            itemOverride?.FailSoftBin ?? Definition.FailSoftBin,
            Distribution,
            itemOverride?.SiteOffsets ?? ExpandSiteOffsets(Definition.SiteOffsets, siteCount));

    /// <summary>
    /// Repeats the catalog's site-offset pattern across however many sites are configured,
    /// so a site count other than the default eight still has a plausible default offset.
    /// </summary>
    private static double[] ExpandSiteOffsets(
        IReadOnlyList<double> pattern, int siteCount)
    {
        if (pattern.Count == 0)
        {
            return [];
        }

        var offsets = new double[siteCount];
        for (var index = 0; index < siteCount; index++)
        {
            offsets[index] = pattern[index % pattern.Count];
        }

        return offsets;
    }
}

internal sealed record FunctionalCatalogItem(
    CpCatalogItemDefinition Definition,
    double PassRate)
    : CpCatalogItem(Definition)
{
    public override TestItem Create(CpTestItemOverride? itemOverride, int siteCount) =>
        TestItemFactory.Functional(
            Definition.TestNumber,
            Definition.Name,
            itemOverride?.FailSoftBin ?? Definition.FailSoftBin,
            PassRate,
            itemOverride?.SpecLimits,
            itemOverride?.TestLimits);
}
