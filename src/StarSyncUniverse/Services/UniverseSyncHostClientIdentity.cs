using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StarSyncUniverse.Services;

public sealed record UniverseSyncHostClientIdentity(
    string Schema,
    string ClientId,
    string PublicKeySpkiBase64,
    string PublicKeyFingerprint,
    string ProtectedPrivateKeyPkcs8Base64,
    string SignatureAlgorithm,
    string KeyPurpose,
    DateTimeOffset CreatedUtc);

/// <summary>
/// SyncHost-compatible persistent client identity. The wire contract intentionally matches the
/// StarSync WPF client: ECDSA P-256/SHA-256, client id derived from SPKI SHA-256 and a DPAPI-protected
/// PKCS#8 private key that never leaves the local user profile.
/// </summary>
public sealed class UniverseSyncHostClientIdentityService
{
    public const string IdentitySchema = "starsync.sync-host.client-identity.v1";
    public const string SignatureAlgorithm = "ECDSA-P256-SHA256";
    public const string KeyPurpose = "starsync-sync-host-rest-as2-lite";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public UniverseSyncHostClientIdentityService()
    {
        RootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "SyncHost");
        IdentityPath = Path.Combine(RootPath, "client-identity.json");
    }

    public string RootPath { get; }
    public string IdentityPath { get; }

    public UniverseSyncHostClientIdentity GetOrCreate()
    {
        var existing = TryRead();
        if (existing is not null) return existing;

        Directory.CreateDirectory(RootPath);
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKey = signer.ExportPkcs8PrivateKey();
        var publicKey = signer.ExportSubjectPublicKeyInfo();
        var identity = new UniverseSyncHostClientIdentity(
            IdentitySchema,
            BuildClientId(publicKey),
            Convert.ToBase64String(publicKey),
            BuildFingerprint(publicKey),
            Convert.ToBase64String(ProtectedData.Protect(privateKey, null, DataProtectionScope.CurrentUser)),
            SignatureAlgorithm,
            KeyPurpose,
            DateTimeOffset.UtcNow);
        File.WriteAllText(IdentityPath, JsonSerializer.Serialize(identity, JsonOptions), Encoding.UTF8);
        return identity;
    }

    public UniverseSyncHostClientIdentity? TryRead()
    {
        try
        {
            if (!File.Exists(IdentityPath)) return null;
            var identity = JsonSerializer.Deserialize<UniverseSyncHostClientIdentity>(File.ReadAllText(IdentityPath, Encoding.UTF8), JsonOptions);
            if (identity is null || identity.Schema != IdentitySchema || string.IsNullOrWhiteSpace(identity.ClientId)) return null;
            var publicKey = Convert.FromBase64String(identity.PublicKeySpkiBase64);
            if (!string.Equals(identity.ClientId, BuildClientId(publicKey), StringComparison.OrdinalIgnoreCase)) return null;
            using var signer = ECDsa.Create();
            signer.ImportPkcs8PrivateKey(
                ProtectedData.Unprotect(Convert.FromBase64String(identity.ProtectedPrivateKeyPkcs8Base64), null, DataProtectionScope.CurrentUser),
                out _);
            return identity;
        }
        catch
        {
            return null;
        }
    }

    public string Sign(UniverseSyncHostClientIdentity identity, ReadOnlySpan<byte> payload)
    {
        using var signer = ECDsa.Create();
        var privateKey = ProtectedData.Unprotect(
            Convert.FromBase64String(identity.ProtectedPrivateKeyPkcs8Base64),
            null,
            DataProtectionScope.CurrentUser);
        signer.ImportPkcs8PrivateKey(privateKey, out _);
        return Convert.ToBase64String(signer.SignData(payload, HashAlgorithmName.SHA256));
    }

    public static bool Verify(string publicKeySpkiBase64, ReadOnlySpan<byte> payload, string signatureBase64)
    {
        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeySpkiBase64), out _);
            return verifier.VerifyData(payload, Convert.FromBase64String(signatureBase64), HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    public static string ComputeSha256Hex(ReadOnlySpan<byte> payload) =>
        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    private static string BuildClientId(byte[] publicKey) =>
        "starsync-" + Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant()[..24];

    private static string BuildFingerprint(byte[] publicKey) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
}
