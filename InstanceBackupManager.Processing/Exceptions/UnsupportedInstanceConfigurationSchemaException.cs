namespace InstanceBackupManager.Processing.Exceptions;

/// <summary>
/// Indicates that an instance configuration must be updated before it can be loaded.
/// </summary>
public sealed class UnsupportedInstanceConfigurationSchemaException : Exception
{
    #region Properties

    public string ConfigPath { get; }

    public int ConfiguredVersion { get; }

    public int SupportedVersion { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes an unsupported-configuration-schema exception.
    /// </summary>
    public UnsupportedInstanceConfigurationSchemaException(
        string configPath,
        int configuredVersion,
        int supportedVersion
    )
        : base(
            $"Configuration '{configPath}' uses schema version '{configuredVersion}', but this application requires version '{supportedVersion}'."
        )
    {
        ConfigPath = configPath;
        ConfiguredVersion = configuredVersion;
        SupportedVersion = supportedVersion;
    }

    #endregion
}
