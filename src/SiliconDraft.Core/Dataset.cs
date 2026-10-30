namespace SiliconDraft.Core;

/// <summary>
/// One complete generation output: the data for a set of lots plus its ground-truth manifest,
/// produced from one configuration and one seed.
/// </summary>
public sealed class Dataset
{
    internal Dataset(
        ResolvedConfig config,
        TestProgram cpProgram,
        TestProgram ftProgram,
        WatProgram watProgram,
        IReadOnlyList<Lot> lots)
    {
        Config = config;
        CpProgram = cpProgram;
        FtProgram = ftProgram;
        WatProgram = watProgram;
        Lots = lots;
        DieMap = lots[0].Wafers[0].DieMap;
        DieTotals = DieCounts.Sum(
            lots.SelectMany(lot => lot.Wafers).Select(wafer => wafer.DieMap.Counts));
        Cp = CpSummary.Create(
            cpProgram,
            lots.SelectMany(lot => lot.Wafers)
                .SelectMany(wafer => wafer.CpResults)
                .ToArray());
        Ft = FtSummary.Create(
            ftProgram,
            lots.SelectMany(lot => lot.FtUnits).ToArray(),
            Cp.PassedDie);
    }

    public ResolvedConfig Config { get; }

    /// <summary>The effective CP Test Program applied to every wafer.</summary>
    public TestProgram CpProgram { get; }

    /// <summary>The effective FT Test Program applied to every FT Unit.</summary>
    public TestProgram FtProgram { get; }

    /// <summary>The effective WAT Test Program applied to every wafer.</summary>
    public WatProgram WatProgram { get; }

    public string ProductName => Config.Product.Name;

    public long Seed => Config.Seed;

    public IReadOnlyList<Lot> Lots { get; }

    /// <summary>The die map shared by every wafer; wafer geometry is dataset-wide.</summary>
    public DieMap DieMap { get; }

    public DieCounts DieTotals { get; }

    /// <summary>CP outcomes for the whole Dataset.</summary>
    public CpSummary Cp { get; }

    /// <summary>FT outcomes and Assembly Loss for the whole Dataset.</summary>
    public FtSummary Ft { get; }

    public int LotCount => Lots.Count;

    public int WaferCount => Lots.Sum(lot => lot.Wafers.Count);

    public int WatMeasurementCount => Lots.Sum(lot => lot.WatMeasurementCount);
}

public sealed record Lot(
    string Id,
    string AssemblyLot,
    IReadOnlyList<Wafer> Wafers,
    IReadOnlyList<FtUnit> FtUnits)
{
    public int WatMeasurementCount => Wafers.Sum(wafer => wafer.WatMeasurementCount);

    public CpSummary GetCpSummary(TestProgram program) =>
        CpSummary.Create(
            program,
            Wafers.SelectMany(wafer => wafer.CpResults).ToArray());

    public FtSummary GetFtSummary(TestProgram program) =>
        FtSummary.Create(program, FtUnits, Wafers.Sum(wafer => wafer.NetDie));
}

public sealed record Wafer(
    string Id,
    DieMap DieMap,
    IReadOnlyList<CpDieResult> CpResults,
    IReadOnlyList<WatMeasurement> WatResults)
{
    /// <summary>Number of valid Die that pass CP.</summary>
    public int NetDie => CpResults.Count(result => result.Passed);

    public int WatMeasurementCount => WatResults.Count;
}
