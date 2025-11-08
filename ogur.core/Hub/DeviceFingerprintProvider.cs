// File: Ogur.Core/Hub/DeviceFingerprintProvider.cs
// Project: Ogur.Core
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Ogur.Abstractions.Hub;

namespace Ogur.Core.Hub;

/// <summary>
/// Provides device fingerprinting using HWID (CPU+Motherboard) and persistent GUID.
/// </summary>
public sealed class DeviceFingerprintProvider : IDeviceFingerprintProvider
{
    private readonly ILogger<DeviceFingerprintProvider> _logger;
    private readonly string _guidFilePath;
    private DeviceFingerprint? _cachedFingerprint;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceFingerprintProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public DeviceFingerprintProvider(ILogger<DeviceFingerprintProvider> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var ogurDir = Path.Combine(appData, "Ogur");
        Directory.CreateDirectory(ogurDir);
        _guidFilePath = Path.Combine(ogurDir, ".device");
    }

    /// <inheritdoc />
    public async Task<DeviceFingerprint> GetFingerprintAsync(CancellationToken ct)
    {
        if (_cachedFingerprint is not null)
            return _cachedFingerprint;

        var hwid = await GetHwidAsync(ct);
        var guid = await GetOrCreateGuidAsync(ct);
        var deviceName = Environment.MachineName;

        _cachedFingerprint = new DeviceFingerprint
        {
            Hwid = hwid,
            Guid = guid,
            DeviceName = deviceName,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        _logger.LogInformation("Device fingerprint generated: HWID={Hwid}, GUID={Guid}", hwid, guid);
        return _cachedFingerprint;
    }

    private async Task<string> GetHwidAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        ct.ThrowIfCancellationRequested();

        try
        {
            var cpuId = GetCpuId();
            var motherboardId = GetMotherboardId();
            var combined = $"{cpuId}|{motherboardId}";
            
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
            return Convert.ToHexString(hash);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate HWID, using fallback");
            return GenerateFallbackHwid();
        }
    }

    private static string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["ProcessorId"]?.ToString() ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
        return string.Empty;
    }

    private static string GetMotherboardId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["SerialNumber"]?.ToString() ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
        return string.Empty;
    }

    private static string GenerateFallbackHwid()
    {
        var fallback = $"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fallback));
        return Convert.ToHexString(hash);
    }

    private async Task<string> GetOrCreateGuidAsync(CancellationToken ct)
    {
        if (File.Exists(_guidFilePath))
        {
            try
            {
                var guid = await File.ReadAllTextAsync(_guidFilePath, ct);
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    _logger.LogDebug("Loaded existing device GUID from {Path}", _guidFilePath);
                    return guid.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read device GUID file, generating new one");
            }
        }

        var newGuid = Guid.NewGuid().ToString("N").ToUpperInvariant();
        try
        {
            await File.WriteAllTextAsync(_guidFilePath, newGuid, ct);
            _logger.LogInformation("Generated and persisted new device GUID to {Path}", _guidFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist device GUID to {Path}", _guidFilePath);
        }

        return newGuid;
    }
}