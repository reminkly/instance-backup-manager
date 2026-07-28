namespace InstanceBackupManager.Processing.Constants;

/// <summary>
/// Defines the file names, directory names, prefixes, and schema versions that make up the application's persistent storage format.
/// </summary>
public static class BackupStorageConstants
{
    #region File Names

    /// <summary>
    /// Gets the file name used for an instance configuration file.
    /// </summary>
    public const string InstanceConfigurationFileName = "instance.json";

    /// <summary>
    /// Gets the file name used for a backup manifest.
    /// </summary>
    public const string ManifestFileName = "manifest.json";

    #endregion

    #region Directory Names

    /// <summary>
    /// Gets the directory name containing the application's instances.
    /// </summary>
    public const string InstancesDirectoryName = "Instances";

    /// <summary>
    /// Gets the directory name containing the backups for an instance.
    /// </summary>
    public const string BackupsDirectoryName = "backups";

    #endregion

    #region Directory Prefixes

    /// <summary>
    /// Gets the prefix used for temporary backup directories while a backup operation is still in progress.
    /// </summary>
    public const string InProgressDirectoryPrefix = ".in-progress-";

    #endregion

    #region Schema Versions

    /// <summary>
    /// Gets the instance configuration schema version supported by this application.
    /// </summary>
    public const int SupportedInstanceConfigurationSchemaVersion = 1;

    /// <summary>
    /// Gets the backup manifest schema version supported by this application.
    /// </summary>
    public const int SupportedManifestSchemaVersion = 1;

    #endregion
}