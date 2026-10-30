namespace SiliconDraft.Core;

/// <summary>
/// The built-in minimal CP catalog. Every entry declares its business meaning via
/// its name and unit, physically plausible distribution defaults, and a default
/// failure Soft Bin. The catalog is versioned because exported data records it.
/// </summary>
internal static class CpCatalog
{
    public const string Version = "cp-catalog-v2";

    public static IReadOnlyList<CpCatalogItem> DefaultItems { get; } =
    [
        Numeric(1001, "Vt", "V", new Limits(0.39, 0.51), new Limits(0.40, 0.50),
            DistributionKind.Normal, mean: 0.45, sigma: 0.02, failSoftBin: 100,
            siteOffsets: [0, 0.0020, -0.0015, 0.0010, -0.0020, 0.0015, -0.0010, 0.0005]),
        Numeric(1002, "Idsat", "mA", new Limits(390, 510), new Limits(400, 500),
            DistributionKind.Normal, mean: 450, sigma: 20, failSoftBin: 101,
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
            DistributionKind.Normal, mean: 1.20, sigma: 0.05, failSoftBin: 105,
            siteOffsets: [0, 0.002, -0.0015, 0.001, -0.0025, 0.0018, -0.0012, 0.0008]),
        Functional(2001, "Scan", failSoftBin: 200, passRate: 0.995),
        Functional(2002, "Bist", failSoftBin: 201, passRate: 0.998),
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
