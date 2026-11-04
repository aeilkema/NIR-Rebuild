using System.Buffers.Binary;

namespace Nir5000.Core;

public sealed record Nir5000CalibrationBlock(float TableA, float TableB, float Step);

/// <summary>
/// Parser for the calibration configuration buffer consumed by FUN_1020_0FEA.
/// This is distinct from the 256-byte runtime instrument header returned by
/// GET_INSTHDR; their relationship still requires a device capture.
/// </summary>
public sealed record Nir5000CalibrationTable(IReadOnlyList<Nir5000CalibrationBlock> Blocks)
{
    public const int TableAOffset = 0x04;
    public const int TableBOffset = 0xAC;
    public const int StepOffset = 0x154;
    public const int EntryStride = 8;

    public static Nir5000CalibrationTable Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2) throw new InvalidDataException("Calibration buffer is shorter than its block count.");
        var blockCount = BinaryPrimitives.ReadInt16LittleEndian(data);
        if (blockCount <= 0 || blockCount > 256) throw new InvalidDataException($"Invalid calibration block count: {blockCount}.");
        var required = StepOffset + (blockCount - 1) * EntryStride + sizeof(float);
        if (required > data.Length) throw new InvalidDataException($"Calibration buffer requires {required} bytes, received {data.Length}.");

        var blocks = new List<Nir5000CalibrationBlock>(blockCount);
        for (var index = 0; index < blockCount; index++)
        {
            blocks.Add(new Nir5000CalibrationBlock(
                ReadFloat(data, TableAOffset + index * EntryStride),
                ReadFloat(data, TableBOffset + index * EntryStride),
                ReadFloat(data, StepOffset + index * EntryStride)));
        }
        return new Nir5000CalibrationTable(blocks);
    }

    private static float ReadFloat(ReadOnlySpan<byte> data, int offset) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, sizeof(float))));
}
