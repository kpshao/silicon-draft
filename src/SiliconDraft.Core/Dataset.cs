namespace SiliconDraft.Core;

/// <summary>
/// One complete generation output: the data for a set of lots plus its ground-truth manifest,
/// produced from one configuration and one seed.
/// </summary>
public sealed class Dataset
{
    internal Dataset(ResolvedConfig config, CpProgram testProgram, IReadOnlyList<Lot> lots)
    {
        Config = config;
        TestProgram = testProgram;
        Lots = lots;
        DieMap = lots[0].Wafers[0].DieMap;
        DieTotals = DieCounts.Sum(
            lots.SelectMany(lot => lot.Wafers).Select(wafer => wafer.DieMap.Counts));
        Cp = CpSummary.Create(
            testProgram,
            lots.SelectMany(lot => lot.Wafers)
                .SelectMany(wafer => wafer.CpResults)
                .ToArray());
    }

    public ResolvedConfig Config { get; }

    /// <summary>The effective CP Test Program applied to every wafer.</summary>
    public CpProgram TestProgram { get; }

    public string ProductName => Config.Product.Name;

    public long Seed => Config.Seed;

    public IReadOnlyList<Lot> Lots { get; }

    /// <summary>The die map shared by every wafer; wafer geometry is dataset-wide.</summary>
    public DieMap DieMap { get; }

    public DieCounts DieTotals { get; }

    /// <summary>CP outcomes for the whole Dataset.</summary>
    public CpSummary Cp { get; }

    public int LotCount => Lots.Count;

    public int WaferCount => Lots.Sum(lot => lot.Wafers.Count);
}

public sealed record Lot(string Id, IReadOnlyList<Wafer> Wafers)
{
    public CpSummary GetCpSummary(CpProgram program) =>
        CpSummary.Create(
            program,
            Wafers.SelectMany(wafer => wafer.CpResults).ToArray());
}

public sealed record Wafer(string Id, DieMap DieMap, IReadOnlyList<CpDieResult> CpResults)
{
    /// <summary>Number of valid Die that pass CP.</summary>
    public int NetDie => CpResults.Count(result => result.Passed);
}
