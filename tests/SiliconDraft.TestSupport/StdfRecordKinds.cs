namespace SiliconDraft.TestSupport;

/// <summary>
/// The STDF V4 record kinds a full-fidelity SiliconDraft file must contain. STDF V4 has
/// no WER record; the Wafer configuration record is WCR (2,30).
/// </summary>
public static class StdfRecordKinds
{
    public static IReadOnlyList<(byte Type, byte Sub)> FullV4Set { get; } =
    [
        (0, 10), // FAR
        (1, 10), // MIR
        (1, 20), // MRR
        (1, 30), // PCR
        (1, 40), // HBR
        (1, 50), // SBR
        (1, 80), // SDR
        (2, 10), // WIR
        (2, 20), // WRR
        (2, 30), // WCR
        (5, 10), // PIR
        (5, 20), // PRR
        (10, 30), // TSR
        (15, 10), // PTR
        (15, 20), // FTR
    ];
}
