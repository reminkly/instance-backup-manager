using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.BackupMaintenance;

/// <summary>
/// Describes a completed backup deleted by a backup-maintenance operation.
/// </summary>
public sealed class BackupDeletionResultEntry
{
    #region Properties

    /// <summary>
    /// Gets the directory name assigned to the deleted backup.
    /// </summary>
    public required string BackupName { get; init; }

    /// <summary>
    /// Gets the absolute path previously occupied by the deleted backup.
    /// </summary>
    public required string BackupPath { get; init; }

    /// <summary>
    /// Gets the reason the deleted backup was originally created.
    /// </summary>
    public BackupKind Kind { get; init; }

    /// <summary>
    /// Gets the UTC date and time at which the deleted backup was created.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>
    /// Gets the number of files removed from the backup directory.
    /// </summary>
    public long FileCount { get; init; }

    /// <summary>
    /// Gets the combined size, in bytes, of all files removed from the backup directory.
    /// </summary>
    public long TotalBytes { get; init; }

    #endregion
}