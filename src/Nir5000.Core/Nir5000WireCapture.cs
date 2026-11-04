namespace Nir5000.Core;

public interface INir5000WireRecorder
{
    void RecordTransmit(ReadOnlySpan<byte> data);
    void RecordReceive(byte value);
}

/// <summary>Writes raw transmit and receive bytes to separate capture files.</summary>
public sealed class Nir5000WireCapture : INir5000WireRecorder, IDisposable
{
    private readonly FileStream transmit;
    private readonly FileStream receive;
    private readonly object sync = new();

    public Nir5000WireCapture(string transmitPath, string receivePath)
    {
        if (string.IsNullOrWhiteSpace(transmitPath)) throw new ArgumentException("A transmit capture path is required.", nameof(transmitPath));
        if (string.IsNullOrWhiteSpace(receivePath)) throw new ArgumentException("A receive capture path is required.", nameof(receivePath));
        transmit = new FileStream(transmitPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        receive = new FileStream(receivePath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    public void RecordTransmit(ReadOnlySpan<byte> data)
    {
        lock (sync) transmit.Write(data);
    }

    public void RecordReceive(byte value)
    {
        lock (sync) receive.WriteByte(value);
    }

    public void RecordReceive(ReadOnlySpan<byte> data)
    {
        lock (sync) receive.Write(data);
    }

    public void Dispose()
    {
        lock (sync)
        {
            transmit.Dispose();
            receive.Dispose();
        }
    }
}
