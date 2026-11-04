namespace Nir5000.Core;

public sealed record Nir5000TransferSegment(int Offset, byte[] Payload)
{
    public int Length => Payload.Length;
}

public static class Nir5000Transfer
{
    /// <summary>
    /// Splits a legacy transfer exactly like the statically reconstructed
    /// FUN_1010_1724 path: sequential offsets and a maximum segment of F0h.
    /// </summary>
    public static IReadOnlyList<Nir5000TransferSegment> Segment(ReadOnlySpan<byte> data)
    {
        var segments = new List<Nir5000TransferSegment>();
        for (var offset = 0; offset < data.Length;)
        {
            var length = Math.Min(Nir5000Commands.MaximumSegmentLength, data.Length - offset);
            segments.Add(new Nir5000TransferSegment(offset, data.Slice(offset, length).ToArray()));
            offset += length;
        }
        return segments;
    }
}
