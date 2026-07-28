using InstanceBackupManager.Processing.Models.Configuration;

namespace InstanceBackupManager.Processing.Models.Instances;

/// <summary>
/// Represents a loaded instance configuration together with the filesystem paths discovered at runtime.
/// </summary>
/// <remarks>
/// This model is used only while the application is running and is not serialized into <c>instance.json</c>.
/// </remarks>
public sealed class InstanceContext
{
    #region Properties

    /// <summary>
    /// Gets the absolute path of the directory containing the instance configuration and backups directory.
    /// </summary>
    public required string InstancePath { get; init; }

    /// <summary>
    /// Gets the absolute path of the instance's <c>instance.json</c> configuration file.
    /// </summary>
    public required string ConfigPath { get; init; }

    /// <summary>
    /// Gets the absolute path of the directory containing the instance's timestamped backups.
    /// </summary>
    public required string BackupsPath { get; init; }

    /// <summary>
    /// Gets the configuration loaded from the instance's <c>instance.json</c> file.
    /// </summary>
    public required InstanceConfig Config { get; init; }

    #endregion
}
