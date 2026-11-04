using System.Buffers.Binary;
using System.Text;

namespace Nir5000.Core;

/// <summary>
/// Read-only structural view of the ISIscan legacy EQA container.
/// The coefficient records are intentionally kept opaque until their record
/// boundaries have been proven against an authorized model corpus.
/// </summary>
public sealed record LegacyEqaFile(
    ushort FormatVersion,
    ushort HeaderType,
    ushort SourcePointCount,
    uint FileChecksum,
    string Description,
    string SerialNumber,
    string InstrumentName,
    ushort ModelPointCount,
    double StartWavelengthNm,
    double IncrementWavelengthNm,
    double StopWavelengthNm,
    string RecordLayout,
    IReadOnlyList<string> CandidateNames,
    byte[] RawBytes)
{
    private const int HeaderMinimumLength = 0x180;

    public static LegacyEqaFile Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < HeaderMinimumLength)
            throw new InvalidDataException("EQA file is shorter than the known legacy header.");

        var instrumentName = ReadText(bytes, 0x82, 31);
        var recordLayout = HasNir5000FixedRecords(bytes, instrumentName)
            ? "NIR5000-fixed-0x5F8"
            : "unknown/variant";
        var names = FindRecordNames(bytes, instrumentName);
        var start = ReadSingle(bytes, 0xCC);
        var increment = ReadSingle(bytes, 0xE8);
        var stop = start + (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2)) - 1) * increment;
        if (!float.IsFinite(start) || !float.IsFinite(increment) || increment <= 0 || !double.IsFinite(stop) || start >= stop)
            throw new InvalidDataException("EQA file has invalid wavelength metadata.");

        return new LegacyEqaFile(
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2)),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x0A, 4)),
            ReadText(bytes, 0x12, 64),
            ReadText(bytes, 0x59, 16),
            instrumentName,
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0xA4, 2)),
            start,
            increment,
            stop,
            recordLayout,
            names,
            bytes);
    }

    private static float ReadSingle(byte[] bytes, int offset) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(float))));

    private static IReadOnlyList<string> FindRecordNames(byte[] bytes, string instrumentName)
    {
        // The NIRSystem 5000 files in the authorized corpus use a 0x180-byte
        // header followed by fixed 0x5F8-byte constituent records.  Restrict
        // this shortcut to that exact shape; other EQA variants remain
        // intentionally opaque and use the conservative text fallback.
        const int headerLength = 0x180;
        const int recordLength = 0x5F8;
        if (!HasNir5000FixedRecords(bytes, instrumentName))
            return Array.Empty<string>();

        var names = new List<string>();
        for (var offset = headerLength; offset < bytes.Length; offset += recordLength)
        {
            var name = ReadText(bytes, offset, 16);
            if (name.Length >= 1 && name.All(IsNameCharacter) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                names.Add(name);
        }
        return names;
    }

    private static bool HasNir5000FixedRecords(byte[] bytes, string instrumentName) =>
        instrumentName.Contains("5000", StringComparison.OrdinalIgnoreCase) &&
        bytes.Length > 0x180 && (bytes.Length - 0x180) % 0x5F8 == 0;

    private static bool IsNameCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is ' ' or '_' or '-';

    private static string ReadText(byte[] bytes, int offset, int length)
    {
        var end = Math.Min(offset + length, bytes.Length);
        for (var index = offset; index < end; index++)
        {
            if (bytes[index] == 0 || bytes[index] < 0x20)
            {
                end = index;
                break;
            }
        }
        return Encoding.ASCII.GetString(bytes, offset, end - offset).Trim();
    }
}
