using System.Buffers.Binary;
using System.Text;

namespace SiliconDraft.Core;

/// <summary>
/// Shared STDF V4 record writing: file header and trailer, record framing,
/// part-information groups, parametric test records, audit trails, and string encoding.
/// All records are little-endian; FAR declares the byte order and version.
/// </summary>
internal static class StdfFormat
{
    private const byte StdfVersion = 4;

    /// <summary>0 declares a little-endian file; the FAR record length confirms it.</summary>
    private const byte CpuType = 0;

    /// <summary>
    /// Fixed timestamp keeps output byte-identical. Simulation Time will replace it when
    /// that configuration lands; no wall-clock time may enter the file.
    /// </summary>
    private const uint SimulationEpoch = 1_577_836_800; // 2020-01-01T00:00:00Z

    private const byte HeadNumber = 1;
    private const byte SiteNumber = 1;

    /// <summary>
    /// Writes the file header and trailer around one step's part records. Audit records
    /// go immediately after FAR and before MIR, where STDF V4 expects them.
    /// </summary>
    public static void WriteFile(
        Stream stream,
        string lotId,
        string productName,
        string stepCode,
        Action<Stream> writeParts,
        Action<Stream>? writeAudit = null)
    {
        WriteFar(stream);
        writeAudit?.Invoke(stream);
        WriteMir(stream, lotId, productName, stepCode);
        writeParts(stream);
        WriteMrr(stream);
    }

    public static void WriteFar(Stream stream) =>
        WriteRecord(stream, 0, 10, writer =>
        {
            writer.Write(CpuType);
            writer.Write(StdfVersion);
        });

    public static void WriteMir(
        Stream stream, string lotId, string productName, string stepCode) =>
        WriteRecord(stream, 1, 10, writer =>
        {
            writer.Write(SimulationEpoch); // SETUP_T
            writer.Write(SimulationEpoch); // START_T
            writer.Write((byte)1); // STAT_NUM
            writer.Write((byte)'P'); // MODE_COD
            writer.Write((byte)' '); // RTST_COD
            writer.Write((byte)' '); // PROT_COD
            writer.Write(ushort.MaxValue); // BURN_TIM: missing
            writer.Write((byte)' '); // CMOD_COD
            WriteCn(writer, lotId); // LOT_ID
            WriteCn(writer, productName); // PART_TYP
            WriteCn(writer, "SILICONDRAFT"); // NODE_NAM
            WriteCn(writer, "SILICONDRAFT"); // TSTR_TYP
            WriteCn(writer, productName + "_" + stepCode); // JOB_NAM
            WriteCn(writer, "1"); // JOB_REV
            WriteCn(writer, string.Empty); // SBLOT_ID
            WriteCn(writer, "SILICONDRAFT"); // OPER_NAM
            WriteCn(writer, "SILICONDRAFT"); // EXEC_TYP
            WriteCn(writer, "1.0"); // EXEC_VER
            WriteCn(writer, stepCode); // TEST_COD
            WriteCn(writer, "25"); // TST_TEMP
            // The remaining optional MIR fields are omitted; record length ends the record.
        });

    public static void WritePir(Stream stream) =>
        WriteRecord(stream, 5, 10, writer =>
        {
            writer.Write(HeadNumber); // HEAD_NUM
            writer.Write(SiteNumber); // SITE_NUM
        });

