using InstanceBackupManager.Processing.Configuration;
using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Provides a facade for instance discovery, configuration serialization, validation, and runtime-context creation.
/// </summary>
public sealed class ConfigProcessor
{
    #region Properties

    /// <summary>
    /// Gets the service used to discover immediate instance directories.
    /// </summary>
    private InstanceDiscoveryService InstanceDiscoveryService { get; }

    /// <summary>
    /// Gets the service used to read and write instance configuration files.
    /// </summary>
    private InstanceConfigSerializer InstanceConfigSerializer { get; }

    /// <summary>
    /// Gets the service used to validate instance configurations.
    /// </summary>
    private InstanceConfigValidator InstanceConfigValidator { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a configuration processor using the default configuration services.
    /// </summary>
    public ConfigProcessor()
        : this(
            new InstanceDiscoveryService(),
            new InstanceConfigSerializer(),
            new InstanceConfigValidator()
        )
    {
        
    }

    /// <summary>
    /// Initializes a configuration processor using the specified configuration services.
    /// </summary>
    /// <param name="instanceDiscoveryService">The service used to discover instance directories.</param>
    /// <param name="instanceConfigSerializer">The service used to read and write configuration files.</param>
    /// <param name="instanceConfigValidator">The service used to validate configurations.</param>
    internal ConfigProcessor(
        InstanceDiscoveryService instanceDiscoveryService,
        InstanceConfigSerializer instanceConfigSerializer,
        InstanceConfigValidator instanceConfigValidator
    )
    {
        ArgumentNullException.ThrowIfNull(instanceDiscoveryService);
        ArgumentNullException.ThrowIfNull(instanceConfigSerializer);
        ArgumentNullException.ThrowIfNull(instanceConfigValidator);

        InstanceDiscoveryService = instanceDiscoveryService;
        InstanceConfigSerializer = instanceConfigSerializer;
        InstanceConfigValidator = instanceConfigValidator;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Discovers and loads every configured instance beneath an instances directory.
    /// </summary>
    /// <param name="instancesPath">The directory containing individual instance directories.</param>
    /// <returns>A read-only collection of successfully loaded configured instances.</returns>
    public IReadOnlyCollection<InstanceContext> LoadInstances(string instancesPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancesPath);

        var discoveredInstances = InstanceDiscoveryService.Discover(instancesPath);
        var loadedInstances = new List<InstanceContext>();

        foreach (var instance in discoveredInstances)
        {
            if (!instance.HasConfiguration)
            {
                continue;
            }

            loadedInstances.Add(
                LoadInstance(instance.InstancePath)
            );
        }

        return loadedInstances.AsReadOnly();
    }

    /// <summary>
    /// Loads and validates one instance and creates its runtime context.
    /// </summary>
    /// <param name="instancePath">The directory containing the instance configuration.</param>
    /// <returns>The loaded configuration and normalized runtime paths.</returns>
    public InstanceContext LoadInstance(string instancePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);

        var fullInstancePath = Path.GetFullPath(instancePath);

        var configPath = Path.Combine(
            fullInstancePath,
            BackupStorageConstants.InstanceConfigurationFileName
        );

        var backupsPath = Path.Combine(
            fullInstancePath,
            BackupStorageConstants.BackupsDirectoryName
        );

        var config = LoadConfig(configPath);

        var validationErrors = ValidateConfig(
            config,
            fullInstancePath
        );

        if (validationErrors.Count > 0)
        {
            throw new InvalidDataException(
                $"Invalid configuration '{configPath}':{Environment.NewLine}" +
                string.Join(Environment.NewLine, validationErrors)
            );
        }

        Directory.CreateDirectory(backupsPath);

        return new InstanceContext
        {
            InstancePath = fullInstancePath,
            ConfigPath = configPath,
            BackupsPath = backupsPath,
            Config = config
        };
    }

    /// <summary>
    /// Reads and deserializes an instance configuration file.
    /// </summary>
    /// <param name="configPath">The path of the configuration file.</param>
    /// <returns>The deserialized configuration.</returns>
    public InstanceConfig LoadConfig(string configPath)
    {
        return InstanceConfigSerializer.Load(configPath);
    }

    /// <summary>
    /// Creates a skeleton configuration for an instance without overwriting an existing file.
    /// </summary>
    /// <param name="instancePath">The instance directory in which the configuration will be created.</param>
    public void CreateSkeletonConfig(string instancePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);

        var fullInstancePath = Path.GetFullPath(instancePath);

        var configPath = Path.Combine(
            fullInstancePath,
            BackupStorageConstants.InstanceConfigurationFileName
        );

        var config = CreateSkeletonConfiguration(fullInstancePath);

        InstanceConfigSerializer.Create(
            configPath,
            config
        );
    }

    /// <summary>
    /// Validates an instance configuration and its configured filesystem paths.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <param name="instancePath">The directory containing the instance configuration.</param>
    /// <returns>A read-only collection of validation errors.</returns>
    public IReadOnlyCollection<string> ValidateConfig(
        InstanceConfig config,
        string instancePath
    )
    {
        return InstanceConfigValidator.Validate(
            config,
            instancePath
        );
    }

    /// <summary>
    /// Discovers every immediate instance directory without requiring a configuration file.
    /// </summary>
    /// <param name="instancesPath">The directory containing individual instance directories.</param>
    /// <returns>A read-only collection describing the discovered directories.</returns>
    public IReadOnlyCollection<InstanceDescriptor> DiscoverInstances(string instancesPath)
    {
        return InstanceDiscoveryService.Discover(instancesPath);
    }

    #endregion

    #region Skeleton Configuration

    /// <summary>
    /// Creates the in-memory skeleton configuration written for a new instance.
    /// </summary>
    /// <param name="instancePath">The normalized absolute instance-directory path.</param>
    /// <returns>A skeleton configuration containing an example disabled target.</returns>
    private static InstanceConfig CreateSkeletonConfiguration(string instancePath)
    {
        return new InstanceConfig
        {
            SchemaVersion = BackupStorageConstants.SupportedInstanceConfigurationSchemaVersion,
            Name = Path.GetFileName(instancePath),
            Retention = new RetentionSettings
            {
                ManualBackupsToKeep = null,
                PreRestoreBackupsToKeep = null
            },
            Targets =
            [
                new TargetPath
                {
                    Id = "example-target",
                    Name = "Example Target - Update or Remove",
                    Enabled = false,
                    Required = true,
                    AllowClear = false,
                    Source = "replace-with-source-path",
                    Type = TargetPathType.File,
                    BackupPath = "files/replace-with-file-name"
                }
            ]
        };
    }

    #endregion
}