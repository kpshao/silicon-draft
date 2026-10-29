using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SiliconDraft.Core;

/// <summary>
/// Derives an independent child seed from the Dataset seed and a domain path, so
/// scheduling and iteration order cannot change generated values (ADR-0004).
/// </summary>
internal static class DeterministicSeed
{
    public static ulong ForDie(long datasetSeed, string lotId, string waferId, int x, int y)
    {
        var text = string.Concat(
            datasetSeed.ToString(CultureInfo.InvariantCulture),
            "|",
            lotId,
            "|",
            waferId,
            "|",
            x.ToString(CultureInfo.InvariantCulture),
            "|",
            y.ToString(CultureInfo.InvariantCulture));

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(text), hash);
        return BinaryPrimitives.ReadUInt64LittleEndian(hash);
    }
}

/// <summary>SplitMix64: a small, fully specified PRNG with stable output across runtimes.</summary>
internal struct SplitMix64
{
    private ulong _state;

    public SplitMix64(ulong seed) => _state = seed;

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    /// <summary>Uniform value in [0, 1) with 53 random bits.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Standard normal value via the Box-Muller transform.</summary>
    public double NextNormal()
    {
        var first = 1.0 - NextDouble();
        var second = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(first)) * Math.Cos(2.0 * Math.PI * second);
    }
}
