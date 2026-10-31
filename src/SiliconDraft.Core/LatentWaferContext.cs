namespace SiliconDraft.Core;

/// <summary>
/// The shared lot and wafer factors plus the Spatial Field of one Wafer. CP and FT
/// sampling of the same Wafer reuse the same context, so both steps see identical
/// shared variation.
/// </summary>
internal sealed class LatentWaferContext
{
    private readonly Dictionary<(int X, int Y), double> _spatial;

    private LatentWaferContext(
        double lotFactor,
        double waferFactor,
        Dictionary<(int X, int Y), double> spatial)
    {
        LotFactor = lotFactor;
        WaferFactor = waferFactor;
        _spatial = spatial;
    }

    public double LotFactor { get; }

    public double WaferFactor { get; }

    public static LatentWaferContext Create(
        long seed,
        string lotId,
        string waferId,
        double lotFactor,
        DieMap dieMap,
        double diameterMm)
    {
        var waferFactor = new SplitMix64(
            DeterministicSeed.ForLatentWafer(seed, lotId, waferId)).NextNormal();
        var spatial = CreateSpatialField(seed, lotId, waferId, dieMap, diameterMm);
        return new LatentWaferContext(lotFactor, waferFactor, spatial);
    }

    public double SpatialAt(int x, int y) => _spatial.GetValueOrDefault((x, y));

    private static Dictionary<(int X, int Y), double> CreateSpatialField(
        long seed,
        string lotId,
        string waferId,
        DieMap dieMap,
        double diameterMm)
    {
        var random = new SplitMix64(
            DeterministicSeed.ForSpatialField(seed, lotId, waferId));
        Span<double> coefficients = stackalloc double[6];
        for (var index = 0; index < coefficients.Length; index++)
        {
            coefficients[index] = random.NextNormal();
        }

        var radius = diameterMm / 2;
        var tested = dieMap.Dies.Where(die => die.Tested).ToArray();
        var raw = new double[tested.Length];
        var mean = 0.0;
        for (var index = 0; index < tested.Length; index++)
        {
            var die = tested[index];
            var u = die.CenterXmm / radius;
            var v = die.CenterYmm / radius;
            raw[index] = coefficients[0] +
                (coefficients[1] * u) +
                (coefficients[2] * v) +
                (coefficients[3] * ((u * u) + (v * v))) +
                (coefficients[4] * ((u * u) - (v * v))) +
                (coefficients[5] * u * v);
            mean += raw[index];
        }

        mean /= tested.Length;
        var sumSquares = 0.0;
        for (var index = 0; index < raw.Length; index++)
        {
            var centered = raw[index] - mean;
            sumSquares += centered * centered;
        }

        var deviation = Math.Sqrt(sumSquares / tested.Length);
        var values = new Dictionary<(int X, int Y), double>(tested.Length);
        for (var index = 0; index < tested.Length; index++)
        {
            values[(tested[index].X, tested[index].Y)] =
                deviation > 1e-12 ? (raw[index] - mean) / deviation : 0;
        }

        return values;
    }
}
