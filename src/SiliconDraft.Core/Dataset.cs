namespace SiliconDraft.Core;

/// <summary>
/// One complete generation output: the data for a set of lots plus its ground-truth manifest,
/// produced from one configuration and one seed.
/// </summary>
public sealed class Dataset
{
    internal Dataset(ResolvedConfig config, IReadOnlyList<Lot> lots)
    {
        Config = config;
        Lots = lots;
    }

    public ResolvedConfig Config { get; }

    public string ProductName => Config.Product.Name;

    public long Seed => Config.Seed;

    public IReadOnlyList<Lot> Lots { get; }

    public int LotCount => Lots.Count;

    public int WaferCount => Lots.Sum(lot => lot.Wafers.Count);
}

public sealed record Lot(string Id, IReadOnlyList<Wafer> Wafers);

public sealed record Wafer(string Id, DieMap DieMap);
