using System.Buffers.Binary;
using System.Text;

namespace SiliconDraft.Core;

/// <summary>
/// Shared STDF V4 record writing: file header and trailer, record framing, wafer records,
/// part-information groups, parametric and functional test records, lot summaries, audit
/// trails, and string encoding. All records are little-endian; FAR declares the byte order
/// and version.
/// </summary>
internal static class StdfFormat
{
    private const byte StdfVersion = 4;

    /// <summary>
    /// STDF CPU_TYPE 2 is IBM PC/AT-compatible, i.e. little-endian. Parser libraries
    /// such as Semi-ATE-STDF only recognize 1 (big-endian) and 2 (little-endian); the
    /// legacy DEC/VAX value 0 is treated as unknown endianness.
    /// </summary>
    private const byte CpuType = 2;

    /// <summary>
    /// Fixed timestamp keeps output byte-identical. Simulation Time will replace it when
    /// that configuration lands; no wall-clock time may enter the file.
    /// </summary>
    private const uint SimulationEpoch = 1_577_836_800; // 2020-01-01T00:00:00Z

    private const byte HeadNumber = 1;
    private const byte SiteGroup = 1;

    /// <summary>STDF WCR WF_UNITS value for millimeters.</summary>
    private const byte WaferUnitsMillimeters = 3;

    /// <summary>STDF PTR OPT_FLAG with every optional field valid; bit 1 is reserved and must be 1.</summary>
    private const byte PtrOptionalFlags = 0x02;

