namespace SiliconDraft.Core;

/// <summary>
/// Generates the WAT measurements of every Wafer: one value per Test Item at every PCM
/// Site. The RNG is derived only from the Dataset seed, the Lot, Wafer, and Site ids, and
/// the Test Item name, so ordering cannot change the data.
/// </summary>
internal static class WatGenerator
{
    public static IReadOnlyList<WatMeasurement> Generate(
        WatProgram program,
        IReadOnlyList<PcmSite> sites,
        long seed,
        string lotId,
        string waferId)
    {
        var results = new List<WatMeasurement>(sites.Count * program.Items.Count);
        foreach (var site in sites)
        {
            foreach (var item in program.Items)
            {
                var random = new SplitMix64(
                    DeterministicSeed.ForWat(seed, lotId, waferId, site.Id, item.Name));
                results.Add(new WatMeasurement(site, item, item.Distribution.Sample(ref random)));
            }
        }

        return results;
    }
}
