# STARLIMS API uploader

NIR-Rebuild contains a reusable `StarLimsUploadClient` and a CLI uploader for
the REST contract found in `sl-source`:

```powershell
Nir5000.Cli.exe --upload-starlims results.txt `
  https://lims.example/rest.web.api/upload `
  ACCESS_KEY SECRET_KEY upload
```

The client sends a `POST` with `Content-Type: application/octet-stream` and
these headers:

- `SL-API-Auth`: access key;
- `SL-API-Method`: API method, default `upload`;
- `SL-API-Timestamp`: UTC `yyyy-MM-ddTHH:mm:ss.fffZ`;
- `SL-API-Signature`: Base64 HMAC-SHA256;
- `SL-API-Filename`: the source file name.

The signature input matches `RestApiHelper.srvscr`: UTF-8 bytes of
`URL + LF + HTTP verb + LF + access key + LF + API method + LF + timestamp`,
followed directly by the binary body. The secret key is never written to the
application log. The client returns the HTTP status and response body so the
NIR-computer log can distinguish authentication, routing and server-side DCU
errors.

The endpoint must be configured on STARLIMS to place the uploaded file where
`FossNirCommaSep2.srvscr` processes it, or to call an equivalent business
wrapper. The source proves the generic REST upload contract, but not a
deployed production URL or server-side destination; those remain deployment
settings.
