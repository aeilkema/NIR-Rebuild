namespace Nir5000.Core;

/// <summary>
/// Stable instrument boundary modeled after the legacy NIRSystems driver
/// exports (initialize, reference/sample acquisition and terminate). The
/// transport and wire protocol stay below this boundary.
/// </summary>
public interface INir5000Instrument : IAsyncDisposable
{
    Nir5000InstrumentIdentity Identity { get; }
    bool IsInitialized { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<Spectrum> AcquireReferenceAsync(IReadOnlyList<double> wavelengthsNm, CancellationToken cancellationToken = default);
    Task<Spectrum> AcquireSpectrumAsync(IReadOnlyList<double> wavelengthsNm, CancellationToken cancellationToken = default);
    ValueTask TerminateAsync(CancellationToken cancellationToken = default);
}
