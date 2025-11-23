using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Abstractions.Memory;


namespace Ogur.Core.Metin.Memory;


/// <summary>
/// Detects chat buffer addresses using differential memory scanning technique.
/// Takes multiple memory snapshots and compares them byte-by-byte to find frequently changing regions (chat buffers).
/// Validates detected regions by checking for Metin2 chat color code pattern (|cff).
/// </summary>
public sealed class DifferentialChatBufferDetector : IChatBufferDetector
{
    private readonly ILogger<DifferentialChatBufferDetector> _logger;
    private readonly ChatDetectionOptions _options;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess,
        nint lpBaseAddress,
        byte[] lpBuffer,
        int dwSize,
        out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        VMRead = 0x0010,
        QueryInformation = 0x0400
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DifferentialChatBufferDetector"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <param name="options">Configuration options for detection algorithm.</param>
    public DifferentialChatBufferDetector(
        ILogger<DifferentialChatBufferDetector> logger,
        IOptions<ChatDetectionOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<ChatBufferInfo?> DetectAsync(int processId, CancellationToken ct)
    {
        _logger.LogInformation(
            "Starting chat buffer detection for PID {ProcessId}. Scan range: 0x{Start:X8}-0x{End:X8} ({Size:F2} MB)",
            processId,
            _options.ScanStart,
            _options.ScanEnd,
            (_options.ScanEnd - _options.ScanStart) / 1024.0 / 1024.0);

        IntPtr hProcess = IntPtr.Zero;

        try
        {
            hProcess = OpenProcess(
                ProcessAccessFlags.VMRead | ProcessAccessFlags.QueryInformation,
                false,
                processId);

            if (hProcess == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogError("Failed to open process {ProcessId}. Win32 error: {Error}", processId, error);
                return null;
            }

            _logger.LogDebug("Process handle opened successfully");

            var hotspots = await DifferentialScanAsync(hProcess, ct);

            if (hotspots.Count == 0)
            {
                _logger.LogWarning("No chat buffer hotspots detected in process {ProcessId}", processId);
                return null;
            }

            var best = hotspots[0];
            var messageStart = best.Address;
            var digitAddress = messageStart + 0x14;

            _logger.LogInformation(
                "✅ Chat buffer detected: MessageStart=0x{MessageStart:X8}, Digit=0x{Digit:X8}, Changes={Changes}, Sample: {Sample}",
                messageStart,
                digitAddress,
                best.ChangeCount,
                best.SampleText.Length > 50 ? best.SampleText.Substring(0, 50) + "..." : best.SampleText);

            return new ChatBufferInfo(messageStart, digitAddress, best.ChangeCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Chat buffer detection cancelled for process {ProcessId}", processId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat buffer detection failed for process {ProcessId}", processId);
            return null;
        }
        finally
        {
            if (hProcess != IntPtr.Zero)
            {
                CloseHandle(hProcess);
            }
        }
    }

    /// <summary>
    /// Performs differential memory scanning by taking multiple snapshots and comparing byte-level changes.
    /// </summary>
    /// <param name="hProcess">Process handle with VMRead access.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of hotspots sorted by change count (most active first).</returns>
    private async Task<List<Hotspot>> DifferentialScanAsync(IntPtr hProcess, CancellationToken ct)
    {
        var totalBytes = (int)(_options.ScanEnd - _options.ScanStart);
        var snapshots = new List<byte[]>();

        _logger.LogDebug(
            "Taking {Count} snapshots with {Interval}ms intervals (total scan time: ~{Duration}s)",
            _options.SnapshotCount,
            _options.IntervalMs,
            _options.SnapshotCount * _options.IntervalMs / 1000.0);

        // Phase 1: Take snapshots
        for (var i = 0; i < _options.SnapshotCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var snapshot = ReadMemoryRegion(hProcess, _options.ScanStart, totalBytes);
            snapshots.Add(snapshot);

            if (i < _options.SnapshotCount - 1)
            {
                await Task.Delay(_options.IntervalMs, ct);
            }

            if ((i + 1) % 20 == 0)
            {
                _logger.LogDebug("Snapshot progress: {Current}/{Total}", i + 1, _options.SnapshotCount);
            }
        }

        _logger.LogDebug("✅ {Count} snapshots collected, analyzing changes...", snapshots.Count);

        // Phase 2: Compare snapshots byte-by-byte
        var changeMap = new Dictionary<int, int>();

        for (var i = 1; i < snapshots.Count; i++)
        {
            var prev = snapshots[i - 1];
            var curr = snapshots[i];

            for (var offset = 0; offset < Math.Min(prev.Length, curr.Length); offset++)
            {
                if (prev[offset] != curr[offset])
                {
                    changeMap[offset] = changeMap.GetValueOrDefault(offset, 0) + 1;
                }
            }
        }

        _logger.LogDebug("Found {Count:N0} bytes with changes", changeMap.Count);

        // Phase 3: Group into contiguous regions
        var regions = GroupIntoRegions(changeMap);
        _logger.LogDebug("Grouped into {Count} contiguous regions", regions.Count);

        // Phase 4: Test offsets and filter to chat buffers only
        var hotspots = new List<Hotspot>();

        
        foreach (var (start, changeCount) in regions)
        {
            ct.ThrowIfCancellationRequested();

            var regionAddress = _options.ScanStart + start;

            // Test offset +9
            var testAddress9 = regionAddress + 0x9;
            var sample9 = ReadString(hProcess, testAddress9, 80);

            // Test offset +10
            var testAddress10 = regionAddress + 0xA;
            var sample10 = ReadString(hProcess, testAddress10, 80);

            // Który zaczyna się od "|cff"?
            nint messageStartAddr;
            string fullSample;

            if (!string.IsNullOrEmpty(sample9) && sample9.StartsWith("|cff"))
            {
                messageStartAddr = testAddress9;
                fullSample = sample9;
            }
            else if (!string.IsNullOrEmpty(sample10) && sample10.StartsWith("|cff"))
            {
                messageStartAddr = testAddress10;
                fullSample = sample10;
            }
            else
            {
                continue; // Skip region - no |cff
            }

            // Found first match - return it
            hotspots.Add(new Hotspot(messageStartAddr, changeCount, fullSample));
        }

// Return filtered (już posortowane bo regions były posortowane)
        var filteredHotspots = hotspots
            .Where(h => h.ChangeCount >= _options.MinChangeCount)
            .ToList();

        return filteredHotspots;
    }

    /// <summary>
    /// Reads entire memory region in chunks to avoid access violations.
    /// </summary>
    /// <param name="hProcess">Process handle.</param>
    /// <param name="baseAddress">Start address to read from.</param>
    /// <param name="totalSize">Total size to read in bytes.</param>
    /// <returns>Byte array containing memory contents (zero-filled for unreadable regions).</returns>
    private byte[] ReadMemoryRegion(IntPtr hProcess, nint baseAddress, int totalSize)
    {
        var result = new byte[totalSize];
        var totalRead = 0;

        for (nint addr = baseAddress; addr < baseAddress + totalSize; addr += _options.ReadChunkSize)
        {
            var sizeToRead = (int)Math.Min(_options.ReadChunkSize, (baseAddress + totalSize) - addr);
            var offsetInResult = (int)(addr - baseAddress);

            var chunk = new byte[sizeToRead];

            if (ReadProcessMemory(hProcess, addr, chunk, sizeToRead, out var bytesRead) && bytesRead > 0)
            {
                Array.Copy(chunk, 0, result, offsetInResult, bytesRead);
                totalRead += bytesRead;
            }
            // Failed reads leave zeros in result array
        }

        return result;
    }

    /// <summary>
    /// Groups individual changing bytes into contiguous regions based on proximity.
    /// </summary>
    /// <param name="changeMap">Map of offset → change count.</param>
    /// <returns>List of regions (start offset, total change count).</returns>
    private List<(int Start, int ChangeCount)> GroupIntoRegions(Dictionary<int, int> changeMap)
    {
        var regions = new List<(int Start, int ChangeCount)>();
        var sortedOffsets = changeMap.Keys.OrderBy(x => x).ToList();

        if (sortedOffsets.Count == 0)
            return regions;

        var regionStart = sortedOffsets[0];
        var regionChangeCount = changeMap[sortedOffsets[0]];
        var lastOffset = sortedOffsets[0];

        for (var i = 1; i < sortedOffsets.Count; i++)
        {
            var offset = sortedOffsets[i];

            if (offset - lastOffset < _options.RegionGroupingGap)
            {
                // Contiguous - extend current region
                regionChangeCount += changeMap[offset];
                lastOffset = offset;
            }
            else
            {
                // Gap too large - save current region and start new one
                if (regionChangeCount >= _options.MinChangeCount)
                {
                    regions.Add((regionStart, regionChangeCount));
                }

                regionStart = offset;
                regionChangeCount = changeMap[offset];
                lastOffset = offset;
            }
        }

        // Don't forget last region
        if (regionChangeCount >= _options.MinChangeCount)
        {
            regions.Add((regionStart, regionChangeCount));
        }

        return regions.OrderByDescending(r => r.ChangeCount).ToList();
    }

    /// <summary>
    /// Reads null-terminated string from process memory using Windows-1250 encoding (Polish).
    /// </summary>
    /// <param name="hProcess">Process handle.</param>
    /// <param name="address">Memory address to read from.</param>
    /// <param name="maxLength">Maximum length to read.</param>
    /// <returns>Decoded string or empty string on failure.</returns>
    private string ReadString(IntPtr hProcess, nint address, int maxLength)
    {
        var buffer = new byte[maxLength];

        if (!ReadProcessMemory(hProcess, address, buffer, maxLength, out var bytesRead) || bytesRead == 0)
        {
            return string.Empty;
        }

        // Find null terminator
        var nullIndex = Array.IndexOf(buffer, (byte)0, 0, bytesRead);
        var length = nullIndex >= 0 ? nullIndex : bytesRead;

        if (length == 0)
            return string.Empty;

        try
        {
            // Register Windows-1250 encoding provider (Polish characters)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = Encoding.GetEncoding(1250);
            return encoding.GetString(buffer, 0, length);
        }
        catch
        {
            return "[encoding error]";
        }
    }

    /// <summary>
    /// Internal record representing a detected memory hotspot.
    /// </summary>
    /// <param name="Address">Memory address of the hotspot.</param>
    /// <param name="ChangeCount">Number of changes detected.</param>
    /// <param name="SampleText">Sample text read from this location.</param>
    private sealed record Hotspot(nint Address, int ChangeCount, string SampleText);
}