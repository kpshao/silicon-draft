namespace SiliconDraft.Core;

/// <summary>
/// The built-in WAT catalog. Every entry declares its business meaning via its name
/// and unit, physically plausible distribution defaults, and spec and test limits.
/// The catalog is versioned because exported data records it.
/// </summary>
internal static class WatCatalog
{
    public const string Version = "wat-catalog-v3";

    public static IReadOnlyList<WatTestItem> DefaultItems { get; } =
    [
        Item(3001, "Vtn", "V", new Limits(0.39, 0.51), new Limits(0.40, 0.50),
            DistributionKind.Normal, mean: 0.45, sigma: 0.02),
        Item(3002, "Vtp", "V", new Limits(-0.51, -0.39), new Limits(-0.50, -0.40),
            DistributionKind.Normal, mean: -0.45, sigma: 0.02),
        Item(3003, "IdsatN", "mA", new Limits(390, 510), new Limits(400, 500),
            DistributionKind.Lognormal, mean: 6.1092475827643655,
            sigma: 0.044444444444444446),
        Item(3004, "IdsatP", "mA", new Limits(190, 250), new Limits(200, 240),
            DistributionKind.Lognormal, mean: 5.393627546352362,
            sigma: 0.029545454545454545),
        Item(3005, "Tox", "nm", new Limits(1.9, 2.1), new Limits(1.92, 2.08),
            DistributionKind.Normal, mean: 2.0, sigma: 0.03),
        Item(3006, "Rs", "Ohm/sq", new Limits(100, 140), new Limits(105, 135),
            DistributionKind.Normal, mean: 120, sigma: 5),
        Item(3007, "Rc", "Ohm", new Limits(0, 8), new Limits(0, 6),
            DistributionKind.Lognormal, mean: 1.204, sigma: 0.15),
        Item(3008, "Vbd", "V", new Limits(10, 14), new Limits(10.5, 13.5),
            DistributionKind.Normal, mean: 12, sigma: 0.5),
        Item(3009, "IoffN", "pA", new Limits(0, 200), new Limits(0, 150),
            DistributionKind.Lognormal, mean: 2.996, sigma: 0.5),
        Item(3010, "IoffP", "pA", new Limits(0, 200), new Limits(0, 150),
            DistributionKind.Lognormal, mean: 2.890, sigma: 0.5),
        Item(3011, "Ssw", "mV/dec", new Limits(70, 90), new Limits(72, 88),
            DistributionKind.Normal, mean: 80, sigma: 2.6),
        Item(3012, "Vfb", "V", new Limits(-0.95, -0.75), new Limits(-0.92, -0.78),
            DistributionKind.Normal, mean: -0.85, sigma: 0.023),
    ];

    public static WatTestItem? Find(string name) =>
        DefaultItems.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));

    public static WatProgram CreateProgram(WatConfig config)
    {
        var overrides = config.TestItems.ToDictionary(
            item => item.Name, StringComparer.Ordinal);
        var items = DefaultItems
            .Select(item => ApplyOverride(item, overrides.GetValueOrDefault(item.Name)))
            .Concat(config.CustomTestItems)
            .ToArray();
        return new WatProgram(Version, items);
    }

    private static WatTestItem ApplyOverride(WatTestItem item, WatTestItemOverride? itemOverride) =>
        itemOverride is null
            ? item
            : item with
            {
                SpecLimits = itemOverride.SpecLimits ?? item.SpecLimits,
                TestLimits = itemOverride.TestLimits ?? item.TestLimits,
                Distribution = itemOverride.Distribution ?? item.Distribution,
            };

    private static WatTestItem Item(
        int testNumber,
        string name,
        string unit,
        Limits specLimits,
        Limits testLimits,
        DistributionKind kind,
        double mean,
        double sigma) =>
        new(testNumber, name, unit, specLimits, testLimits, new Distribution(kind, mean, sigma));
}
