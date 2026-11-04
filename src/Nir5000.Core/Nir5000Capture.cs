namespace Nir5000.Core;

public sealed record Nir5000CapturedFrame(int Offset, Nir5000Frame Frame);

public static class Nir5000Capture
{
    /// <summary>
    /// Finds checksum-valid data frames in a raw serial capture. The payload
    /// length is supplied by the caller because the legacy header layout is
    /// not yet fully decoded.
    /// </summary>
    public static IReadOnlyList<Nir5000CapturedFrame> FindFrames(ReadOnlySpan<byte> capture, int payloadLength)
    {
        if (payloadLength < 0) throw new ArgumentOutOfRangeException(nameof(payloadLength));
        var frameLength = 2 + Nir5000ProtocolConstants.HeaderLength + payloadLength + 1;
        var frames = new List<Nir5000CapturedFrame>();
        for (var offset = 0; offset <= capture.Length - frameLength; offset++)
        {
            if (capture[offset] != Nir5000ProtocolConstants.Soh) continue;
            try
            {
                var frame = Nir5000FrameCodec.ParseReceivedFrame(capture.Slice(offset, frameLength), payloadLength);
                if (frame.HasValidChecksum) frames.Add(new Nir5000CapturedFrame(offset, frame));
            }
            catch (InvalidDataException)
            {
                // SOH can occur in payload data; continue scanning.
            }
        }
        return frames;
    }
}
