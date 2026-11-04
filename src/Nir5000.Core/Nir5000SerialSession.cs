using System.IO.Ports;

namespace Nir5000.Core;

public sealed record Nir5000SerialOptions(string PortName, int BaudRate, int TimeoutMs = 3000);

public sealed class Nir5000SerialSession : IDisposable
{
    private readonly SerialPort port;
    private readonly Nir5000ProtocolSession protocol;

    public Nir5000SerialSession(Nir5000SerialOptions options)
    {
        if (options.BaudRate <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        port = new SerialPort(options.PortName, options.BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = options.TimeoutMs,
            WriteTimeout = options.TimeoutMs,
            DtrEnable = false,
            RtsEnable = false
        };
        protocol = new Nir5000ProtocolSession(new SerialPortTransport(port));
    }

    public void Open() => protocol.Open();

    public void SendFrame(ReadOnlySpan<byte> header, ReadOnlySpan<byte> payload)
    {
        protocol.SendFrame(header, payload);
    }

    public Nir5000Frame ReceiveFrame(int payloadLength)
    {
        return protocol.ReceiveFrame(payloadLength);
    }

    public void Dispose() => port.Dispose();

    private sealed class SerialPortTransport(SerialPort port) : INir5000ByteTransport
    {
        public bool IsOpen => port.IsOpen;
        public void Open() => port.Open();
        public int ReadByte() => port.ReadByte();
        public void Write(ReadOnlySpan<byte> data) => port.Write(data.ToArray(), 0, data.Length);
        public void Dispose() { }
    }
}
