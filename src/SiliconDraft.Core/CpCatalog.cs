namespace SiliconDraft.Core;

/// <summary>
/// The built-in minimal CP catalog. Every entry declares its business meaning via
/// its name and unit, physically plausible distribution defaults, and a default
/// failure Soft Bin. The catalog is versioned because exported data records it.
/// </summary>
internal static class CpCatalog
{
    public const string Version = "cp-catalog-v1";

    /// <summary>P/F values are sampled as 1 for pass and 0 for fail.</summary>
    private const string PassFailUnit = "P/F";

    private static readonly Limits PassFailSpec = new(0.5, 1.5);
    private static readonly Limits PassFailTest = new(0.5, 1.5);

    public static IReadOnlyList<CpCatalogItem> DefaultItems { get; } =
    [
        Numeric(1001, "Vt", "V", new Limits(0.39, 0.51), new Limits(0.40, 0.50),
            DistributionKind.Normal, mean: 0.45, sigma: 0.02, failSoftBin: 100),
        Numeric(1002, "Idsat", "mA", new Limits(390, 510), new Limits(400, 500),
            DistributionKind.Normal, mean: 450, sigma: 20, failSoftBin: 101),
        Numeric(1003, "Iddq", "uA", new Limits(0, 35), new Limits(0, 30),
            DistributionKind.Lognormal, mean: 2.485, sigma: 0.20, failSoftBin: 102),
        Numeric(1004, "RdsOn", "mOhm", new Limits(73, 97), new Limits(75, 95),
            DistributionKind.Normal, mean: 85, sigma: 4, failSoftBin: 103),
        Numeric(1005, "Leakage", "nA", new Limits(0, 25), new Limits(0, 20),
            DistributionKind.Lognormal, mean: 1.609, sigma: 0.35, failSoftBin: 104),
        Numeric(1006, "Vf", "V", new Limits(1.08, 1.32), new Limits(1.10, 1.30),
            DistributionKind.Normal, mean: 1.20, sigma: 0.05, failSoftBin: 105),
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
            .Select(item => item.Create(overrides.GetValueOrDefault(item.Name)))
            .ToArray();
        return new TestProgram(Version, items, config.Bins);
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
        int failSoftBin) =>
        new NumericCatalogItem(
            new CpCatalogItemDefinition(testNumber, name, unit, specLimits, testLimits, failSoftBin),
            new Distribution(kind, mean, sigma));

    private static CpCatalogItem Functional(
        int testNumber, string name, int failSoftBin, double passRate) =>
        new FunctionalCatalogItem(
            new CpCatalogItemDefinition(
                testNumber, name, PassFailUnit, PassFailSpec, PassFailTest, failSoftBin),
            passRate);
}

/// <summary>The fields every catalog entry shares; overrides replace limits and Soft Bin.</summary>
internal sealed record CpCatalogItemDefinition(
    int TestNumber,
    string Name,
    string Unit,
    Limits SpecLimits,
    Limits TestLimits,
    int FailSoftBin);

internal abstract record CpCatalogItem(CpCatalogItemDefinition Definition)
{
    public string Name => Definition.Name;

    public int FailSoftBin => Definition.FailSoftBin;

    public abstract TestItem Create(CpTestItemOverride? itemOverride);
}

internal sealed record NumericCatalogItem(
    CpCatalogItemDefinition Definition,
    Distribution Distribution)
    : CpCatalogItem(Definition)
{
    public override TestItem Create(CpTestItemOverride? itemOverride) => new NumericTestItem(
        Definition.TestNumber,
        Definition.Name,
        Definition.Unit,
        itemOverride?.SpecLimits ?? Definition.SpecLimits,
        itemOverride?.TestLimits ?? Definition.TestLimits,
        itemOverride?.FailSoftBin ?? Definition.FailSoftBin,
        Distribution);
}

internal sealed record FunctionalCatalogItem(
    CpCatalogItemDefinition Definition,
    double PassRate)
    : CpCatalogItem(Definition)
{
    public override TestItem Create(CpTestItemOverride? itemOverride) => new FunctionalTestItem(
        Definition.TestNumber,
        Definition.Name,
        Definition.Unit,
        itemOverride?.SpecLimits ?? Definition.SpecLimits,
        itemOverride?.TestLimits ?? Definition.TestLimits,
        itemOverride?.FailSoftBin ?? Definition.FailSoftBin,
        PassRate);
}
