using System.Buffers.Binary;
using System.Text;

namespace SiliconDraft.TestSupport;

/// <summary>
/// A minimal, test-only STDF V4 reader. It parses the record stream and enough fields of
/// FAR, ATR, MIR, SDR, WIR, WCR, WRR, PCR, HBR, SBR, TSR, PIR, PRR, PTR, FTR, and MRR to
/// assert exported content and record framing.
/// </summary>
public static class StdfReader
{
    public static IReadOnlyList<StdfRecord> Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var records = new List<StdfRecord>();
        var offset = 0;
        while (offset < bytes.Length)
        {
            if (offset + 4 > bytes.Length)
            {
                throw new InvalidDataException($"truncated STDF record header at offset {offset}");
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
            var type = bytes[offset + 2];
            var sub = bytes[offset + 3];
            if (offset + 4 + length > bytes.Length)
            {
                throw new InvalidDataException($"truncated STDF record body at offset {offset}");
            }

            records.Add(new StdfRecord(type, sub, bytes.AsSpan(offset + 4, length).ToArray()));
            offset += 4 + length;
        }

        return records;
    }

    public static FarInfo ParseFar(StdfRecord record)
    {
        Require(record, 0, 10, "FAR");
        var reader = new FieldReader(record.Data);
        return new FarInfo(reader.ReadU1(), reader.ReadU1());
    }

    public static MirInfo ParseMir(StdfRecord record)
    {
        Require(record, 1, 10, "MIR");
        var reader = new FieldReader(record.Data);
        var setupTime = reader.ReadU4();
        var startTime = reader.ReadU4();
        var station = reader.ReadU1();
        var mode = reader.ReadC1();
        var retest = reader.ReadC1();
        var protection = reader.ReadC1();
        var burnTime = reader.ReadU2();
        var commandMode = reader.ReadC1();
        var lotId = reader.ReadCn();
        var partType = reader.ReadCn();
        var nodeName = reader.ReadCn();
        var testerType = reader.ReadCn();
        var jobName = reader.ReadCn();
        var jobRevision = reader.ReadCn();
        reader.ReadCn(); // SBLOT_ID
        reader.ReadCn(); // OPER_NAM
        reader.ReadCn(); // EXEC_TYP
        reader.ReadCn(); // EXEC_VER
        var testCode = reader.ReadCn();
        var testTemperature = reader.ReadCn();

        return new MirInfo(
            setupTime,
            startTime,
            station,
            mode,
            retest,
            protection,
            burnTime,
            commandMode,
            lotId,
            partType,
            nodeName,
            testerType,
            jobName,
            jobRevision,
            testCode,
            testTemperature);
    }

    public static PirInfo ParsePir(StdfRecord record)
    {
        Require(record, 5, 10, "PIR");
        var reader = new FieldReader(record.Data);
        return new PirInfo(reader.ReadU1(), reader.ReadU1());
    }

    public static PtrInfo ParsePtr(StdfRecord record)
    {
        Require(record, 15, 10, "PTR");
        var reader = new FieldReader(record.Data);
        var testNumber = reader.ReadU4();
        var head = reader.ReadU1();
        var site = reader.ReadU1();
        var testFlags = reader.ReadB1();
        var parametricFlags = reader.ReadB1();
        var result = reader.ReadR4();
        var testText = reader.ReadCn();
        var alarmId = reader.ReadCn();
        var optionalFlags = reader.ReadB1();
        var resultScale = reader.ReadI1();
        var lowLimitScale = reader.ReadI1();
        var highLimitScale = reader.ReadI1();
        var lowLimit = reader.ReadR4();
        var highLimit = reader.ReadR4();
        var unit = reader.ReadCn();
        reader.ReadCn(); // C_RESFMT
        reader.ReadCn(); // C_LLMFMT
        reader.ReadCn(); // C_HLMFMT
        var lowSpec = reader.ReadR4();
        var highSpec = reader.ReadR4();

        return new PtrInfo(
            testNumber,
            head,
            site,
            testFlags,
            parametricFlags,
            result,
            testText,
            alarmId,
            optionalFlags,
            resultScale,
            lowLimitScale,
            highLimitScale,
            lowLimit,
            highLimit,
            unit,
            lowSpec,
            highSpec);
    }

