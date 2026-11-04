using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Nir5000.Core;

public sealed record StarLimsUploadOptions(
    Uri Endpoint,
    string AccessKey,
    string SecretKey,
    string ApiMethod = "upload",
    int TimeoutMs = 30000);

public sealed record StarLimsUploadResult(
    bool IsSuccess,
    HttpStatusCode StatusCode,
    string ResponseBody,
    string FileName,
    DateTimeOffset Timestamp);

/// <summary>
/// Uploads a CommaSep2 document to the STARLIMS REST file endpoint using the
/// signing contract reconstructed from sl-source/RestApiHelper.srvscr.
/// </summary>
public sealed class StarLimsUploadClient(HttpClient httpClient, StarLimsUploadOptions options)
{
    public async Task<StarLimsUploadResult> UploadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath)) throw new FileNotFoundException("STARLIMS upload file was not found.", filePath);
        if (string.IsNullOrWhiteSpace(options.AccessKey)) throw new ArgumentException("STARLIMS access key is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.SecretKey)) throw new ArgumentException("STARLIMS secret key is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApiMethod)) throw new ArgumentException("STARLIMS API method is required.", nameof(options));

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var timestamp = DateTimeOffset.UtcNow;
        var timestampText = timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var signature = CreateSignature(options.Endpoint, HttpMethod.Post.Method, options.AccessKey, options.ApiMethod, timestampText, bytes, options.SecretKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content = content;
        request.Headers.TryAddWithoutValidation("SL-API-Auth", options.AccessKey);
        request.Headers.TryAddWithoutValidation("SL-API-Method", options.ApiMethod);
        request.Headers.TryAddWithoutValidation("SL-API-Timestamp", timestampText);
        request.Headers.TryAddWithoutValidation("SL-API-Signature", signature);
        request.Headers.TryAddWithoutValidation("SL-API-Filename", Path.GetFileName(filePath));

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(options.TimeoutMs));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
        return new StarLimsUploadResult(response.IsSuccessStatusCode, response.StatusCode, body, Path.GetFileName(filePath), timestamp);
    }

    public static string CreateSignature(Uri url, string httpVerb, string accessKey, string apiMethod, string timestamp, ReadOnlySpan<byte> payload, string secretKey)
    {
        var meta = $"{url}\n{httpVerb}\n{accessKey}\n{apiMethod}\n{timestamp}";
        var metaBytes = Encoding.UTF8.GetBytes(meta);
        var all = new byte[metaBytes.Length + payload.Length];
        metaBytes.CopyTo(all, 0);
        payload.CopyTo(all.AsSpan(metaBytes.Length));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        return Convert.ToBase64String(hmac.ComputeHash(all));
    }
}
