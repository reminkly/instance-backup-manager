namespace InstanceBackupManager.Processing.Models.Configuration;

/// <summary>
/// Describes a successfully upgraded instance configuration and the preserved original file.
/// </summary>
public sealed class ConfigurationUpgradeResult
{
    #region Properties

    /// <summary>
    /// Gets the path of the upgraded active configuration.
    /// </summary>
    public required string ConfigPath { get; init; }

    /// <summary>
    /// Gets the path containing the unchanged configuration that existed before the upgrade.
    /// </summary>
    public required string BackupPath { get; init; }

    /// <summary>
    /// Gets the schema version that existed before the upgrade.
    /// </summary>
    public required int PreviousVersion { get; init; }

    /// <summary>
    /// Gets the schema version produced by the upgrade.
    /// </summary>
    public required int CurrentVersion { get; init; }

    #endregion
}