    public static void WritePtr(Stream stream, TestResult test)
    {
        var item = test.Item;
        WriteRecord(stream, 15, 10, writer =>
        {
            writer.Write((uint)item.TestNumber); // TEST_NUM
            writer.Write(HeadNumber); // HEAD_NUM
            writer.Write(SiteNumber); // SITE_NUM
            writer.Write((byte)(test.Passed ? 0x00 : 0x80)); // TEST_FLG
            writer.Write((byte)0); // PARM_FLG
            writer.Write((float)test.Value); // RESULT
            WriteCn(writer, item.Name); // TEST_TXT
            WriteCn(writer, string.Empty); // ALARM_ID
            writer.Write((byte)0); // OPT_FLAG: every optional field follows
            writer.Write((sbyte)0); // RES_SCAL
            writer.Write((sbyte)0); // LLM_SCAL
            writer.Write((sbyte)0); // HLM_SCAL
            writer.Write((float)item.TestLimits.Low); // LO_LIMIT
            writer.Write((float)item.TestLimits.High); // HI_LIMIT
            WriteCn(writer, item.Unit); // UNITS
            WriteCn(writer, string.Empty); // C_RESFMT
            WriteCn(writer, string.Empty); // C_LLMFMT
            WriteCn(writer, string.Empty); // C_HLMFMT
            writer.Write((float)item.SpecLimits.Low); // LO_SPEC
            writer.Write((float)item.SpecLimits.High); // HI_SPEC
        });
    }

    public static void WritePrr(
        Stream stream,
        int x,
        int y,
        bool passed,
        int testCount,
        int hardBin,
        int softBin,
        string partId)
    {
        EnsureCoordinateRange(x, y);
        WriteRecord(stream, 5, 20, writer =>
        {
            writer.Write(HeadNumber); // HEAD_NUM
            writer.Write(SiteNumber); // SITE_NUM
            // PART_FLG: bit 3 marks a failing part; bit 4 stays 0 so the P/F flag is valid.
            writer.Write((byte)(passed ? 0x00 : 0x08));
            writer.Write((ushort)testCount); // NUM_TEST
            writer.Write((ushort)hardBin); // HARD_BIN
            writer.Write((ushort)softBin); // SOFT_BIN
            writer.Write((short)x); // X_COORD
            writer.Write((short)y); // Y_COORD
            writer.Write((uint)0); // TEST_T
            WriteCn(writer, partId); // PART_ID
            WriteCn(writer, string.Empty); // PART_TXT
            writer.Write((byte)0); // PART_FIX: empty bit array
        });
    }

    /// <summary>
    /// Writes an ATR whose CMD_LINE carries semicolon-separated <c>KEY=VALUE</c> pairs.
    /// </summary>
    public static void WriteAtr(Stream stream, string commandLine) =>
        WriteRecord(stream, 0, 20, writer =>
        {
            writer.Write(SimulationEpoch); // MOD_TIM
            WriteCn(writer, commandLine); // CMD_LINE
        });

    public static void WriteMrr(Stream stream) =>
        WriteRecord(stream, 1, 20, writer =>
        {
            writer.Write(SimulationEpoch); // FINISH_T
            writer.Write((byte)' '); // DISP_COD
            WriteCn(writer, string.Empty); // USR_DESC
            WriteCn(writer, string.Empty); // EXC_DESC
        });

    private static void EnsureCoordinateRange(int x, int y)
    {
        if (x is < short.MinValue or > short.MaxValue ||
            y is < short.MinValue or > short.MaxValue)
        {
            throw new InvalidOperationException(
                $"die ({x}, {y}) is outside the STDF I2 coordinate range");
        }
    }

    private static void WriteRecord(
        Stream stream, byte type, byte sub, Action<BinaryWriter> writeBody)
    {
        using var body = new MemoryStream();
        using (var writer = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true))
        {
            writeBody(writer);
        }

        if (body.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"STDF record ({type},{sub}) is {body.Length} bytes; the maximum is {ushort.MaxValue}");
        }

        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)body.Length);
        header[2] = type;
        header[3] = sub;
        stream.Write(header);
        body.Position = 0;
        body.CopyTo(stream);
    }

    private static void WriteCn(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > byte.MaxValue)
        {
            throw new InvalidOperationException(
                $"STDF string '{value}' is {bytes.Length} bytes; the maximum is {byte.MaxValue}");
        }

        writer.Write((byte)bytes.Length);
        writer.Write(bytes);
    }
}
