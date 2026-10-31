namespace SiliconDraft.Core;

/// <summary>
/// The achieved die-level correlation of one configured CP-FT pair, measured on every
/// FT Unit that carries both results.
/// </summary>
public sealed record DieCorrelationResult(
    string CpItem,
    string FtItem,
    double TargetR,
    double Tolerance,
    double? AchievedR,
    int SampleCount)
{
    /// <summary><c>null</c> when there are too few samples to compute a correlation.</summary>
    public bool? WithinTolerance =>
        AchievedR is double achieved ? Math.Abs(achieved - TargetR) <= Tolerance : null;
}

internal static class CorrelationSummary
{
    public static IReadOnlyList<DieCorrelationResult> Create(
        ResolvedConfig config,
        TestProgram cpProgram,
        TestProgram ftProgram,
        IReadOnlyList<Lot> lots)
    {
        if (config.Latent.DiePairs.Count == 0)
        {
            return [];
        }

        var cpIndexes = config.Latent.DiePairs
            .Select(pair => TestProgramLookup.IndexOf(cpProgram, pair.CpItem))
            .ToArray();
        var ftIndexes = config.Latent.DiePairs
            .Select(pair => TestProgramLookup.IndexOf(ftProgram, pair.FtItem))
            .ToArray();
        var accumulators = new PearsonAccumulator[config.Latent.DiePairs.Count];

        foreach (var lot in lots)
        {
            var cpByDie = new Dictionary<(string WaferId, int X, int Y), CpDieResult>();
            foreach (var wafer in lot.Wafers)
            {
                foreach (var result in wafer.CpResults)
                {
                    cpByDie[(wafer.Id, result.Die.X, result.Die.Y)] = result;
                }
            }

            foreach (var unit in lot.FtUnits)
            {
                if (!cpByDie.TryGetValue(
                        (unit.SourceWaferId, unit.SourceDie.X, unit.SourceDie.Y),
                        out var cp))
                {
                    continue;
                }

                for (var index = 0; index < accumulators.Length; index++)
                {
                    accumulators[index].Add(
                        cp.Tests[cpIndexes[index]].Value,
                        unit.Tests[ftIndexes[index]].Value);
                }
            }
        }

        var results = new DieCorrelationResult[config.Latent.DiePairs.Count];
        for (var index = 0; index < results.Length; index++)
        {
            var pair = config.Latent.DiePairs[index];
            var accumulator = accumulators[index];
            results[index] = new DieCorrelationResult(
                pair.CpItem,
                pair.FtItem,
                pair.TargetR,
                pair.Tolerance,
                accumulator.Correlation,
                accumulator.Count);
        }

        return results;
    }

    private struct PearsonAccumulator
    {
        private long _count;
        private double _meanX;
        private double _meanY;
        private double _sumXX;
        private double _sumYY;
        private double _sumXY;

        public int Count => (int)_count;

        public double? Correlation
        {
            get
            {
                if (_count < 2 || _sumXX <= 0 || _sumYY <= 0)
                {
                    return null;
                }

                return Math.Round(_sumXY / Math.Sqrt(_sumXX * _sumYY), 6);
            }
        }

        public void Add(double x, double y)
        {
            _count++;
            var dx = x - _meanX;
            var dy = y - _meanY;
            _meanX += dx / _count;
            _meanY += dy / _count;
            _sumXX += dx * (x - _meanX);
            _sumYY += dy * (y - _meanY);
            _sumXY += dx * (y - _meanY);
        }
    }
}
