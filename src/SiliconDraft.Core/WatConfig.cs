namespace SiliconDraft.Core;

/// <summary>
/// Effective WAT settings after defaults are applied: the PCM Sites measured on every
/// Wafer and the TXT export choices. Recorded in the Resolved Config.
/// </summary>
public sealed record WatConfig
{
    internal WatConfig(
        IReadOnlyList<PcmSite> sites,
        WatTemplate template,
        WatFileOrganization organization)
    {
        Sites = sites;
        Template = template;
        Organization = organization;
    }

    /// <summary>The PCM Sites measured on every Wafer, in file order.</summary>
    public IReadOnlyList<PcmSite> Sites { get; }

    public WatTemplate Template { get; }

    public WatFileOrganization Organization { get; }
}

/// <summary>
/// A fixed wafer location where PCM structures are measured, in millimeters from the
/// wafer center (+X right, +Y up).
/// </summary>
public sealed record PcmSite(string Id, double XMm, double YMm)
{
    /// <summary>
    /// The default layout: the wafer center plus eight points halfway between the center
    /// and the wafer edge, at 45° increments starting from +X.
    /// </summary>
    internal static IReadOnlyList<PcmSite> CreateDefaultLayout(WaferGeometry wafer)
    {
        var radius = Math.Round(wafer.DiameterMm / 4, 6);
        var diagonal = Math.Round(radius * Math.Sqrt(0.5), 6);
        return
        [
            new PcmSite("S1", 0, 0),
            new PcmSite("S2", radius, 0),
            new PcmSite("S3", diagonal, diagonal),
            new PcmSite("S4", 0, radius),
            new PcmSite("S5", -diagonal, diagonal),
            new PcmSite("S6", -radius, 0),
            new PcmSite("S7", -diagonal, -diagonal),
            new PcmSite("S8", 0, -radius),
            new PcmSite("S9", diagonal, -diagonal),
        ];
    }
}

public enum WatTemplate
{
    Long,
    Wide,
}

public enum WatFileOrganization
{
    PerLot,
    PerWafer,
}
