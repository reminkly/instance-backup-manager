using InstanceBackupManager.Processing.Configuration;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests schema, retention, target, source-path, and backup-path validation.
/// </summary>
[TestClass]
public sealed class InstanceConfigValidatorTests
{
    #region Fields

    private string _testRootPath = null!;
    private string _instancePath = null!;
    private InstanceConfigValidator _validator = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Creates isolated test paths and a validator before each test.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _testRootPath = Path.Combine(
            Path.GetTempPath(),
            "InstanceBackupManagerTests",
            Guid.NewGuid().ToString("N")
        );

        _instancePath = Path.Combine(_testRootPath, "Test Instance");
        Directory.CreateDirectory(_instancePath);
        _validator = new InstanceConfigValidator();
    }

    /// <summary>
    /// Removes the isolated test directory after each test.
    /// </summary>
    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, recursive: true);
        }
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

        var errors = _validator.Validate(config, _instancePath);

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

        var errors = _validator.Validate(config, _instancePath);

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
            SchemaVersion = 1,
            Name = "Test Instance",
            Targets = []
        };

        var errors = _validator.Validate(config, _instancePath);

        AssertContainsError(errors, "unsupported schema version");
    }

    /// <summary>
    /// Verifies that target identifiers are required to be unique without regard to casing.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenTargetIdsDifferOnlyByCase_ReturnsValidationError()
    {
        var firstTarget = CreateValidTarget(id: "saves");

        var secondTarget = CreateValidTarget(id: "SAVES");

        var config = new InstanceConfig
        {
            Name = "Test Instance",
            Targets = [firstTarget, secondTarget]
        };

        var errors = _validator.Validate(config, _instancePath);

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

        var errors = _validator.Validate(config, _instancePath);

        AssertContainsError(errors, "unsupported target type");
    }

    #endregion

    #region Retention Tests

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

        var errors = _validator.Validate(
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

        var errors = _validator.Validate(
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

        var errors = _validator.Validate(
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

        var errors = _validator.Validate(
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

        var errors = _validator.Validate(
            config,
            _instancePath
        );

        AssertContainsError(
            errors,
            "PreRestoreBackupsToKeep must be at least one"
        );
    }

    #endregion

    #region Backup Root Validation Tests

    /// <summary>
    /// Verifies that an absolute backup root is supported for external storage.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenBackupRootIsAbsolute_ReturnsNoBackupRootError()
    {
        var config = CreateValidConfig();
        config = new InstanceConfig
        {
            Name = config.Name,
            BackupRoot = Path.Combine(_testRootPath, "External Backups"),
            Targets = config.Targets
        };

        var errors = _validator.Validate(config, _instancePath);

        Assert.IsFalse(errors.Any(error => error.Contains("backup root", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Verifies that a relative backup root cannot escape the instance directory.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenRelativeBackupRootEscapesInstance_ReturnsValidationError()
    {
        var config = new InstanceConfig
        {
            Name = "Test Instance",
            BackupRoot = Path.Combine("..", "External Backups"),
            Targets = [CreateValidTarget()]
        };

        var errors = _validator.Validate(config, _instancePath);

        AssertContainsError(errors, "relative backup root");
    }

    /// <summary>
    /// Verifies that a filesystem root cannot be used as the backup root.
    /// </summary>
    [TestMethod]
    public void ValidateConfig_WhenBackupRootIsFilesystemRoot_ReturnsValidationError()
    {
        var config = new InstanceConfig
        {
            Name = "Test Instance",
            BackupRoot = Path.GetPathRoot(Path.GetFullPath(_testRootPath))!,
            Targets = [CreateValidTarget()]
        };

        var errors = _validator.Validate(config, _instancePath);

        AssertContainsError(errors, "filesystem root");
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

        var errors = _validator.Validate(config, _instancePath);

        AssertContainsError(errors, "overlaps the configured backup root");
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

        var errors = _validator.Validate(config, _instancePath);

        AssertContainsError(errors, "overlaps the configured backup root");
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

        var errors = _validator.Validate(config, _instancePath);

        AssertContainsError(errors, "overlaps the configured backup root");
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
    /// <param name="source">The source path, or <see langword="null"/> to use a valid default path.</param>    /// <param name="type">The target type.</param>
    /// <returns>A configured target path.</returns>
    private TargetPath CreateValidTarget(
        string id = "saves",
        string? source = null,
        TargetPathType type = TargetPathType.Directory
    )
    {
        return new TargetPath
        {
            Id = id,
            Name = "Save Files",
            Enabled = true,
            AllowClear = true,
            Source = source ?? Path.Combine(_testRootPath, "Source Data"),
            Type = type
        };
    }

    /// <summary>
    /// Verifies that a validation-error collection contains the expected text without regard to casing.
    /// </summary>
    /// <param name="errors">The validation errors to inspect.</param>
    /// <param name="expectedText">The text expected in at least one validation error.</param>
    private static void AssertContainsError(
        IEnumerable<string> errors,
        string expectedText
    )
    {
        var matchingErrorExists = errors.Any(
            error => error.Contains(expectedText, StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(
            matchingErrorExists,
            $"Expected a validation error containing '{expectedText}'. Actual errors: {string.Join(" | ", errors)}");
    }

    #endregion
}