    /// <summary>
    /// Writes the file header and trailer around one step's records. Audit records go
    /// immediately after FAR and before MIR, where STDF V4 expects them.
    /// </summary>
    public static void WriteFile(
        Stream stream,
        string lotId,
        string productName,
        string stepCode,
        Action<Stream> writeBody,
        Action<Stream>? writeAudit = null)
    {
        WriteFar(stream);
        writeAudit?.Invoke(stream);
        WriteMir(stream, lotId, productName, stepCode);
        writeBody(stream);
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

    /// <summary>
    /// SDR describes the site group a multi-site step runs. It follows MIR and precedes
    /// any record that names a site.
    /// </summary>
    public static void WriteSdr(Stream stream, int siteCount)
    {
        if (siteCount is < CpConfig.MinSiteCount or > CpConfig.MaxSiteCount)
        {
            throw new InvalidOperationException(
                $"site count {siteCount} is outside the STDF test-site range " +
                $"{CpConfig.MinSiteCount}..{CpConfig.MaxSiteCount}");
        }

        WriteRecord(stream, 1, 80, writer =>
        {
            writer.Write(HeadNumber); // HEAD_NUM
            writer.Write(SiteGroup); // SITE_GRP
            writer.Write((byte)siteCount); // SITE_CNT
            for (var site = 1; site <= siteCount; site++)
            {
                writer.Write((byte)site); // SITE_NUM
            }

            for (var pair = 0; pair < 8; pair++)
            {
                WriteCn(writer, string.Empty); // *_TYP
                WriteCn(writer, string.Empty); // *_ID
            }
        });
    }

    public static void WritePir(Stream stream, int site) =>
        WriteRecord(stream, 5, 10, writer =>
        {
            writer.Write(HeadNumber); // HEAD_NUM
            WriteSite(writer, site); // SITE_NUM
        });

    public static void WritePtr(Stream stream, TestResult test, int site)
    {
        var item = test.Item;
        WriteRecord(stream, 15, 10, writer =>
        {
            writer.Write((uint)item.TestNumber); // TEST_NUM
            writer.Write(HeadNumber); // HEAD_NUM
            WriteSite(writer, site); // SITE_NUM
            writer.Write((byte)(test.Passed ? 0x00 : 0x80)); // TEST_FLG
            writer.Write((byte)0); // PARM_FLG
            writer.Write((float)test.Value); // RESULT
            WriteCn(writer, item.Name); // TEST_TXT
            WriteCn(writer, string.Empty); // ALARM_ID
            writer.Write(PtrOptionalFlags); // OPT_FLAG: every optional field follows
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

    /// <summary>
    /// FTR records a functional Test Item. STDF V4 FTR carries no unit or limit fields;
    /// the Test Item's identity, Tester Site, and pass/fail result are what it reports.
    /// OPT_FLAG 0xFF marks every optional vector field invalid (bits 6-7 are reserved and
    /// must be 1) while TEST_TXT and RSLT_TXT still follow in the default-data section.
    /// </summary>
    public static void WriteFtr(Stream stream, TestResult test, int site)
    {
        var item = test.Item;
        WriteRecord(stream, 15, 20, writer =>
        {
            writer.Write((uint)item.TestNumber); // TEST_NUM
            writer.Write(HeadNumber); // HEAD_NUM
            WriteSite(writer, site); // SITE_NUM
            writer.Write((byte)(test.Passed ? 0x00 : 0x80)); // TEST_FLG
            writer.Write((byte)0xFF); // OPT_FLAG
            writer.Write(0u); // CYCL_CNT
            writer.Write(0u); // REL_VADR
            writer.Write(0u); // REPT_CNT
            writer.Write(0u); // NUM_FAIL
            writer.Write(0); // XFAIL_AD: I4
            writer.Write(0); // YFAIL_AD: I4
            writer.Write((short)0); // VECT_OFF: I2
            writer.Write((ushort)0); // RTN_ICNT
            writer.Write((ushort)0); // PGM_ICNT
            writer.Write((ushort)0); // FAIL_PIN: D*n, zero bits
            WriteCn(writer, string.Empty); // VECT_NAM
            WriteCn(writer, string.Empty); // TIME_SET
            WriteCn(writer, string.Empty); // OP_CODE
            WriteCn(writer, item.Name); // TEST_TXT
            WriteCn(writer, string.Empty); // ALARM_ID
            WriteCn(writer, string.Empty); // PROG_TXT
            WriteCn(writer, test.Passed ? "PASS" : "FAIL"); // RSLT_TXT
            writer.Write(byte.MaxValue); // PATG_NUM: invalid
            writer.Write((ushort)0); // SPIN_MAP: D*n, zero bits
        });
    }

    /// <summary>
    /// Writes the record type the Test Item's result requires: Numeric items are
    /// parametric PTRs and Functional items are FTRs.
    /// </summary>
    public static void WriteTestRecord(Stream stream, TestResult test, int site)
    {
        if (test.Item is FunctionalTestItem)
        {
            WriteFtr(stream, test, site);
        }
        else
        {
            WritePtr(stream, test, site);
        }
    }

    public static char TestTypeCode(TestItem item) => item is FunctionalTestItem ? 'F' : 'P';

    public static void WritePrr(
        Stream stream,
        int site,
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
            WriteSite(writer, site); // SITE_NUM
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

    public static void WriteWir(Stream stream, string waferId) =>
        WriteRecord(stream, 2, 10, writer =>
        {
            writer.Write(HeadNumber); // HEAD_NUM
            writer.Write(SiteGroup); // SITE_GRP
            writer.Write(SimulationEpoch); // START_T
            WriteCn(writer, waferId); // WAFER_ID
        });

    public static void WriteWcr(Stream stream, WaferGeometry geometry, int centerX, int centerY)
    {
        EnsureCoordinateRange(centerX, centerY);
        var (positiveX, positiveY) = PositiveDirections(geometry);
        WriteRecord(stream, 2, 30, writer =>
        {
            writer.Write((float)geometry.DiameterMm); // WAFR_SIZ
            writer.Write((float)geometry.DieWidthMm); // DIE_WID
            writer.Write((float)geometry.DieHeightMm); // DIE_HT
            writer.Write(WaferUnitsMillimeters); // WF_UNITS
            writer.Write(FlatDirection(geometry.NotchAngleDeg)); // WF_FLAT
            writer.Write((short)centerX); // CENTER_X
            writer.Write((short)centerY); // CENTER_Y
            writer.Write((sbyte)positiveX); // POS_X
            writer.Write((sbyte)positiveY); // POS_Y
        });
    }

    public static void WriteWrr(
        Stream stream, string waferId, int partCount, int goodCount) =>
        WriteRecord(stream, 2, 20, writer =>
        {
            writer.Write(HeadNumber); // HEAD_NUM
            writer.Write(SiteGroup); // SITE_GRP
            writer.Write(SimulationEpoch); // FINISH_T
            writer.Write((uint)partCount); // PART_CNT
            writer.Write(0u); // RTST_CNT
            writer.Write(0u); // ABRT_CNT
            writer.Write((uint)goodCount); // GOOD_CNT
            writer.Write((uint)partCount); // FUNC_CNT: every part is functional
            WriteCn(writer, waferId); // WAFER_ID
        });

    public static void WriteTsr(
        Stream stream, int site, TestItem item, int executionCount, int failCount) =>
        WriteRecord(stream, 10, 30, writer =>
        {
            writer.Write(HeadNumber); // HEAD_NUM
            WriteSite(writer, site); // SITE_NUM
            writer.Write((byte)TestTypeCode(item)); // TEST_TYP
            writer.Write((uint)item.TestNumber); // TEST_NUM
            writer.Write((uint)executionCount); // EXEC_CNT
            writer.Write((uint)failCount); // FAIL_CNT
            writer.Write(0u); // ALRM_CNT
            WriteCn(writer, item.Name); // TEST_NAM
        });

    public static void WriteHbr(Stream stream, int site, BinCount bin) =>
        WriteBinRecord(stream, 1, 40, site, bin);

    public static void WriteSbr(Stream stream, int site, BinCount bin) =>
        WriteBinRecord(stream, 1, 50, site, bin);

    private static void WriteBinRecord(
        Stream stream, byte type, byte sub, int site, BinCount bin) =>
        WriteRecord(stream, type, sub, writer =>
        {
            writer.Write(HeadNumber); // HEAD_NUM
            WriteSite(writer, site); // SITE_NUM
            writer.Write((ushort)bin.Number); // BIN_NUM
            writer.Write((uint)bin.Count); // BIN_CNT
            writer.Write((byte)(bin.Pass ? 'P' : 'F')); // BIN_PF
            WriteCn(writer, bin.Name); // BIN_NAM
        });

    public static void WritePcr(Stream stream, int site, int partCount, int goodCount) =>
        WriteRecord(stream, 1, 30, writer =>
        {
            writer.Write(HeadNumber); // HEAD_NUM
            WriteSite(writer, site); // SITE_NUM
            writer.Write((uint)partCount); // PART_CNT
            writer.Write(0u); // RTST_CNT
            writer.Write(0u); // ABRT_CNT
            writer.Write((uint)goodCount); // GOOD_CNT
        });

    public static void WriteMrr(Stream stream) =>
        WriteRecord(stream, 1, 20, writer =>
        {
            writer.Write(SimulationEpoch); // FINISH_T
            writer.Write((byte)' '); // DISP_COD
            WriteCn(writer, string.Empty); // USR_DESC
            WriteCn(writer, string.Empty); // EXC_DESC
        });

    private static (byte PositiveX, byte PositiveY) PositiveDirections(WaferGeometry geometry)
    {
        var (axisX, _) = geometry.PhysicalDirection(1, 0);
        var (_, rowY) = geometry.PhysicalDirection(0, 1);
        var positiveX = axisX > 0 ? (byte)1 : axisX < 0 ? (byte)2 : (byte)0;
        var positiveY = rowY > 0 ? (byte)1 : rowY < 0 ? (byte)2 : (byte)0;
        return (positiveX, positiveY);
    }

    /// <summary>Maps the Notch angle to the STDF WF_FLAT value; 255 means unknown.</summary>
    private static byte FlatDirection(double notchAngleDeg) => notchAngleDeg switch
    {
        90 => 0, // up
        270 => 1, // down
        180 => 2, // left
        0 => 3, // right
        _ => 255,
    };

    private static void WriteSite(BinaryWriter writer, int site)
    {
        if (site is < CpConfig.MinSiteCount or > CpConfig.MaxSiteCount)
        {
            throw new InvalidOperationException(
                $"site {site} is outside the STDF test-site range " +
                $"{CpConfig.MinSiteCount}..{CpConfig.MaxSiteCount}");
        }

        writer.Write((byte)site);
    }

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
