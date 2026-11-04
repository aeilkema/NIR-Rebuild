namespace Nir5000.Core;

public sealed record Nir5000InstrumentIdentity(string Model, string SerialNumber, string Firmware);

/// <summary>
/// Offline instrument substitute for development and regression tests. It
/// models the public spectrum workflow, not unverified wire-level headers.
/// </summary>
public sealed class SimulatedNir5000Device : INir5000Instrument
{
    private readonly ISpectrumSource spectrumSource;

    public SimulatedNir5000Device(ISpectrumSource? spectrumSource = null) =>
        this.spectrumSource = spectrumSource ?? new SimulatedSpectrumSource();

    public Nir5000InstrumentIdentity Identity { get; } =
        new("NIRSystem 5000 (simulator)", "SIM-5000", "offline");

    public bool IsInitialized { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsInitialized = true;
        return Task.CompletedTask;
    }

    public async Task<Spectrum> AcquireSpectrumAsync(IReadOnlyList<double> wavelengthsNm, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized) throw new InvalidOperationException("Initialize the simulated NIR 5000 first.");
        cancellationToken.ThrowIfCancellationRequested();
        return await spectrumSource.AcquireAsync(wavelengthsNm, cancellationToken);
    }

    public Task<Spectrum> AcquireReferenceAsync(IReadOnlyList<double> wavelengthsNm, CancellationToken cancellationToken = default) =>
        AcquireSpectrumAsync(wavelengthsNm, cancellationToken);

    public ValueTask TerminateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsInitialized = false;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => TerminateAsync();
}
