namespace SiliconDraft.Core;

/// <summary>
/// Aggregates a step's per-part outcomes into the STDF V4 lot summaries, written in the
/// canonical order: one TSR per Test Item and site, then the per-site Hard Bin, Soft Bin,
/// and part-count records.
/// </summary>
internal static class StdfSummaryWriter
{
    public static void Write(
        Stream stream,
        TestProgram program,
        int siteCount,
        IEnumerable<StdfPartOutcome> parts)
    {
        var itemIndexByTestNumber = program.Items
            .Select((item, index) => (item.TestNumber, Index: index))
            .ToDictionary(entry => entry.TestNumber, entry => entry.Index);
        var partCounts = new int[siteCount + 1];
        var goodCounts = new int[siteCount + 1];
        var failCounts = new int[siteCount + 1][];
        var hardBinCounts = new Dictionary<int, int>[siteCount + 1];
        var softBinCounts = new Dictionary<int, int>[siteCount + 1];
        for (var site = 1; site <= siteCount; site++)
        {
            failCounts[site] = new int[program.Items.Count];
            hardBinCounts[site] = [];
            softBinCounts[site] = [];
        }

        foreach (var part in parts)
        {
            if (part.Site is < CpConfig.MinSiteCount or > CpConfig.MaxSiteCount)
            {
                throw new InvalidOperationException(
                    $"part site {part.Site} is outside the STDF test-site range " +
                    $"{CpConfig.MinSiteCount}..{CpConfig.MaxSiteCount}");
            }

            if (part.Site > siteCount)
            {
                throw new InvalidOperationException(
                    $"part site {part.Site} exceeds the configured site count {siteCount}");
            }

            var site = part.Site;
            partCounts[site]++;
            if (part.HardBin == program.Bins.PassHardBin)
            {
                goodCounts[site]++;
            }

            foreach (var test in part.Tests)
            {
                if (!itemIndexByTestNumber.TryGetValue(test.Item.TestNumber, out var itemIndex))
                {
                    throw new InvalidOperationException(
                        $"test {test.Item.TestNumber} is not part of the Test Program");
                }

                if (!test.Passed)
                {
                    failCounts[site][itemIndex]++;
                }
            }

            hardBinCounts[site][part.HardBin] =
                hardBinCounts[site].GetValueOrDefault(part.HardBin) + 1;
            softBinCounts[site][part.SoftBin] =
                softBinCounts[site].GetValueOrDefault(part.SoftBin) + 1;
        }

        for (var site = 1; site <= siteCount; site++)
        {
            for (var itemIndex = 0; itemIndex < program.Items.Count; itemIndex++)
            {
                StdfFormat.WriteTsr(
                    stream,
                    site,
                    program.Items[itemIndex],
                    partCounts[site],
                    failCounts[site][itemIndex]);
            }
        }

        var hardBins = new (int Number, string Name, bool Pass)[]
        {
            (program.Bins.PassHardBin, "Pass", true),
            (program.Bins.ParametricFailHardBin, "Parametric Fail", false),
            (program.Bins.FunctionalFailHardBin, "Functional Fail", false),
        }.OrderBy(bin => bin.Number);
        foreach (var (number, name, pass) in hardBins)
        {
            for (var site = 1; site <= siteCount; site++)
            {
                StdfFormat.WriteHbr(
                    stream,
                    site,
                    new BinCount(
                        number,
                        name,
                        pass,
                        hardBinCounts[site].GetValueOrDefault(number)));
            }
        }

        var softBins = new List<(int Number, string Name, bool Pass)>
        {
            (program.Bins.PassSoftBin, "Pass", true),
        };
        softBins.AddRange(
            program.Items.Select(item => (item.FailSoftBin, item.Name, false)));
        foreach (var (number, name, pass) in softBins.OrderBy(bin => bin.Number))
        {
            for (var site = 1; site <= siteCount; site++)
            {
                StdfFormat.WriteSbr(
                    stream,
                    site,
                    new BinCount(
                        number,
                        name,
                        pass,
                        softBinCounts[site].GetValueOrDefault(number)));
            }
        }

        for (var site = 1; site <= siteCount; site++)
        {
            StdfFormat.WritePcr(stream, site, partCounts[site], goodCounts[site]);
        }
    }
}

/// <summary>The per-part facts the lot summaries aggregate.</summary>
internal readonly record struct StdfPartOutcome(
    int Site,
    IReadOnlyList<TestResult> Tests,
    int HardBin,
    int SoftBin);
