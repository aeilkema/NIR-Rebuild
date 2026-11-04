using System.IO.Ports;

namespace Nir5000.Core;

// Sic.dll's statically reconstructed read wrapper uses a 1000 ms timeout.
// Keep it as the compatibility default while allowing diagnostics to override it.
public sealed record Nir5000SerialOptions(
    string PortName,
    int BaudRate,
    int TimeoutMs = 1000,
    string? TransmitCapturePath = null,
    string? ReceiveCapturePath = null,
    string? WireTracePath = null);

public sealed class Nir5000SerialSession : IDisposable
{
    private readonly SerialPort port;
    private readonly Nir5000ProtocolSession protocol;
    private readonly Nir5000WireCapture? capture;

    public Nir5000SerialSession(Nir5000SerialOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PortName)) throw new ArgumentException("A COM port name is required.", nameof(options));
        if (options.BaudRate <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Baud rate must be positive.");
        if (options.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Timeout must be positive.");
        port = new SerialPort(options.PortName, options.BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = options.TimeoutMs,
            WriteTimeout = options.TimeoutMs,
            DtrEnable = false,
            RtsEnable = false
        };
        if ((options.TransmitCapturePath is null) != (options.ReceiveCapturePath is null))
            throw new ArgumentException("Transmit and receive capture paths must be supplied together.", nameof(options));
        capture = options.TransmitCapturePath is not null
            ? new Nir5000WireCapture(options.TransmitCapturePath, options.ReceiveCapturePath!, options.WireTracePath)
            : null;
        protocol = new Nir5000ProtocolSession(new SerialPortTransport(port), capture);
    }

    public bool IsOpen => protocol.IsOpen;
    public void Open() => protocol.Open();

    public void SendFrame(ReadOnlySpan<byte> header, ReadOnlySpan<byte> payload)
    {
        protocol.SendFrame(header, payload);
    }

    public Nir5000Frame ReceiveFrame(int payloadLength)
    {
        return protocol.ReceiveFrame(payloadLength);
    }

    public void Dispose()
    {
        protocol.Dispose();
        capture?.Dispose();
    }

    private sealed class SerialPortTransport(SerialPort port) : INir5000ByteTransport
    {
        public bool IsOpen => port.IsOpen;
        public void Open() => port.Open();
        public int ReadByte() => port.ReadByte();
        public void Write(ReadOnlySpan<byte> data) => port.Write(data.ToArray(), 0, data.Length);
        public void Dispose()
        {
            if (port.IsOpen) port.Close();
            port.Dispose();
        }
    }
}
