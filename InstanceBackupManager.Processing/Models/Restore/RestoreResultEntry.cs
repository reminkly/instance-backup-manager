using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.Restore;

/// <summary>
/// Describes a configured target restored from a backup.
/// </summary>
public sealed class RestoreResultEntry
{
    #region Properties

    /// <summary>
    /// Gets the stable identifier of the restored target.
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>
    /// Gets the current display name of the restored target.
    /// </summary>
    public required string TargetName { get; init; }

    /// <summary>
    /// Gets the current resolved destination path used for the restore operation.
    /// </summary>
    public required string DestinationPath { get; init; }

    /// <summary>
    /// Gets the type of filesystem entry restored for the target.
    /// </summary>
    public TargetPathType Type { get; init; }

    /// <summary>
    /// Gets the number of files restored for the target.
    /// </summary>
    public long FileCount { get; init; }

    /// <summary>
    /// Gets the combined size, in bytes, of the restored files.
    /// </summary>
    public long TotalBytes { get; init; }

    #endregion
}