    public static AtrInfo ParseAtr(StdfRecord record)
    {
        Require(record, 0, 20, "ATR");
        var reader = new FieldReader(record.Data);
        return new AtrInfo(reader.ReadU4(), reader.ReadCn());
    }

    public static SdrInfo ParseSdr(StdfRecord record)
    {
        Require(record, 1, 80, "SDR");
        var reader = new FieldReader(record.Data);
        var head = reader.ReadU1();
        var siteGroup = reader.ReadU1();
        var siteCount = reader.ReadU1();
        var siteNumbers = new byte[siteCount];
        for (var index = 0; index < siteCount; index++)
        {
            siteNumbers[index] = reader.ReadU1();
        }

        // The eight hardware type/ID pairs follow; SiliconDraft writes them empty but
        // still present, so consume them to prove the record is well formed.
        for (var index = 0; index < 16; index++)
        {
            reader.ReadCn();
        }

        return new SdrInfo(head, siteGroup, siteCount, siteNumbers);
    }

    public static WirInfo ParseWir(StdfRecord record)
    {
        Require(record, 2, 10, "WIR");
        var reader = new FieldReader(record.Data);
        return new WirInfo(reader.ReadU1(), reader.ReadU1(), reader.ReadU4(), reader.ReadCn());
    }

    public static WcrInfo ParseWcr(StdfRecord record)
    {
        Require(record, 2, 30, "WCR");
        var reader = new FieldReader(record.Data);
        return new WcrInfo(
            reader.ReadR4(),
            reader.ReadR4(),
            reader.ReadR4(),
            reader.ReadU1(),
            reader.ReadB1(),
            reader.ReadI2(),
            reader.ReadI2(),
            reader.ReadI1(),
            reader.ReadI1());
    }

    public static WrrInfo ParseWrr(StdfRecord record)
    {
        Require(record, 2, 20, "WRR");
        var reader = new FieldReader(record.Data);
        var head = reader.ReadU1();
        var siteGroup = reader.ReadU1();
        var finishTime = reader.ReadU4();
        var partCount = reader.ReadU4();
        var retestCount = reader.ReadU4();
        var abortCount = reader.ReadU4();
        var goodCount = reader.ReadU4();
        var functionalCount = reader.ReadU4();
        var waferId = reader.ReadCn();
        return new WrrInfo(
            head,
            siteGroup,
            finishTime,
            partCount,
            retestCount,
            abortCount,
            goodCount,
            functionalCount,
            waferId);
    }

    public static PcrInfo ParsePcr(StdfRecord record)
    {
        Require(record, 1, 30, "PCR");
        var reader = new FieldReader(record.Data);
        return new PcrInfo(
            reader.ReadU1(),
            reader.ReadU1(),
            reader.ReadU4(),
            reader.ReadU4(),
            reader.ReadU4(),
            reader.ReadU4());
    }

    public static HbrInfo ParseHbr(StdfRecord record)
    {
        Require(record, 1, 40, "HBR");
        var reader = new FieldReader(record.Data);
        return new HbrInfo(
            reader.ReadU1(),
            reader.ReadU1(),
            reader.ReadU2(),
            reader.ReadU4(),
            reader.ReadC1(),
            reader.ReadCn());
    }

    public static SbrInfo ParseSbr(StdfRecord record)
    {
        Require(record, 1, 50, "SBR");
        var reader = new FieldReader(record.Data);
        return new SbrInfo(
            reader.ReadU1(),
            reader.ReadU1(),
            reader.ReadU2(),
            reader.ReadU4(),
            reader.ReadC1(),
            reader.ReadCn());
    }

    public static TsrInfo ParseTsr(StdfRecord record)
    {
        Require(record, 10, 30, "TSR");
        var reader = new FieldReader(record.Data);
        return new TsrInfo(
            reader.ReadU1(),
            reader.ReadU1(),
            reader.ReadC1(),
            reader.ReadU4(),
            reader.ReadU4(),
            reader.ReadU4(),
            reader.ReadU4(),
            reader.ReadCn());
    }

