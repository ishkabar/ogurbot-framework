using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Abstractions.Security;

namespace Ogur.Core.Security;

/// <summary>
/// AES-CBC encryption manager with PBKDF2 key derivation and Base64 payloads.
/// </summary>
public sealed class EncryptionManager : IEncryptionManager
{
    private static readonly Encoding TextEncoding = Encoding.Unicode;

    private static readonly byte[] Salt =
    {
        0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64,
        0x76, 0x65, 0x64, 0x65, 0x76
    };

    private const int Iterations = 200_000;
    private static readonly HashAlgorithmName Pbkdf2Hash = HashAlgorithmName.SHA256;

    private readonly string _keyMaterial;
    private readonly ILogger<EncryptionManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionManager"/> class.
    /// </summary>
    /// <param name="options">Encryption options.</param>
    /// <param name="logger">Logger instance.</param>
    public EncryptionManager(IOptions<EncryptionOptions> options, ILogger<EncryptionManager> logger)
    {
        _logger = logger;
        _keyMaterial = ResolveKey(options.Value);
    }

    /// <inheritdoc />
    public Task<string> EncryptAsync(string plainText, CancellationToken ct)
    {
        if (plainText is null) throw new ArgumentNullException(nameof(plainText));
        ct.ThrowIfCancellationRequested();

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var pdb = new Rfc2898DeriveBytes(_keyMaterial, Salt, Iterations, Pbkdf2Hash);
        aes.Key = pdb.GetBytes(32);
        aes.IV = pdb.GetBytes(16);

        var clearBytes = TextEncoding.GetBytes(plainText);
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(clearBytes, 0, clearBytes.Length);
        }
        return Task.FromResult(Convert.ToBase64String(ms.ToArray()));
    }

    /// <inheritdoc />
    public Task<string> DecryptAsync(string cipherText, CancellationToken ct)
    {
        if (cipherText is null) throw new ArgumentNullException(nameof(cipherText));
        ct.ThrowIfCancellationRequested();

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var pdb = new Rfc2898DeriveBytes(_keyMaterial, Salt, Iterations, Pbkdf2Hash);
        aes.Key = pdb.GetBytes(32);
        aes.IV = pdb.GetBytes(16);

        var cipherBytes = Convert.FromBase64String(cipherText);
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
        {
            cs.Write(cipherBytes, 0, cipherBytes.Length);
        }
        return Task.FromResult(TextEncoding.GetString(ms.ToArray()));
    }

    private string ResolveKey(EncryptionOptions opts)
    {
        var fromEnv = Environment.GetEnvironmentVariable(opts.EnvVarName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            _logger.LogInformation("Encryption key loaded from environment variable {Env}.", opts.EnvVarName);
            return fromEnv!;
        }

        if (!string.IsNullOrWhiteSpace(opts.Key))
        {
            _logger.LogInformation("Encryption key loaded from configuration.");
            return opts.Key!;
        }

        _logger.LogWarning("Encryption key not supplied; using embedded fallback key.");
        return "OGURBOT_FALLBACK_KEY_CHANGE_ME_!2025";
    }
}
