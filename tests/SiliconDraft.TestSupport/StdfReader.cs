using System.Buffers.Binary;
using System.Text;

namespace SiliconDraft.TestSupport;

/// <summary>
/// A minimal, test-only STDF V4 reader. It parses the record stream and just enough
/// fields of FAR, MIR, PIR, PTR, PRR, and MRR to assert exported content.
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
