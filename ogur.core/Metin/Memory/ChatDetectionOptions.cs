namespace Ogur.Core.Metin.Memory;


/// <summary>
/// Configuration options for differential chat buffer detection algorithm.
/// Controls memory scan range, snapshot count, and change detection thresholds.
/// </summary>
public sealed class ChatDetectionOptions
{
    /// <summary>
    /// Gets or initializes the start address for memory scanning.
    /// Default: 0x0CC00000 (based on Metin2 typical memory layout).
    /// </summary>
    public nint ScanStart { get; init; } = 0x0CC00000;

    /// <summary>
    /// Gets or initializes the end address for memory scanning.
    /// Default: 0x0D600000 (10 MB range from ScanStart).
    /// </summary>
    public nint ScanEnd { get; init; } = 0x0D600000;

    /// <summary>
    /// Gets or initializes the number of memory snapshots to take during scanning.
    /// More snapshots = more accurate but slower detection.
    /// Default: 100
    /// </summary>
    public int SnapshotCount { get; init; } = 100;

    /// <summary>
    /// Gets or initializes the interval between snapshots in milliseconds.
    /// Default: 50ms (100 snapshots = 5 seconds total scan time).
    /// </summary>
    public int IntervalMs { get; init; } = 50;

    /// <summary>
    /// Gets or initializes the chunk size for memory reads in bytes.
    /// Must be aligned with memory page size for compatibility.
    /// Default: 4096 (4 KB - standard memory page size).
    /// </summary>
    public int ReadChunkSize { get; init; } = 4 * 1024;

    /// <summary>
    /// Gets or initializes the minimum change count threshold for valid hotspots.
    /// Regions with fewer changes are filtered out as noise.
    /// Default: 10
    /// </summary>
    public int MinChangeCount { get; init; } = 10;

    /// <summary>
    /// Gets or initializes the maximum gap between changed bytes to group into same region.
    /// Default: 1024 bytes (1 KB).
    /// </summary>
    public int RegionGroupingGap { get; init; } = 1024;
}