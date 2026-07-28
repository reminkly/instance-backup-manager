namespace InstanceBackupManager.Processing.Models.Configuration;

/// <summary>
/// Defines the maximum number of completed backups retained for each backup kind.
/// </summary>
public sealed class RetentionSettings
{
    #region Properties

    /// <summary>
    /// Gets the maximum number of manual backups retained for the instance.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value retains manual backups indefinitely. A configured value must be at least one.
    /// </remarks>
    public int? ManualBackupsToKeep { get; init; }

    /// <summary>
    /// Gets the maximum number of pre-restore backups retained for the instance.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value retains pre-restore backups indefinitely. A configured value must be at least one.
    /// </remarks>
    public int? PreRestoreBackupsToKeep { get; init; }

    #endregion
}