    public static FtrInfo ParseFtr(StdfRecord record)
    {
        Require(record, 15, 20, "FTR");
        var reader = new FieldReader(record.Data);
        var testNumber = reader.ReadU4();
        var head = reader.ReadU1();
        var site = reader.ReadU1();
        // STDF V4 FTR has no PARM_FLG; TEST_FLG is followed directly by OPT_FLAG.
        var testFlags = reader.ReadB1();
        var optionalFlags = reader.ReadB1();
        reader.ReadU4(); // CYCL_CNT
        reader.ReadU4(); // REL_VADR
        reader.ReadU4(); // REPT_CNT
        reader.ReadU4(); // NUM_FAIL
        reader.ReadI2(); // XFAIL_AD
        reader.ReadI2(); // YFAIL_AD
        reader.ReadI2(); // VECT_OFF
        var returnIndexCount = reader.ReadU2();
        var programIndexCount = reader.ReadU2();
        reader.Skip(returnIndexCount * 2);
        reader.Skip((returnIndexCount + 1) / 2);
        reader.Skip(programIndexCount * 2);
        reader.Skip((programIndexCount + 1) / 2);
        reader.ReadDn(); // FAIL_PIN
        reader.ReadCn(); // VECT_NAM
        reader.ReadCn(); // TIME_SET
        reader.ReadCn(); // OP_CODE
        var testText = reader.ReadCn();
        var alarmId = reader.ReadCn();
        reader.ReadCn(); // PROG_TXT
        var resultText = reader.ReadCn();
        reader.ReadU1(); // PATG_NUM
        reader.ReadDn(); // SPIN_MAP

        return new FtrInfo(
            testNumber,
            head,
            site,
            testFlags,
            optionalFlags,
            resultText,
            testText,
            alarmId);
    }

    public static PrrInfo ParsePrr(StdfRecord record)
    {
        Require(record, 5, 20, "PRR");
        var reader = new FieldReader(record.Data);
        var head = reader.ReadU1();
        var site = reader.ReadU1();
        var partFlags = reader.ReadB1();
        var numTest = reader.ReadU2();
        var hardBin = reader.ReadU2();
        var softBin = reader.ReadU2();
        var x = reader.ReadI2();
        var y = reader.ReadI2();
        var testTime = reader.ReadU4();
        var partId = reader.ReadCn();
        var partText = reader.ReadCn();
        var partFix = reader.ReadBn();

        return new PrrInfo(
            head, site, partFlags, numTest, hardBin, softBin, x, y, testTime, partId, partText, partFix);
    }

    public static MrrInfo ParseMrr(StdfRecord record)
    {
        Require(record, 1, 20, "MRR");
        var reader = new FieldReader(record.Data);
        return new MrrInfo(reader.ReadU4(), reader.ReadC1(), reader.ReadCn(), reader.ReadCn());
    }

    private static void Require(StdfRecord record, byte type, byte sub, string name)
    {
        if (record.Type != type || record.Sub != sub)
        {
            throw new InvalidDataException(
                $"expected {name} ({type},{sub}), found ({record.Type},{record.Sub})");
        }
    }

    private sealed class FieldReader
    {
        private readonly byte[] _data;
        private int _offset;

        public FieldReader(byte[] data) => _data = data;

        public byte ReadU1() => ReadBytes(1)[0];

        public sbyte ReadI1() => unchecked((sbyte)ReadU1());

        public char ReadC1() => (char)ReadU1();

        public byte ReadB1() => ReadU1();

        public ushort ReadU2() => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(2));

        public short ReadI2() => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(2));

        public uint ReadU4() => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));

        public float ReadR4() => BinaryPrimitives.ReadSingleLittleEndian(ReadBytes(4));

        public string ReadCn()
        {
            var length = ReadU1();
            return Encoding.UTF8.GetString(ReadBytes(length));
        }

        public byte[] ReadBn()
        {
            var length = ReadU1();
            return ReadBytes(length).ToArray();
        }

        public byte[] ReadDn() => ReadBn();

        public void Skip(int count) => ReadBytes(count);

        private ReadOnlySpan<byte> ReadBytes(int count)
        {
            if (_offset + count > _data.Length)
            {
                throw new InvalidDataException(
                    $"record ended at offset {_offset} while reading {count} bytes");
            }

            var span = _data.AsSpan(_offset, count);
            _offset += count;
            return span;
        }
    }
}

