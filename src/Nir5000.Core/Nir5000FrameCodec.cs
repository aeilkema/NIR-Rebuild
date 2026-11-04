namespace Nir5000.Core;

public static class Nir5000ProtocolConstants
{
    public const byte Enquiry = 0x04;
    public const byte Ack = 0x06;
    public const byte Soh = 0x01;
    public const byte Stx = 0x02;
    public const byte Nak = 0x15;
    public const int HeaderLength = 8;
}

public sealed record Nir5000Frame(byte[] Header, byte[] Payload, byte Checksum)
{
    public bool HasValidChecksum => Nir5000FrameCodec.Checksum(Header, Payload) == Checksum;
}

public static class Nir5000FrameCodec
{
    public static byte Checksum(ReadOnlySpan<byte> header, ReadOnlySpan<byte> payload)
    {
        byte result = 0;
        foreach (var value in header) result = unchecked((byte)(result + value));
        foreach (var value in payload) result = unchecked((byte)(result + value));
        return result;
    }

    public static byte[] BuildSendPreamble() => [Nir5000ProtocolConstants.Enquiry];

    public static byte[] BuildSendFrame(ReadOnlySpan<byte> header, ReadOnlySpan<byte> payload)
    {
        if (header.Length != Nir5000ProtocolConstants.HeaderLength) throw new ArgumentException("NIR 5000 frame headers are 8 bytes.", nameof(header));
        var result = new byte[2 + header.Length + payload.Length + 1];
        result[0] = Nir5000ProtocolConstants.Soh;
        header.CopyTo(result.AsSpan(1));
        result[1 + header.Length] = Nir5000ProtocolConstants.Stx;
        payload.CopyTo(result.AsSpan(2 + header.Length));
        result[^1] = Checksum(header, payload);
        return result;
    }

    public static Nir5000Frame ParseReceivedFrame(ReadOnlySpan<byte> frame, int payloadLength)
    {
        var expectedLength = 2 + Nir5000ProtocolConstants.HeaderLength + payloadLength + 1;
        if (frame.Length != expectedLength) throw new InvalidDataException($"Expected {expectedLength} bytes, received {frame.Length}.");
        if (frame[0] != Nir5000ProtocolConstants.Soh) throw new InvalidDataException("Missing SOH.");
        var header = frame.Slice(1, Nir5000ProtocolConstants.HeaderLength).ToArray();
        if (frame[1 + Nir5000ProtocolConstants.HeaderLength] != Nir5000ProtocolConstants.Stx) throw new InvalidDataException("Missing STX.");
        var payload = frame.Slice(2 + Nir5000ProtocolConstants.HeaderLength, payloadLength).ToArray();
        return new Nir5000Frame(header, payload, frame[^1]);
    }
}
