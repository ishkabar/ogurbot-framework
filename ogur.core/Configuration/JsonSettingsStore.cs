using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ogur.Abstractions.Configuration;

namespace Ogur.Core.Configuration;

/// <summary>
/// File-based JSON settings store located under %AppData%/Ogur/Fishing/config.user.json.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSettingsStore"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public JsonSettingsStore(ILogger<JsonSettingsStore> logger)
    {
        _logger = logger;
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(root, "Ogur", "Fishing");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "config.user.json");
    }

    /// <inheritdoc />
    public async Task<T?> LoadAsync<T>(string sectionName, CancellationToken ct) where T : class, new()
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(_filePath))
            return default;

        await using var fs = File.OpenRead(_filePath);
        var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty(sectionName, out var section))
            return default;

        return section.Deserialize<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    /// <inheritdoc />
    public async Task SaveAsync<T>(string sectionName, T settings, CancellationToken ct) where T : class, new()
    {
        ct.ThrowIfCancellationRequested();

        Dictionary<string, object?> root;
        if (File.Exists(_filePath))
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            root = string.IsNullOrWhiteSpace(json)
                ? new Dictionary<string, object?>()
                : (JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>());
        }
        else
        {
            root = new Dictionary<string, object?>();
        }

        root[sectionName] = settings;
        var output = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, output, ct);
        _logger.LogInformation("User settings saved to {Path}.", _filePath);
    }
}
