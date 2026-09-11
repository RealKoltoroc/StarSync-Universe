using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace StarSyncUniverse.Services;

public sealed record UniverseSyncHostProbeResult(
    bool Success,
    bool ManifestAvailable,
    int ManifestHttpStatus,
    int RegistryHttpStatus,
    string RegistryState,
    string Message,
    string ClientId,
    string Fingerprint);

/// <summary>
/// Minimal StarSyncUniverse SyncHost transport using the exact StarSync WPF client request-signing
/// contract (starsync-client-request-v1 / ECDSA-P256-SHA256). Bookmark group payload publication is
/// kept separate from the connection probe so the server-side bookmark module can be introduced
/// without inventing a second authentication mechanism.
/// </summary>
public sealed class UniverseSyncHostClientService
{
    private readonly UniverseSyncHostClientIdentityService _identityService;

    public UniverseSyncHostClientService(UniverseSyncHostClientIdentityService? identityService = null)
    {
        _identityService = identityService ?? new UniverseSyncHostClientIdentityService();
    }

    public async Task<UniverseSyncHostProbeResult> ProbeAsync(StarSyncUniverseSettings settings, CancellationToken cancellationToken = default)
    {
        var identity = _identityService.GetOrCreate();
        if (!settings.SyncHostEnabled || string.IsNullOrWhiteSpace(settings.SyncHostBaseUrl))
            return new(false, false, 0, 0, "OFF", "SyncHost is disabled or no base URL is configured.", identity.ClientId, identity.PublicKeyFingerprint);

        try
        {
            using var client = CreateClient(settings);
            var manifestUrl = Combine(settings.SyncHostBaseUrl, "sync/sync-host-manifest.json");
            using var manifestRequest = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
            ApplySignedRequestMetadata(manifestRequest, settings, "sync-host", "manifest-probe", identity);
            using var manifestResponse = await client.SendAsync(manifestRequest, cancellationToken).ConfigureAwait(false);
            var manifestStatus = (int)manifestResponse.StatusCode;
            var manifestAvailable = manifestResponse.IsSuccessStatusCode;
            if (!manifestAvailable)
                return new(false, false, manifestStatus, 0, "UNKNOWN", $"SyncHost manifest HTTP {manifestStatus}.", identity.ClientId, identity.PublicKeyFingerprint);

            var meUrl = Combine(settings.SyncHostBaseUrl, "api/v1/clients/me") + "?clientId=" + Uri.EscapeDataString(identity.ClientId);
            using var meRequest = new HttpRequestMessage(HttpMethod.Get, meUrl);
            ApplySignedRequestMetadata(meRequest, settings, "registry", "get-request", identity);
            using var meResponse = await client.SendAsync(meRequest, cancellationToken).ConfigureAwait(false);
            var registryStatus = (int)meResponse.StatusCode;
            var registryText = meResponse.Content is null ? string.Empty : await meResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var registryState = meResponse.IsSuccessStatusCode ? "VALID/KNOWN" : registryStatus is 401 or 403 ? "NOT_AUTHORIZED" : registryStatus == 404 ? "NOT_REGISTERED" : "UNAVAILABLE";
            var success = manifestAvailable && meResponse.IsSuccessStatusCode;
            var message = success
                ? "SyncHost manifest and signed client registry status are reachable."
                : $"SyncHost manifest reachable; signed registry state is {registryState} (HTTP {registryStatus}).";
            if (!string.IsNullOrWhiteSpace(registryText) && registryText.Length < 240)
                message += " " + registryText.Trim();
            return new(success, true, manifestStatus, registryStatus, registryState, message, identity.ClientId, identity.PublicKeyFingerprint);
        }
        catch (Exception ex)
        {
            return new(false, false, 0, 0, "ERROR", ex.Message, identity.ClientId, identity.PublicKeyFingerprint);
        }
    }

