using System.Buffers.Binary;
using System.Text;

namespace Nir5000.Core;

public sealed record LegacyEqaParameter(
    string Name,
    short TermCount,
    short DegreesOfFreedom,
    float Bias,
    float EquationMean,
    float EquationSec,
    float EquationRsq,
    byte[] SelectionMask,
    float[] Coefficients,
    byte[] PreprocessingHeader)
{
    public LegacyEqaPreprocessing Preprocessing => LegacyEqaPreprocessing.Parse(PreprocessingHeader);

    public double PredictSelected(ReadOnlySpan<double> selectedSpectrum)
    {
        if (selectedSpectrum.Length != Coefficients.Length)
            throw new ArgumentException("The selected spectrum length does not match the EQA coefficient count.", nameof(selectedSpectrum));

        var result = (double)Bias;
        for (var index = 0; index < Coefficients.Length; index++)
            result += selectedSpectrum[index] * Coefficients[index];
        return result;
    }

    public double PredictStandardized(ReadOnlySpan<double> standardizedSpectrum)
    {
        if (standardizedSpectrum.Length != SelectionMask.Length)
            throw new ArgumentException("The standardized spectrum length does not match the EQA selection mask.", nameof(standardizedSpectrum));

        var selected = new double[Coefficients.Length];
        var selectedIndex = 0;
        for (var index = 0; index < SelectionMask.Length; index++)
        {
            if (SelectionMask[index] == 0) continue;
            if (selectedIndex == selected.Length)
                throw new InvalidDataException("The EQA selection mask contains more points than its coefficient vector.");
            selected[selectedIndex++] = standardizedSpectrum[index];
        }
        if (selectedIndex != selected.Length)
            throw new InvalidDataException("The EQA selection mask contains fewer points than its coefficient vector.");
        return PredictSelected(selected);
    }

    public double PredictSpectrum(ReadOnlySpan<double> rawSpectrum)
    {
        var standardized = Preprocessing.Apply(rawSpectrum);
        return PredictStandardized(standardized);
    }
}

/// <summary>
/// Reader for the proven NIRSystem 5000 type-9 EQA record variant.
/// It deliberately does not guess preprocessing semantics: the raw eight-byte
/// preprocessing header and selection mask are retained for the standardizer.
/// </summary>
public sealed record LegacyEqaModel(
    LegacyEqaFile Container,
    IReadOnlyList<LegacyEqaParameter> Parameters)
{
    private const int HeaderLength = 0x180;
    private const int RecordLength = 0x5F8;
    private const int RecordHeaderLength = 0x88;

    public static LegacyEqaModel ReadNir5000(string path)
    {
        var container = LegacyEqaFile.Read(path);
        var bytes = container.RawBytes;
        if (!container.InstrumentName.Contains("5000", StringComparison.OrdinalIgnoreCase) ||
            container.FormatVersion != 9 ||
            bytes.Length <= HeaderLength ||
            (bytes.Length - HeaderLength) % RecordLength != 0)
            throw new InvalidDataException("The file is not the proven NIRSystem 5000 type-9 EQA record variant.");

        var recordCount = (bytes.Length - HeaderLength) / RecordLength;
        var parameters = new List<LegacyEqaParameter>(recordCount);
        for (var recordIndex = 0; recordIndex < recordCount; recordIndex++)
        {
            var recordStart = HeaderLength + recordIndex * RecordLength;
            var termCount = ReadInt16(bytes, recordStart + 0x12);
            if (termCount <= 0 || RecordHeaderLength + container.SourcePointCount + termCount * sizeof(float) > RecordLength)
                throw new InvalidDataException($"EQA record {recordIndex} has an invalid term count: {termCount}.");

            var maskStart = recordStart + RecordHeaderLength;
            var coefficientStart = maskStart + container.SourcePointCount;
            var coefficients = new float[termCount];
            for (var coefficientIndex = 0; coefficientIndex < coefficients.Length; coefficientIndex++)
                coefficients[coefficientIndex] = ReadSingle(bytes, coefficientStart + coefficientIndex * sizeof(float));

            parameters.Add(new LegacyEqaParameter(
                ReadText(bytes, recordStart, 16),
                termCount,
                ReadInt16(bytes, recordStart + 0x14),
                ReadSingle(bytes, recordStart + 0x1A),
                ReadSingle(bytes, recordStart + 0x1E),
                ReadSingle(bytes, recordStart + 0x22),
                ReadSingle(bytes, recordStart + 0x26),
                bytes[maskStart..coefficientStart],
                coefficients,
                bytes[(recordStart + 0x80)..maskStart]));
        }

        return new LegacyEqaModel(container, parameters);
    }

    private static short ReadInt16(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, sizeof(short)));

    private static float ReadSingle(byte[] bytes, int offset) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(float))));

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
