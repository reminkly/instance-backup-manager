using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.Configuration;

/// <summary>
/// Defines a file or directory that can be backed up, restored, and optionally cleared.
/// </summary>
public sealed class TargetPath
{
    #region Properties

    /// <summary>
    /// Gets the stable, machine-readable identifier for the target.
    /// </summary>
    /// <remarks>
    /// The identifier should remain unchanged when the display name changes and must be unique within its instance.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the user-facing name displayed for the target.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the target participates in backup, restore, and other supported operations.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the backup must fail when the configured source does not exist.
    /// </summary>
    /// <remarks>
    /// When this value is <see langword="false"/>, a missing source is skipped and omitted from the backup manifest.
    /// </remarks>
    public bool Required { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the source file or directory may be cleared by the application.
    /// </summary>
    /// <remarks>
    /// This setting only grants permission for the clear operation to be offered. The application must still validate
    /// the resolved source path and require confirmation before deleting any data.
    /// </remarks>
    public bool AllowClear { get; init; }

    /// <summary>
    /// Gets the configured source file or directory path.
    /// </summary>
    /// <remarks>
    /// The value may contain environment variables. Relative paths are resolved against the containing instance directory.
    /// </remarks>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the type of filesystem entry represented by the source path.
    /// </summary>
    public TargetPathType Type { get; init; } = TargetPathType.Unknown;

    /// <summary>
    /// Gets the relative destination used to store the target inside each timestamped backup directory.
    /// </summary>
    /// <remarks>
    /// The value must be relative and must not escape the backup directory through parent traversal.
    /// </remarks>
    public required string BackupPath { get; init; }

    #endregion
}