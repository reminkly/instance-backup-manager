namespace InstanceBackupManager.Processing.Strategies;

/// <summary>
/// Contains aggregate information about files processed by a target-path strategy.
/// </summary>
/// <param name="FileCount">The number of files processed.</param>
/// <param name="TotalBytes">The combined size, in bytes, of the processed files.</param>
internal readonly record struct FileOperationStatistics(
    long FileCount,
    long TotalBytes
);