namespace InstanceBackupManager.Processing.Models.Configuration;

/// <summary>
/// Defines the user-editable configuration for a backup instance.
/// </summary>
public sealed class InstanceConfig
{
    #region Properties

    /// <summary>
    /// Gets the version of the configuration-file schema.
    /// </summary>
    /// <remarks>
    /// The schema version allows future releases to detect, reject, or migrate older configuration formats.
    /// </remarks>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets the user-facing name displayed for the instance.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the instance is available for backup, restore, and other supported operations.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the optional per-kind backup-retention settings for the instance.
    /// </summary>
    /// <remarks>
    /// A missing or <see langword="null"/> value leaves retention unlimited for every backup kind.
    /// </remarks>
    public RetentionSettings? Retention { get; init; } = new();

    /// <summary>
    /// Gets the files and directories managed by the instance.
    /// </summary>
    public List<TargetPath> Targets { get; init; } = [];

    #endregion
}