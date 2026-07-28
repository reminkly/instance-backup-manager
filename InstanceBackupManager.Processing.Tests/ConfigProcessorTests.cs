using System.Text.Json;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;

namespace InstanceBackupManager.Processing.Tests;

/// <summary>
/// Tests instance discovery, configuration creation, configuration loading, and configuration validation.
/// </summary>
[TestClass]
public sealed class ConfigProcessorTests
{
    #region Fields

    private string _testRootPath = null!;
    private string _instancePath = null!;
    private ConfigProcessor _processor = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Creates an isolated temporary directory and a new configuration processor before each test.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _testRootPath = Path.Combine(
            Path.GetTempPath(),
            "InstanceBackupManagerTests",
            Guid.NewGuid().ToString("N"));

        _instancePath = Path.Combine(_testRootPath, "Test Instance");
        _processor = new ConfigProcessor();

        Directory.CreateDirectory(_testRootPath);
    }

    /// <summary>
    /// Removes the isolated temporary directory after each test.
    /// </summary>
    [TestCleanup]
    public void TestCleanup()
    {
        // Every test receives a unique directory beneath the application-specific test directory.
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, recursive: true);
        }
    }

    #endregion

    #region LoadInstances Tests

    /// <summary>
    /// Verifies that loading a missing instances directory creates it and returns an empty collection.
    /// </summary>
    [TestMethod]
    public void LoadInstances_WhenInstancesDirectoryDoesNotExist_CreatesDirectoryAndReturnsEmptyCollection()
    {
        var instancesPath = Path.Combine(_testRootPath, "Instances");

        var instances = _processor.LoadInstances(instancesPath);

        Assert.IsTrue(Directory.Exists(instancesPath));
        Assert.IsEmpty(instances);
    }

    /// <summary>
    /// Verifies that instance directories without configuration files are not returned as loaded instances.
    /// </summary>
    [TestMethod]
    public void LoadInstances_WhenInstanceHasNoConfiguration_SkipsInstance()
    {
        var instancesPath = Path.Combine(_testRootPath, "Instances");
        var unconfiguredInstancePath = Path.Combine(instancesPath, "Unconfigured Instance");

        Directory.CreateDirectory(unconfiguredInstancePath);

        var instances = _processor.LoadInstances(instancesPath);

        Assert.IsEmpty(instances);
    }

    /// <summary>
    /// Verifies that configured instance directories are loaded and returned.
    /// </summary>
    [TestMethod]
    public void LoadInstances_WhenInstanceIsConfigured_ReturnsInstance()
    {
        var instancesPath = Path.Combine(_testRootPath, "Instances");
        var configuredInstancePath = Path.Combine(instancesPath, "Configured Instance");

        _processor.CreateSkeletonConfig(configuredInstancePath);

        var instances = _processor.LoadInstances(instancesPath);

        Assert.HasCount(1, instances);

        var instance = instances.Single();

        Assert.AreEqual("Configured Instance", instance.Config.Name);
        Assert.AreEqual(Path.GetFullPath(configuredInstancePath), instance.InstancePath);
    }

    #endregion

    #region LoadInstance Tests

    /// <summary>
    /// Verifies that loading an instance creates its backups directory when it does not already exist.
    /// </summary>
    [TestMethod]
    public void LoadInstance_WhenBackupsDirectoryDoesNotExist_CreatesBackupsDirectory()
    {
        _processor.CreateSkeletonConfig(_instancePath);

        var instance = _processor.LoadInstance(_instancePath);

        Assert.IsTrue(Directory.Exists(instance.BackupsPath));
        Assert.AreEqual(Path.Combine(Path.GetFullPath(_instancePath), "backups"), instance.BackupsPath);
    }

    /// <summary>
    /// Verifies that loading an instance returns the expected runtime paths.
    /// </summary>
    [TestMethod]
    public void LoadInstance_WhenConfigurationIsValid_ReturnsExpectedContext()
    {
        _processor.CreateSkeletonConfig(_instancePath);

        var instance = _processor.LoadInstance(_instancePath);

        Assert.AreEqual(Path.GetFullPath(_instancePath), instance.InstancePath);
        Assert.AreEqual(Path.Combine(Path.GetFullPath(_instancePath), "instance.json"), instance.ConfigPath);
        Assert.AreEqual(Path.Combine(Path.GetFullPath(_instancePath), "backups"), instance.BackupsPath);
        Assert.AreEqual("Test Instance", instance.Config.Name);
    }

    /// <summary>
    /// Verifies that loading an instance without a configuration file throws a file-not-found exception.
    /// </summary>
    [TestMethod]
    public void LoadInstance_WhenConfigurationDoesNotExist_ThrowsFileNotFoundException()
    {
        Assert.ThrowsExactly<FileNotFoundException>(() => _processor.LoadInstance(_instancePath));
    }

    #endregion

    #region CreateSkeletonConfig Tests

    /// <summary>
    /// Verifies that skeleton creation produces an instance directory and configuration file.
    /// </summary>
    [TestMethod]
    public void CreateSkeletonConfig_WhenInstanceDoesNotExist_CreatesInstanceAndConfiguration()
    {
        _processor.CreateSkeletonConfig(_instancePath);

        var configPath = Path.Combine(_instancePath, "instance.json");

        Assert.IsTrue(Directory.Exists(_instancePath));
        Assert.IsTrue(File.Exists(configPath));
    }

    /// <summary>
    /// Verifies that a skeleton configuration uses the instance directory name and contains a disabled example target.
    /// </summary>
    [TestMethod]
    public void CreateSkeletonConfig_WhenCreated_UsesDirectoryNameAndCreatesExampleTarget()
    {
        _processor.CreateSkeletonConfig(_instancePath);

        var configPath = Path.Combine(_instancePath, "instance.json");
        var config = _processor.LoadConfig(configPath);

        Assert.AreEqual("Test Instance", config.Name);
        Assert.AreEqual(1, config.SchemaVersion);
        Assert.IsTrue(config.Enabled);
        Assert.IsNotNull(config.Retention);
        Assert.IsNull(config.Retention.ManualBackupsToKeep);
        Assert.IsNull(config.Retention.PreRestoreBackupsToKeep);
        Assert.HasCount(1, config.Targets);

        var target = config.Targets.Single();

        Assert.AreEqual("example-target", target.Id);
        Assert.AreEqual("Example Target - Update or Remove", target.Name);
        Assert.IsFalse(target.Enabled);
        Assert.IsTrue(target.Required);
        Assert.IsFalse(target.AllowClear);
        Assert.AreEqual("replace-with-source-path", target.Source);
        Assert.AreEqual(TargetPathType.File, target.Type);
        Assert.AreEqual("files/replace-with-file-name", target.BackupPath);
    }

    /// <summary>
    /// Verifies that skeleton creation does not overwrite an existing configuration file.
    /// </summary>
    [TestMethod]
    public void CreateSkeletonConfig_WhenConfigurationAlreadyExists_ThrowsIOException()
    {
        _processor.CreateSkeletonConfig(_instancePath);

        Assert.ThrowsExactly<IOException>(() => _processor.CreateSkeletonConfig(_instancePath));
    }

    #endregion

    #region LoadConfig Tests

    /// <summary>
    /// Verifies that loading a missing configuration file throws a file-not-found exception.
    /// </summary>
    [TestMethod]
    public void LoadConfig_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        var configPath = Path.Combine(_instancePath, "instance.json");

        Assert.ThrowsExactly<FileNotFoundException>(() => _processor.LoadConfig(configPath));
    }

    /// <summary>
    /// Verifies that loading malformed JSON throws a JSON exception.
    /// </summary>
    [TestMethod]
    public void LoadConfig_WhenJsonIsMalformed_ThrowsJsonException()
    {
        Directory.CreateDirectory(_instancePath);

        var configPath = Path.Combine(_instancePath, "instance.json");

        File.WriteAllText(configPath, "{ this is not valid JSON }");

        Assert.ThrowsExactly<JsonException>(() => _processor.LoadConfig(configPath));
    }

    /// <summary>
    /// Verifies that JSON comments and trailing commas are accepted in manually edited configurations.
    /// </summary>
    [TestMethod]
    public void LoadConfig_WhenJsonContainsCommentsAndTrailingCommas_LoadsConfiguration()
    {
        Directory.CreateDirectory(_instancePath);

        var configPath = Path.Combine(_instancePath, "instance.json");

        var json =
            """
            {
              // This comment is intentionally permitted.
              "schemaVersion": 1,
              "name": "Commented Instance",
              "enabled": true,
              "targets": [],
            }
            """;

        File.WriteAllText(configPath, json);

        var config = _processor.LoadConfig(configPath);

        Assert.AreEqual("Commented Instance", config.Name);
        Assert.IsEmpty(config.Targets);
    }

    #endregion

    #region General Validation Tests

    /// <summary>
    /// Verifies that a valid configuration returns no validation errors.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenConfigurationIsValid_ReturnsNoErrors()
    {
        var config = CreateValidConfig();

        var errors = _processor.ValidateConfig(config, _instancePath);

        Assert.IsEmpty(errors);
    }

    /// <summary>
    /// Verifies that an empty instance name produces a validation error.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenNameIsEmpty_ReturnsValidationError()
    {
        var config = new InstanceConfig
        {
            Name = string.Empty,
            Targets = []
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "instance name is required");
    }

    /// <summary>
    /// Verifies that an unsupported schema version produces a validation error.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenSchemaVersionIsUnsupported_ReturnsValidationError()
    {
        var config = new InstanceConfig
        {
            SchemaVersion = 2,
            Name = "Test Instance",
            Targets = []
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "unsupported schema version");
    }

    /// <summary>
    /// Verifies that target identifiers are required to be unique without regard to casing.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenTargetIdsDifferOnlyByCase_ReturnsValidationError()
    {
        var firstTarget = CreateValidTarget(
            id: "saves",
            backupPath: "saves");

        var secondTarget = CreateValidTarget(
            id: "SAVES",
            backupPath: "other-saves");

        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Targets = [firstTarget, secondTarget]
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "target ID 'saves' is duplicated");
    }

    /// <summary>
    /// Verifies that an unknown target type produces a validation error.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenTargetTypeIsUnknown_ReturnsValidationError()
    {
        var target = CreateValidTarget(type: TargetPathType.Unknown);

        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Targets = [target]
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "unsupported target type");
    }

    #endregion

    #region Retention Tests

    /// <summary>
    /// Verifies that a legacy configuration without retention settings loads with unlimited retention.
    /// </summary>
    [TestMethod]
    public void LoadConfig_WhenRetentionIsOmitted_UsesUnlimitedRetention()
    {
        var configPath = Path.Combine(_instancePath, "instance.json");

        Directory.CreateDirectory(_instancePath);

        File.WriteAllText(
            configPath,
            """
            {
            "SchemaVersion": 1,
            "Name": "Legacy Instance",
            "Enabled": true,
            "Targets": []
            }
            """
        );

        var config = _processor.LoadConfig(configPath);

        Assert.IsNotNull(config.Retention);
        Assert.IsNull(config.Retention.ManualBackupsToKeep);
        Assert.IsNull(config.Retention.PreRestoreBackupsToKeep);
    }

    /// <summary>
    /// Verifies that a null retention object is accepted and represents unlimited retention.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenRetentionIsNull_ReturnsNoRetentionErrors()
    {
        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Retention = null,
            Targets =
            [
                CreateValidTarget()
            ]
        };

        var errors = _processor.ValidateConfig(
            config,
            _instancePath
        );

        Assert.IsEmpty(errors);
    }

    /// <summary>
    /// Verifies that null per-kind limits are accepted and represent unlimited retention.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenRetentionLimitsAreNull_ReturnsNoErrors()
    {
        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Retention = new RetentionSettings
            {
                ManualBackupsToKeep = null,
                PreRestoreBackupsToKeep = null
            },
            Targets =
            [
                CreateValidTarget()
            ]
        };

        var errors = _processor.ValidateConfig(
            config,
            _instancePath
        );

        Assert.IsEmpty(errors);
    }

    /// <summary>
    /// Verifies that positive manual and pre-restore retention limits are accepted.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenRetentionLimitsArePositive_ReturnsNoErrors()
    {
        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Retention = new RetentionSettings
            {
                ManualBackupsToKeep = 10,
                PreRestoreBackupsToKeep = 5
            },
            Targets =
            [
                CreateValidTarget()
            ]
        };

        var errors = _processor.ValidateConfig(
            config,
            _instancePath
        );

        Assert.IsEmpty(errors);
    }

    /// <summary>
    /// Verifies that a manual-backup retention limit of zero produces a validation error.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenManualRetentionLimitIsZero_ReturnsValidationError()
    {
        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Retention = new RetentionSettings
            {
                ManualBackupsToKeep = 0,
                PreRestoreBackupsToKeep = null
            },
            Targets =
            [
                CreateValidTarget()
            ]
        };

        var errors = _processor.ValidateConfig(
            config,
            _instancePath
        );

        AssertContainsError(
            errors,
            "ManualBackupsToKeep must be at least one"
        );
    }

    /// <summary>
    /// Verifies that a negative pre-restore retention limit produces a validation error.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenPreRestoreRetentionLimitIsNegative_ReturnsValidationError()
    {
        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Retention = new RetentionSettings
            {
                ManualBackupsToKeep = null,
                PreRestoreBackupsToKeep = -1
            },
            Targets =
            [
                CreateValidTarget()
            ]
        };

        var errors = _processor.ValidateConfig(
            config,
            _instancePath
        );

        AssertContainsError(
            errors,
            "PreRestoreBackupsToKeep must be at least one"
        );
    }

    #endregion

    #region Backup Path Validation Tests

    /// <summary>
    /// Verifies that an absolute backup path produces a validation error.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenBackupPathIsRooted_ReturnsValidationError()
    {
        var rootedBackupPath = Path.Combine(
            Path.GetPathRoot(Path.GetFullPath(_testRootPath))!,
            "UnsafeBackup");

        var target = CreateValidTarget(backupPath: rootedBackupPath);

        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Targets = [target]
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "rooted backup path");
    }

    /// <summary>
    /// Verifies that a backup path containing parent traversal produces a validation error.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenBackupPathEscapesBackupDirectory_ReturnsValidationError()
    {
        var target = CreateValidTarget(
            backupPath: Path.Combine("..", "escaped"));

        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Targets = [target]
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "unsafe backup path");
    }

    /// <summary>
    /// Verifies that two targets cannot use the same backup destination.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenBackupPathsAreEqual_ReturnsValidationError()
    {
        var firstTarget = CreateValidTarget(
            id: "saves",
            backupPath: "shared");

        var secondTarget = CreateValidTarget(
            id: "mods",
            backupPath: "shared");

        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Targets = [firstTarget, secondTarget]
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "overlapping backup paths");
    }

    /// <summary>
    /// Verifies that one target cannot store its backup beneath another target's backup destination.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenBackupPathIsNestedInsideAnotherBackupPath_ReturnsValidationError()
    {
        var firstTarget = CreateValidTarget(
            id: "data",
            backupPath: "data");

        var secondTarget = CreateValidTarget(
            id: "mods",
            backupPath: Path.Combine("data", "mods"));

        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Targets = [firstTarget, secondTarget]
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "overlapping backup paths");
    }

    #endregion

    #region Source Path Validation Tests

    /// <summary>
    /// Verifies that using the backups directory as a source produces a validation error.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenSourceEqualsBackupsDirectory_ReturnsValidationError()
    {
        var backupsPath = Path.Combine(_instancePath, "backups");
        var target = CreateValidTarget(source: backupsPath);

        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Targets = [target]
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "overlaps the instance backups directory");
    }

    /// <summary>
    /// Verifies that using a directory containing the backups directory as a source produces a validation error.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenSourceContainsBackupsDirectory_ReturnsValidationError()
    {
        var target = CreateValidTarget(source: _instancePath);

        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Targets = [target]
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "overlaps the instance backups directory");
    }

    /// <summary>
    /// Verifies that using a directory beneath the backups directory as a source produces a validation error.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenSourceIsInsideBackupsDirectory_ReturnsValidationError()
    {
        var sourcePath = Path.Combine(_instancePath, "backups", "Existing Backup");
        var target = CreateValidTarget(source: sourcePath);

        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Targets = [target]
        };

        var errors = _processor.ValidateConfig(config, _instancePath);

        AssertContainsError(errors, "overlaps the instance backups directory");
    }

    /// <summary>
    /// Verifies that discovering a missing instances directory creates it and returns an empty collection.
    /// </summary>
    [TestMethod]
    public void DiscoverInstances_WhenInstancesDirectoryDoesNotExist_CreatesDirectoryAndReturnsEmptyCollection()
    {
        var instancesPath = Path.Combine(_testRootPath, "Instances");

        var instances = _processor.DiscoverInstances(instancesPath);

        Assert.IsTrue(Directory.Exists(instancesPath));
        Assert.IsEmpty(instances);
    }

    /// <summary>
    /// Verifies that discovery identifies which instance directories contain configuration files.
    /// </summary>
    [TestMethod]
    public void DiscoverInstances_WhenConfiguredAndUnconfiguredInstancesExist_ReturnsConfigurationStatus()
    {
        var instancesPath = Path.Combine(_testRootPath, "Instances");
        var configuredInstancePath = Path.Combine(instancesPath, "Configured Instance");
        var unconfiguredInstancePath = Path.Combine(instancesPath, "Unconfigured Instance");

        _processor.CreateSkeletonConfig(configuredInstancePath);
        Directory.CreateDirectory(unconfiguredInstancePath);

        var instances = _processor.DiscoverInstances(instancesPath);

        Assert.HasCount(2, instances);

        var configuredInstance = instances.Single(
            instance => instance.Name == "Configured Instance"
        );

        var unconfiguredInstance = instances.Single(
            instance => instance.Name == "Unconfigured Instance"
        );

        Assert.IsTrue(configuredInstance.HasConfiguration);
        Assert.IsFalse(unconfiguredInstance.HasConfiguration);
        Assert.AreEqual(Path.GetFullPath(configuredInstancePath), configuredInstance.InstancePath);
        Assert.AreEqual(Path.GetFullPath(unconfiguredInstancePath), unconfiguredInstance.InstancePath);
    }

    /// <summary>
    /// Verifies that discovered instances are returned alphabetically without regard to casing.
    /// </summary>
    [TestMethod]
    public void DiscoverInstances_WhenMultipleInstancesExist_ReturnsInstancesInAlphabeticalOrder()
    {
        var instancesPath = Path.Combine(_testRootPath, "Instances");

        Directory.CreateDirectory(Path.Combine(instancesPath, "Zulu"));
        Directory.CreateDirectory(Path.Combine(instancesPath, "alpha"));
        Directory.CreateDirectory(Path.Combine(instancesPath, "Bravo"));

        var instances = _processor.DiscoverInstances(instancesPath);
        var instanceNames = instances
            .Select(instance => instance.Name)
            .ToList();

        var expectedNames = new List<string>
        {
            "alpha",
            "Bravo",
            "Zulu"
        };

        CollectionAssert.AreEqual(expectedNames, instanceNames);
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a valid instance configuration for tests that do not require a specific invalid value.
    /// </summary>
    /// <returns>A valid instance configuration.</returns>
    private InstanceConfig CreateValidConfig()
    {
        return new InstanceConfig
        {
            Name = "Test Instance",
            Targets =
            [
                CreateValidTarget()
            ]
        };
    }

    /// <summary>
    /// Creates a valid target while allowing individual values to be overridden by a test.
    /// </summary>
    /// <param name="id">The target identifier.</param>
    /// <param name="source">The source path, or <see langword="null"/> to use a valid default path.</param>
    /// <param name="backupPath">The relative backup path.</param>
    /// <param name="type">The target type.</param>
    /// <returns>A configured target path.</returns>
    private TargetPath CreateValidTarget(
        string id = "saves",
        string? source = null,
        string backupPath = "saves",
        TargetPathType type = TargetPathType.Directory)
    {
        return new TargetPath
        {
            Id = id,
            Name = "Save Files",
            Enabled = true,
            AllowClear = true,
            Source = source ?? Path.Combine(_testRootPath, "Source Data"),
            Type = type,
            BackupPath = backupPath
        };
    }

    /// <summary>
    /// Verifies that a validation-error collection contains the expected text without regard to casing.
    /// </summary>
    /// <param name="errors">The validation errors to inspect.</param>
    /// <param name="expectedText">The text expected in at least one validation error.</param>
    private static void AssertContainsError(
        IEnumerable<string> errors,
        string expectedText)
    {
        var matchingErrorExists = errors.Any(
            error => error.Contains(expectedText, StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(
            matchingErrorExists,
            $"Expected a validation error containing '{expectedText}'. Actual errors: {string.Join(" | ", errors)}");
    }

    #endregion
}