public sealed record StdfRecord(byte Type, byte Sub, byte[] Data);

public sealed record FarInfo(byte CpuType, byte StdfVersion);

public sealed record MirInfo(
    uint SetupTime,
    uint StartTime,
    byte StationNumber,
    char ModeCode,
    char RetestCode,
    char ProtectionCode,
    ushort BurnTime,
    char CommandModeCode,
    string LotId,
    string PartType,
    string NodeName,
    string TesterType,
    string JobName,
    string JobRevision,
    string TestCode,
    string TestTemperature);

public sealed record PirInfo(byte HeadNumber, byte SiteNumber);

public sealed record PtrInfo(
    uint TestNumber,
    byte HeadNumber,
    byte SiteNumber,
    byte TestFlags,
    byte ParametricFlags,
    float Result,
    string TestText,
    string AlarmId,
    byte OptionalFlags,
    sbyte ResultScale,
    sbyte LowLimitScale,
    sbyte HighLimitScale,
    float LowLimit,
    float HighLimit,
    string Unit,
    float LowSpec,
    float HighSpec)
{
    public bool Failed => (TestFlags & 0x80) != 0;

    public bool Passed => !Failed;
}

/// <summary>
/// An audit trail record. SiliconDraft encodes traceability fields in CMD_LINE as
/// semicolon-separated <c>KEY=VALUE</c> pairs.
/// </summary>
public sealed record AtrInfo(uint ModificationTime, string CommandLine)
{
    public IReadOnlyDictionary<string, string> ParseFields() =>
        CommandLine
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => parts[0],
                parts => parts.Length == 2 ? parts[1] : string.Empty);
}

public sealed record SdrInfo(
    byte HeadNumber,
    byte SiteGroup,
    byte SiteCount,
    IReadOnlyList<byte> SiteNumbers);

public sealed record WirInfo(byte HeadNumber, byte SiteGroup, uint StartTime, string WaferId);

public sealed record WcrInfo(
    float WaferSize,
    float DieWidth,
    float DieHeight,
    byte WaferUnits,
    byte WaferFlat,
    short CenterX,
    short CenterY,
    sbyte PositiveX,
    sbyte PositiveY);

public sealed record WrrInfo(
    byte HeadNumber,
    byte SiteGroup,
    uint FinishTime,
    uint PartCount,
    uint RetestCount,
    uint AbortCount,
    uint GoodCount,
    uint FunctionalCount,
    string WaferId);

public sealed record PcrInfo(
    byte HeadNumber,
    byte SiteNumber,
    uint PartCount,
    uint RetestCount,
    uint AbortCount,
    uint GoodCount);

public sealed record HbrInfo(
    byte HeadNumber,
    byte SiteNumber,
    ushort HardBinNumber,
    uint Count,
    char PassFail,
    string Name)
{
    public bool Pass => PassFail == 'P';
}

public sealed record SbrInfo(
    byte HeadNumber,
    byte SiteNumber,
    ushort SoftBinNumber,
    uint Count,
    char PassFail,
    string Name)
{
    public bool Pass => PassFail == 'P';
}

public sealed record TsrInfo(
    byte HeadNumber,
    byte SiteNumber,
    char TestType,
    uint TestNumber,
    uint ExecutionCount,
    uint FailCount,
    uint AlarmCount,
    string TestName);

public sealed record FtrInfo(
    uint TestNumber,
    byte HeadNumber,
    byte SiteNumber,
    byte TestFlags,
    byte OptionalFlags,
    string ResultText,
    string TestText,
    string AlarmId)
{
    public bool Failed => (TestFlags & 0x80) != 0;

    public bool Passed => !Failed;
}

public sealed record PrrInfo(
    byte HeadNumber,
    byte SiteNumber,
    byte PartFlags,
    ushort NumberOfTests,
    ushort HardBin,
    ushort SoftBin,
    short X,
    short Y,
    uint TestTime,
    string PartId,
    string PartText,
    byte[] PartFix);

public sealed record MrrInfo(uint FinishTime, char DispositionCode, string UserDescription, string ExecDescription);
