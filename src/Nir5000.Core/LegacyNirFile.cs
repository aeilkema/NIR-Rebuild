using System.Buffers.Binary;
using System.Text;

namespace Nir5000.Core;

public sealed record LegacyNirMetadata(int SampleCount, int PointCount, string Description, int InstrumentType, string InstrumentName, string SerialNumber);

public sealed record LegacyNirFile(LegacyNirMetadata Metadata, Spectrum Spectrum, byte[] HeaderAndTrailer)
{
    public const int SpectrumOffset = 0x480;

    public static LegacyNirFile Read(string path, ScanRange scanRange)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < SpectrumOffset) throw new InvalidDataException("NIR file is shorter than its legacy header.");
        var sampleCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2));
        var pointCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2));
        var required = SpectrumOffset + pointCount * sizeof(float);
        if (pointCount == 0 || required > bytes.Length) throw new InvalidDataException("NIR file has an invalid spectrum length.");
        var wavelengths = scanRange.Wavelengths();
        if (wavelengths.Count != pointCount) throw new InvalidDataException($"Profile contains {wavelengths.Count} points, file contains {pointCount}.");
        var values = new double[pointCount];
        for (var i = 0; i < pointCount; i++) values[i] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(SpectrumOffset + i * 4, 4)));
        var metadata = new LegacyNirMetadata(sampleCount, pointCount, ReadText(bytes, 0x12, 80), BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x80, 2)), ReadText(bytes, 0x82, 32), ReadText(bytes, 0x52, 16));
        return new LegacyNirFile(metadata, new Spectrum(DateTimeOffset.MinValue, wavelengths, values), bytes);
    }

    public void WriteSingleSpectrum(string path, Spectrum spectrum)
    {
        if (spectrum.Values.Count != Metadata.PointCount) throw new ArgumentException("Spectrum point count does not match the legacy file.", nameof(spectrum));
        var bytes = (byte[])HeaderAndTrailer.Clone();
        for (var i = 0; i < spectrum.Values.Count; i++) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(SpectrumOffset + i * 4, 4), BitConverter.SingleToInt32Bits((float)spectrum.Values[i]));
        File.WriteAllBytes(path, bytes);
    }

    private static string ReadText(byte[] bytes, int offset, int length)
    {
        var end = Array.IndexOf(bytes, (byte)0, offset, Math.Min(length, bytes.Length - offset));
        if (end < 0) end = Math.Min(offset + length, bytes.Length);
        return Encoding.ASCII.GetString(bytes, offset, end - offset).Trim();
    }
}
