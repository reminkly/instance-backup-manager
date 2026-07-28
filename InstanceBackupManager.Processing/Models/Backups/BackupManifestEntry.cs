using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.Backups;

/// <summary>
/// Describes a configured target stored within a completed backup.
/// </summary>
public sealed class BackupManifestEntry
{
    #region Properties

    /// <summary>
    /// Gets the stable identifier of the configured target.
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>
    /// Gets the display name of the configured target at the time the backup was created.
    /// </summary>
    public required string TargetName { get; init; }

    /// <summary>
    /// Gets the configured source path recorded at the time the backup was created.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the type of filesystem entry stored in the backup.
    /// </summary>
    public TargetPathType Type { get; init; }

    /// <summary>
    /// Gets the relative path at which the target is stored inside the backup directory.
    /// </summary>
    public required string BackupPath { get; init; }

    /// <summary>
    /// Gets the number of files stored for the target.
    /// </summary>
    public long FileCount { get; init; }

    /// <summary>
    /// Gets the combined size, in bytes, of the files stored for the target.
    /// </summary>
    public long TotalBytes { get; init; }

    #endregion
}