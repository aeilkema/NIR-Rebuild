using System.Net;
using System.Net.Http.Json;

namespace Nir5000.Core;

public sealed record NovaOpenInstrumentOptions(
    Uri BaseUri,
    int InstrumentTypeId,
    long ChassisId);

public sealed record NovaSubmitResult(HttpStatusCode StatusCode, string ResponseBody)
{
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;
}

/// <summary>
/// Explicit adapter for Nova's official OpenInstrument HTTP contract. The
/// caller supplies the already-authorized Nova endpoint and identifiers.
/// This class does not create, spoof or discover a protected instrument.
/// </summary>
public sealed class NovaOpenInstrumentAdapter(HttpClient client, NovaOpenInstrumentOptions options)
{
    public async Task<NovaSubmitResult> SubmitSampleAsync(NovaSampleExport sample, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (options.InstrumentTypeId < 0) throw new ArgumentOutOfRangeException(nameof(options), "Instrument type ID must be non-negative.");
        if (options.ChassisId < 0) throw new ArgumentOutOfRangeException(nameof(options), "Chassis ID must be non-negative.");

        var baseUri = options.BaseUri.AbsoluteUri.TrimEnd('/');
        var endpoint = new Uri($"{baseUri}/instrumentTypes/{options.InstrumentTypeId}/instruments/{options.ChassisId}/samples", UriKind.Absolute);
        using var response = await client.PostAsJsonAsync(endpoint, new[] { sample }, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new NovaSubmitResult(response.StatusCode, body);
    }
}