    private static HttpClient CreateClient(StarSyncUniverseSettings settings)
    {
        var handler = new HttpClientHandler();
        var client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("StarSyncUniverse/0.7");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private void ApplySignedRequestMetadata(
        HttpRequestMessage request,
        StarSyncUniverseSettings settings,
        string module,
        string operation,
        UniverseSyncHostClientIdentity identity)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");
        var fingerprint = NormalizeFingerprint(identity.PublicKeyFingerprint);
        var target = NormalizeSignedRequestTarget(request.RequestUri);
        var method = request.Method.Method.ToUpperInvariant();
        var signingInput = string.Join("\n", new[]
        {
            "starsync-client-request-v1",
            identity.ClientId,
            fingerprint,
            method,
            target,
            timestamp,
            nonce,
            module,
            operation
        });
        var signature = _identityService.Sign(identity, Encoding.UTF8.GetBytes(signingInput));
        var messageId = "<starsync-header-" + nonce + "@" + identity.ClientId + ">";

        Add(request.Headers, "X-StarSync-Client", identity.ClientId);
        Add(request.Headers, "X-StarSync-Client-Display", identity.ClientId);
        Add(request.Headers, "X-StarSync-Client-Id", identity.ClientId);
        Add(request.Headers, "X-StarSync-Client-Fingerprint", identity.PublicKeyFingerprint);
        Add(request.Headers, "X-StarSync-Signature-Algorithm", identity.SignatureAlgorithm);
        Add(request.Headers, "X-StarSync-AS2-Lite", "starsync.as2-lite.v1");
        Add(request.Headers, "X-StarSync-Message-Id", messageId);
        Add(request.Headers, "X-StarSync-Timestamp-Utc", timestamp);
        Add(request.Headers, "X-StarSync-Nonce", nonce);
        Add(request.Headers, "X-StarSync-Player", string.IsNullOrWhiteSpace(settings.SyncHostPlayerHandle) ? Environment.UserName : settings.SyncHostPlayerHandle);
        Add(request.Headers, "X-StarSync-Organization", settings.SyncHostOrganization);
        Add(request.Headers, "X-StarSync-Scope", "personal");
        Add(request.Headers, "X-StarSync-Module", module);
        Add(request.Headers, "X-StarSync-Operation", operation);
        Add(request.Headers, "X-StarSync-Version", typeof(UniverseSyncHostClientService).Assembly.GetName().Version?.ToString() ?? "0.7");
        Add(request.Headers, "X-StarSync-Transport", "rest-as2");
        Add(request.Headers, "X-StarSync-Capability-Probe", "starsync.sync-host-manifest.v1");
        Add(request.Headers, "X-StarSync-Request-Signature-Version", "1");
        Add(request.Headers, "X-StarSync-Request-Target", target);
        Add(request.Headers, "X-StarSync-Request-Signature", signature);
    }

    private static string Combine(string baseUrl, string relative)
    {
        var normalized = baseUrl.Trim();
        if (!normalized.EndsWith('/')) normalized += "/";
        var baseUri = new Uri(normalized, UriKind.Absolute);
        if (baseUri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("SyncHost base URL must use HTTP or HTTPS.");
        if (!string.IsNullOrEmpty(baseUri.UserInfo))
            throw new InvalidOperationException("SyncHost base URL must not contain embedded credentials.");
        return new Uri(baseUri, relative.TrimStart('/')).AbsoluteUri;
    }

    private static string NormalizeSignedRequestTarget(Uri? uri)
    {
        var pathAndQuery = uri?.PathAndQuery ?? "/";
        var queryIndex = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        var path = queryIndex >= 0 ? pathAndQuery[..queryIndex] : pathAndQuery;
        var query = queryIndex >= 0 ? pathAndQuery[queryIndex..] : string.Empty;
        foreach (var marker in new[] { "/api/", "/sync/" })
        {
            var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex > 0) { path = path[markerIndex..]; break; }
        }
        return path + query;
    }

    private static string NormalizeFingerprint(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.StartsWith("sha256:", StringComparison.Ordinal)) return normalized[7..];
        if (normalized.StartsWith("sha-256:", StringComparison.Ordinal)) return normalized[8..];
        return normalized;
    }

    private static void Add(HttpRequestHeaders headers, string name, string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0) return;
        headers.Remove(name);
        headers.TryAddWithoutValidation(name, normalized);
    }
}
