namespace InstanceBackupManager.Processing.Models.Instances;

/// <summary>
/// Describes an instance directory and skeleton configuration created by the application.
/// </summary>
public sealed class InstanceCreationResult
{
    #region Properties

    /// <summary>
    /// Gets the user-facing name written to the skeleton configuration.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the filesystem-safe directory name assigned to the instance.
    /// </summary>
    public required string FolderName { get; init; }

    /// <summary>
    /// Gets the absolute path of the created instance directory.
    /// </summary>
    public required string InstancePath { get; init; }

    /// <summary>
    /// Gets the absolute path of the created skeleton configuration.
    /// </summary>
    public required string ConfigPath { get; init; }

    #endregion
}
