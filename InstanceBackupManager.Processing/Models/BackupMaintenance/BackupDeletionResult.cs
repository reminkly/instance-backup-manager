namespace InstanceBackupManager.Processing.Models.BackupMaintenance;

/// <summary>
/// Describes the result of a completed backup-deletion operation.
/// </summary>
public sealed class BackupDeletionResult
{
    #region Properties

    /// <summary>
    /// Gets the UTC date and time at which the deletion operation completed.
    /// </summary>
    public DateTimeOffset CompletedUtc { get; init; }

    /// <summary>
    /// Gets information about each backup deleted by the operation.
    /// </summary>
    public required IReadOnlyCollection<BackupDeletionResultEntry> Entries { get; init; }

    #endregion
}