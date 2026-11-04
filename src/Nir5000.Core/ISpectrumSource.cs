namespace Nir5000.Core;

public interface ISpectrumSource
{
    Task<Spectrum> AcquireAsync(IReadOnlyList<double> wavelengthsNm, CancellationToken cancellationToken = default);
}
