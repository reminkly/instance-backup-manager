namespace InstanceBackupManager.Processing.Models.Restore;

/// <summary>
/// Describes the result of a completed restore operation.
/// </summary>
public sealed class RestoreResult
{
    #region Properties

    /// <summary>
    /// Gets the name of the backup used for the restore operation.
    /// </summary>
    public required string BackupName { get; init; }

    /// <summary>
    /// Gets the UTC date and time at which the restore operation completed.
    /// </summary>
    public DateTimeOffset CompletedUtc { get; init; }

    /// <summary>
    /// Gets information about each target restored by the operation.
    /// </summary>
    public required IReadOnlyCollection<RestoreResultEntry> Entries { get; init; }

    #endregion
}