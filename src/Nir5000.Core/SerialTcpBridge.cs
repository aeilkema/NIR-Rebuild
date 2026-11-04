using System.IO.Ports;
using System.Net;
using System.Net.Sockets;

namespace Nir5000.Core;

public sealed record SerialTcpBridgeOptions(
    string PortName,
    int BaudRate,
    int TcpPort,
    IPAddress? ListenAddress = null,
    int ReadTimeoutMs = 250,
    string? TransmitCapturePath = null,
    string? ReceiveCapturePath = null,
    string? WireTracePath = null,
    Action<string>? Log = null);

/// <summary>
/// Transparent raw-byte bridge. It does not translate, impersonate or unlock
/// any vendor protocol; callers must supply a supported protocol on both ends.
/// </summary>
public sealed class SerialTcpBridge(SerialTcpBridgeOptions options) : IAsyncDisposable
{
    private readonly SerialPort serial = CreateSerial(options);
    private readonly TcpListener listener = new(options.ListenAddress ?? IPAddress.Loopback, options.TcpPort);
    private readonly Nir5000WireCapture? capture = CreateCapture(options);
    private CancellationTokenSource? stopSource;

    public bool IsRunning { get; private set; }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) throw new InvalidOperationException("The serial/TCP bridge is already running.");
        stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        options.Log?.Invoke($"COM open requested: {options.PortName} at {options.BaudRate} baud.");
        serial.Open();
        options.Log?.Invoke($"COM opened: {options.PortName}.");
        listener.Start();
        IsRunning = true;
        options.Log?.Invoke($"TCP listener started: {listener.LocalEndpoint}.");
        try
        {
            while (!stopSource.Token.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(stopSource.Token).ConfigureAwait(false);
                client.NoDelay = true;
                options.Log?.Invoke($"TCP client connected: {client.Client.RemoteEndPoint}.");
                await RelayClientAsync(client, stopSource.Token).ConfigureAwait(false);
                options.Log?.Invoke($"TCP client disconnected: {client.Client.RemoteEndPoint}.");
            }
        }
        catch (OperationCanceledException) when (stopSource.Token.IsCancellationRequested)
        {
            options.Log?.Invoke("Bridge cancellation received.");
        }
        catch (Exception ex)
        {
            options.Log?.Invoke($"Bridge relay failure: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        finally
        {
            IsRunning = false;
            listener.Stop();
            if (serial.IsOpen) serial.Close();
            stopSource.Dispose();
            stopSource = null;
            options.Log?.Invoke("Bridge resources closed.");
        }
    }

    public ValueTask DisposeAsync()
    {
        stopSource?.Cancel();
        listener.Stop();
        if (serial.IsOpen) serial.Close();
        serial.Dispose();
        capture?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task RelayClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();
        using var relayStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tcpToSerial = CopyTcpToSerialAsync(stream, relayStop.Token);
        var serialToTcp = CopySerialToTcpAsync(stream, relayStop.Token);
        await Task.WhenAny(tcpToSerial, serialToTcp).ConfigureAwait(false);
        relayStop.Cancel();
        try { await Task.WhenAll(tcpToSerial, serialToTcp).ConfigureAwait(false); }
        catch (OperationCanceledException) when (relayStop.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidOperationException)
        {
            options.Log?.Invoke($"Relay client failure: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task CopyTcpToSerialAsync(NetworkStream stream, CancellationToken token)
    {
        var buffer = new byte[4096];
        while (!token.IsCancellationRequested)
        {
            var count = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (count == 0) return;
            capture?.RecordTransmit(buffer.AsSpan(0, count));
            serial.Write(buffer, 0, count);
        }
    }

    private async Task CopySerialToTcpAsync(NetworkStream stream, CancellationToken token)
    {
        var buffer = new byte[4096];
        while (!token.IsCancellationRequested)
        {
            try
            {
                var count = serial.Read(buffer, 0, buffer.Length);
                if (count > 0)
                {
                    capture?.RecordReceive(buffer.AsSpan(0, count));
                    await stream.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                }
            }
            catch (TimeoutException) { await Task.Yield(); }
        }
    }

    private static Nir5000WireCapture? CreateCapture(SerialTcpBridgeOptions value)
    {
        if ((value.TransmitCapturePath is null) != (value.ReceiveCapturePath is null))
            throw new ArgumentException("Transmit and receive capture paths must be supplied together.", nameof(value));
        return value.TransmitCapturePath is null
            ? null
            : new Nir5000WireCapture(value.TransmitCapturePath, value.ReceiveCapturePath!, value.WireTracePath);
    }

    private static SerialPort CreateSerial(SerialTcpBridgeOptions value)
    {
        if (string.IsNullOrWhiteSpace(value.PortName)) throw new ArgumentException("A COM port name is required.", nameof(value));
        if (value.BaudRate <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Baud rate must be positive.");
        if (value.TcpPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(value), "TCP port is outside the valid range.");
        if (value.ReadTimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Read timeout must be positive.");
        return new SerialPort(value.PortName, value.BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = value.ReadTimeoutMs,
            WriteTimeout = value.ReadTimeoutMs,
            DtrEnable = false,
            RtsEnable = false
        };
    }
}
