using System.Buffers.Binary;

namespace Nir5000.Core;

/// <summary>Preprocessing parameters observed in a type-9 NIR5000 EQA record.</summary>
public sealed record LegacyEqaPreprocessing(
    short DerivativeOrder,
    short DifferenceGap,
    short SmoothWindow1,
    short SmoothWindow2)
{
    public static LegacyEqaPreprocessing Parse(ReadOnlySpan<byte> header)
    {
        if (header.Length < 8) throw new InvalidDataException("The EQA preprocessing header is shorter than 8 bytes.");
        var result = new LegacyEqaPreprocessing(
            BinaryPrimitives.ReadInt16LittleEndian(header[0..2]),
            BinaryPrimitives.ReadInt16LittleEndian(header[2..4]),
            BinaryPrimitives.ReadInt16LittleEndian(header[4..6]),
            BinaryPrimitives.ReadInt16LittleEndian(header[6..8]));
        if (result.DerivativeOrder is < 0 or > 8 || result.DifferenceGap is < 1 or > 256 ||
            result.SmoothWindow1 is < 1 or > 256 || result.SmoothWindow2 is < 1 or > 256)
            throw new InvalidDataException("The EQA preprocessing parameters are outside safe bounds.");
        return result;
    }

    /// <summary>
    /// Applies the difference and moving-average operations reconstructed from
    /// predictionrelease.dll (FUN_10005d8c, FUN_10009930 and FUN_100099c0).
    /// Values beyond the effective transformed range are retained but are not
    /// considered transformed data.
    /// </summary>
    public double[] Apply(ReadOnlySpan<double> spectrum)
    {
        if (spectrum.Length == 0) throw new ArgumentException("A spectrum is required.", nameof(spectrum));
        var values = spectrum.ToArray();
        var length = values.Length;
        for (var pass = 0; pass < DerivativeOrder; pass++)
        {
            length -= DifferenceGap;
            if (length < 1) throw new InvalidDataException("The difference gap removes the complete spectrum.");
            for (var index = 0; index < length; index++)
                values[index] -= values[index + DifferenceGap];
        }
        length = ApplyMovingAverage(values, length, SmoothWindow1);
        length = ApplyMovingAverage(values, length, SmoothWindow2);
        return values;
    }

    private static int ApplyMovingAverage(double[] values, int length, int window)
    {
        if (window <= 1) return length;
        if (window > length) throw new InvalidDataException("The smoothing window is larger than the transformed spectrum.");

        var sum = 0.0;
        for (var index = 0; index < window - 1; index++) sum += values[index];
        var previousFirst = 0.0;
        var outputLength = length - window + 1;
        for (var index = 0; index < outputLength; index++)
        {
            var last = values[index + window - 1];
            sum += last - previousFirst;
            previousFirst = values[index];
            values[index] = sum / window;
        }
        return outputLength;
    }
}
