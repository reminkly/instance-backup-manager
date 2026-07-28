using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.BackupMaintenance;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Processing.Tests;

/// <summary>
/// Tests unlimited retention, per-kind limits, chronological preservation, and invalid retention requests.
/// </summary>
[TestClass]
public sealed class BackupRetentionProcessorTests
{
    #region Fields

    private static readonly DateTimeOffset FirstBackupTime = new(2026, 7, 28, 18, 30, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondBackupTime = new(2026, 7, 29, 18, 30, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset ThirdBackupTime = new(2026, 7, 30, 18, 30, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset RetentionTime = new(2026, 7, 31, 18, 30, 5, TimeSpan.Zero);

    private string _testRootPath = null!;
    private string _instancePath = null!;
    private string _backupsPath = null!;
    private string _sourcePath = null!;
    private BackupRetentionProcessor _processor = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Creates isolated instance, backup, and source directories before each test.
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
        _backupsPath = Path.Combine(_instancePath, "backups");
        _sourcePath = Path.Combine(_testRootPath, "Source Data");

        Directory.CreateDirectory(_instancePath);
        Directory.CreateDirectory(_sourcePath);

        var restoreProcessor = new RestoreProcessor(
            new FixedTimeProvider(RetentionTime)
        );

        var maintenanceProcessor = new BackupMaintenanceProcessor(
            restoreProcessor,
            new FixedTimeProvider(RetentionTime)
        );

        _processor = new BackupRetentionProcessor(
            restoreProcessor,
            maintenanceProcessor
        );
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

    #region Unlimited Retention Tests

    /// <summary>
    /// Verifies that a null retention object leaves completed backups untouched.
    /// </summary>
    [TestMethod]
    public void ApplyRetention_WhenRetentionIsNull_DoesNotDeleteBackups()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            retention: null,
            targets:
            [
                CreateTarget()
            ]
        );

        var manifest = CreateBackup(
            instance,
            FirstBackupTime,
            BackupKind.Manual
        );

        var result = _processor.ApplyRetention(
            instance,
            BackupKind.Manual
        );

        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, manifest.BackupName)));
        Assert.IsEmpty(result.Entries);
    }

    /// <summary>
    /// Verifies that a null limit for one backup kind leaves backups of that kind untouched.
    /// </summary>
    [TestMethod]
    public void ApplyRetention_WhenKindLimitIsNull_DoesNotDeleteBackups()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            retention: new RetentionSettings
            {
                ManualBackupsToKeep = null,
                PreRestoreBackupsToKeep = 1
            },
            targets:
            [
                CreateTarget()
            ]
        );

        var firstManifest = CreateBackup(
            instance,
            FirstBackupTime,
            BackupKind.Manual
        );

        var secondManifest = CreateBackup(
            instance,
            SecondBackupTime,
            BackupKind.Manual
        );

        var result = _processor.ApplyRetention(
            instance,
            BackupKind.Manual
        );

        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, firstManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, secondManifest.BackupName)));
        Assert.IsEmpty(result.Entries);
    }

    #endregion

    #region Manual Retention Tests

    /// <summary>
    /// Verifies that manual retention keeps the newest manual backups and deletes older manual backups.
    /// </summary>
    [TestMethod]
    public void ApplyRetention_WhenManualBackupsExceedLimit_DeletesOldestManualBackup()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            retention: new RetentionSettings
            {
                ManualBackupsToKeep = 2,
                PreRestoreBackupsToKeep = null
            },
            targets:
            [
                CreateTarget()
            ]
        );

        var firstManifest = CreateBackup(
            instance,
            FirstBackupTime,
            BackupKind.Manual
        );

        var secondManifest = CreateBackup(
            instance,
            SecondBackupTime,
            BackupKind.Manual
        );

        var thirdManifest = CreateBackup(
            instance,
            ThirdBackupTime,
            BackupKind.Manual
        );

        var result = _processor.ApplyRetention(
            instance,
            BackupKind.Manual
        );

        Assert.IsFalse(Directory.Exists(Path.Combine(_backupsPath, firstManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, secondManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, thirdManifest.BackupName)));
        Assert.HasCount(1, result.Entries);
        Assert.AreEqual(firstManifest.BackupName, result.Entries.Single().BackupName);
        Assert.AreEqual(RetentionTime, result.CompletedUtc);
    }

    /// <summary>
    /// Verifies that applying manual retention does not delete pre-restore backups.
    /// </summary>
    [TestMethod]
    public void ApplyRetention_WhenManualLimitIsApplied_PreservesPreRestoreBackups()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            retention: new RetentionSettings
            {
                ManualBackupsToKeep = 1,
                PreRestoreBackupsToKeep = 1
            },
            targets:
            [
                CreateTarget()
            ]
        );

        var firstManualManifest = CreateBackup(
            instance,
            FirstBackupTime,
            BackupKind.Manual
        );

        var secondManualManifest = CreateBackup(
            instance,
            SecondBackupTime,
            BackupKind.Manual
        );

        var preRestoreManifest = CreateBackup(
            instance,
            ThirdBackupTime,
            BackupKind.PreRestore
        );

        _processor.ApplyRetention(
            instance,
            BackupKind.Manual
        );

        Assert.IsFalse(Directory.Exists(Path.Combine(_backupsPath, firstManualManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, secondManualManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, preRestoreManifest.BackupName)));
    }

    #endregion

    #region Pre-Restore Retention Tests

    /// <summary>
    /// Verifies that pre-restore retention deletes older pre-restore backups while preserving manual backups.
    /// </summary>
    [TestMethod]
    public void ApplyRetention_WhenPreRestoreBackupsExceedLimit_DeletesOldestPreRestoreBackup()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            retention: new RetentionSettings
            {
                ManualBackupsToKeep = 1,
                PreRestoreBackupsToKeep = 1
            },
            targets:
            [
                CreateTarget()
            ]
        );

        var manualManifest = CreateBackup(
            instance,
            FirstBackupTime,
            BackupKind.Manual
        );

        var firstPreRestoreManifest = CreateBackup(
            instance,
            SecondBackupTime,
            BackupKind.PreRestore
        );

        var secondPreRestoreManifest = CreateBackup(
            instance,
            ThirdBackupTime,
            BackupKind.PreRestore
        );

        var result = _processor.ApplyRetention(
            instance,
            BackupKind.PreRestore
        );

        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, manualManifest.BackupName)));
        Assert.IsFalse(Directory.Exists(Path.Combine(_backupsPath, firstPreRestoreManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, secondPreRestoreManifest.BackupName)));
        Assert.HasCount(1, result.Entries);
        Assert.AreEqual(firstPreRestoreManifest.BackupName, result.Entries.Single().BackupName);
    }

    #endregion

    #region Limit Boundary Tests

    /// <summary>
    /// Verifies that no backups are deleted when the number of backups does not exceed the configured limit.
    /// </summary>
    [TestMethod]
    public void ApplyRetention_WhenBackupCountDoesNotExceedLimit_DoesNotDeleteBackups()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            retention: new RetentionSettings
            {
                ManualBackupsToKeep = 2,
                PreRestoreBackupsToKeep = null
            },
            targets:
            [
                CreateTarget()
            ]
        );

        var firstManifest = CreateBackup(
            instance,
            FirstBackupTime,
            BackupKind.Manual
        );

        var secondManifest = CreateBackup(
            instance,
            SecondBackupTime,
            BackupKind.Manual
        );

        var result = _processor.ApplyRetention(
            instance,
            BackupKind.Manual
        );

        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, firstManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, secondManifest.BackupName)));
        Assert.IsEmpty(result.Entries);
    }

    /// <summary>
    /// Verifies that a nonpositive configured retention limit is rejected defensively.
    /// </summary>
    [TestMethod]
    public void ApplyRetention_WhenConfiguredLimitIsZero_ThrowsInvalidOperationException()
    {
        var instance = CreateInstanceContext(
            retention: new RetentionSettings
            {
                ManualBackupsToKeep = 0,
                PreRestoreBackupsToKeep = null
            },
            targets:
            [
                CreateTarget()
            ]
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.ApplyRetention(
                instance,
                BackupKind.Manual
            )
        );
    }

    /// <summary>
    /// Verifies that an unsupported backup kind is rejected.
    /// </summary>
    [TestMethod]
    public void ApplyRetention_WhenBackupKindIsUnsupported_ThrowsArgumentOutOfRangeException()
    {
        var instance = CreateInstanceContext(
            retention: new RetentionSettings
            {
                ManualBackupsToKeep = 1,
                PreRestoreBackupsToKeep = 1
            },
            targets:
            [
                CreateTarget()
            ]
        );

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _processor.ApplyRetention(
                instance,
                (BackupKind)999
            )
        );
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates an instance context with the specified retention settings and targets.
    /// </summary>
    /// <param name="retention">The optional per-kind retention settings.</param>
    /// <param name="targets">The targets assigned to the instance.</param>
    /// <returns>A runtime instance context suitable for retention tests.</returns>
    private InstanceContext CreateInstanceContext(
        RetentionSettings? retention,
        params TargetPath[] targets
    )
    {
        return new InstanceContext
        {
            InstancePath = _instancePath,
            ConfigPath = Path.Combine(_instancePath, "instance.json"),
            BackupsPath = _backupsPath,
            Config = new InstanceConfig
            {
                Name = "Test Instance",
                Enabled = true,
                Retention = retention,
                Targets = [.. targets]
            }
        };
    }

    /// <summary>
    /// Creates a valid directory target for retention tests.
    /// </summary>
    /// <returns>A configured target using the test source directory.</returns>
    private TargetPath CreateTarget()
    {
        return new TargetPath
        {
            Id = "data",
            Name = "Data",
            Enabled = true,
            Required = true,
            AllowClear = false,
            Source = _sourcePath,
            Type = TargetPathType.Directory,
            BackupPath = "data"
        };
    }

    /// <summary>
    /// Creates a completed backup with the specified timestamp and backup kind.
    /// </summary>
    /// <param name="instance">The instance being backed up.</param>
    /// <param name="backupTime">The UTC creation time assigned to the backup.</param>
    /// <param name="kind">The reason the backup is being created.</param>
    /// <returns>The manifest describing the completed backup.</returns>
    private static BackupManifest CreateBackup(
        InstanceContext instance,
        DateTimeOffset backupTime,
        BackupKind kind
    )
    {
        var backupProcessor = new BackupProcessor(
            new FixedTimeProvider(backupTime)
        );

        return backupProcessor.CreateBackup(
            instance,
            kind
        );
    }

    #endregion

    #region Test Types

    /// <summary>
    /// Supplies a fixed UTC time for deterministic backup and retention timestamps.
    /// </summary>
    /// <param name="utcNow">The UTC date and time returned by the provider.</param>
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        #region Public Methods

        /// <summary>
        /// Gets the fixed UTC date and time supplied during construction.
        /// </summary>
        /// <returns>The fixed UTC date and time.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }

        #endregion
    }

    #endregion
}