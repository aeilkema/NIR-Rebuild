namespace Nir5000.Core;

public sealed class SimulatedSpectrumSource : ISpectrumSource
{
    public Task<Spectrum> AcquireAsync(IReadOnlyList<double> wavelengthsNm, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var values = wavelengthsNm.Select(w => 0.2 + 0.75 * Math.Exp(-Math.Pow((w - 1940) / 180, 2)) + 0.03 * Math.Sin(w / 23)).ToArray();
        return Task.FromResult(new Spectrum(DateTimeOffset.UtcNow, wavelengthsNm, values));
    }
}
