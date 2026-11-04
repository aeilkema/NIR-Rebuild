namespace Nir5000.Core;

public interface INir5000ByteTransport : IDisposable
{
    bool IsOpen { get; }
    void Open();
    int ReadByte();
    void Write(ReadOnlySpan<byte> data);
}

/// <summary>Transport-independent implementation of the reconstructed NIR protocol.</summary>
public sealed class Nir5000ProtocolSession : IDisposable
{
    private readonly INir5000ByteTransport transport;
    private readonly INir5000WireRecorder? recorder;

    public Nir5000ProtocolSession(INir5000ByteTransport transport, INir5000WireRecorder? recorder = null)
    {
        this.transport = transport;
        this.recorder = recorder;
    }
    public bool IsOpen => transport.IsOpen;
    public void Open() => transport.Open();
    public void Dispose() => transport.Dispose();

    public void SendFrame(ReadOnlySpan<byte> header, ReadOnlySpan<byte> payload)
    {
        RequireOpen();
        Write(Nir5000FrameCodec.BuildSendPreamble());
        Expect(Nir5000ProtocolConstants.Ack);
        Write(Nir5000FrameCodec.BuildSendFrame(header, payload));
        Expect(Nir5000ProtocolConstants.Ack);
    }

    /// <summary>
    /// Sends the two-byte lamp request statically reconstructed from
    /// Nirsinst.dll InstrSetLamp -> SicWrite. SicWrite does not wait for a
    /// response for this operation; the legacy driver tracks lamp state
    /// locally through InstrWasLampOn.
    /// </summary>
    public void SetLamp(bool enabled)
    {
        RequireOpen();
        Write(Nir5000LampCommands.BuildObservedRequest(enabled));
    }

    public Nir5000Frame ReceiveFrame(int payloadLength)
    {
        RequireOpen();
        var first = ReadByte();
        if (first == Nir5000ProtocolConstants.Enquiry) WriteByte(Nir5000ProtocolConstants.Ack);
        else if (first != Nir5000ProtocolConstants.Ack) throw new InvalidDataException($"Expected ENQ or ACK, received 0x{first:X2}.");
        var frame = new byte[2 + Nir5000ProtocolConstants.HeaderLength + payloadLength + 1];
        frame[0] = ReadExpected(Nir5000ProtocolConstants.Soh);
        ReadExactly(frame.AsSpan(1));
        var decoded = Nir5000FrameCodec.ParseReceivedFrame(frame, payloadLength);
        WriteByte(decoded.HasValidChecksum ? Nir5000ProtocolConstants.Ack : Nir5000ProtocolConstants.Nak);
        if (!decoded.HasValidChecksum) throw new InvalidDataException("NIR 5000 checksum mismatch.");
        return decoded;
    }

    private void Expect(byte expected) => _ = ReadExpected(expected);
    private byte ReadExpected(byte expected)
    {
        var actual = ReadByte();
        if (actual != expected) throw new InvalidDataException($"Expected 0x{expected:X2}, received 0x{actual:X2}.");
        return actual;
    }
    private byte ReadByte()
    {
        var value = checked((byte)transport.ReadByte());
        recorder?.RecordReceive(value);
        return value;
    }
    private void ReadExactly(Span<byte> destination)
    {
        for (var offset = 0; offset < destination.Length; offset++) destination[offset] = ReadByte();
    }
    private void WriteByte(byte value) => Write([value]);
    private void Write(ReadOnlySpan<byte> data)
    {
        recorder?.RecordTransmit(data);
        transport.Write(data);
    }
    private void RequireOpen()
    {
        if (!transport.IsOpen) throw new InvalidOperationException("Open the NIR 5000 serial session first.");
    }
}
