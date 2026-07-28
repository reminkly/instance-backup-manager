using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Processing.Tests;

/// <summary>
/// Tests individual deletion, bulk deletion, backup preservation, path selection, and deletion result reporting.
/// </summary>
[TestClass]
public sealed class BackupMaintenanceProcessorTests
{
    #region Fields

    private static readonly DateTimeOffset FirstBackupTime = new(2026, 7, 28, 18, 30, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondBackupTime = new(2026, 7, 29, 19, 45, 10, TimeSpan.Zero);
    private static readonly DateTimeOffset DeletionTime = new(2026, 7, 30, 20, 15, 30, TimeSpan.Zero);

    private string _testRootPath = null!;
    private string _instancePath = null!;
    private string _backupsPath = null!;
    private string _sourcePath = null!;
    private BackupMaintenanceProcessor _processor = null!;

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

        _processor = new BackupMaintenanceProcessor(
            new RestoreProcessor(
                new FixedTimeProvider(DeletionTime)
            ),
            new FixedTimeProvider(DeletionTime)
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

    #region Individual Deletion Tests

    /// <summary>
    /// Verifies that deleting one backup removes the selected directory while preserving another completed backup.
    /// </summary>
    [TestMethod]
    public void DeleteBackup_WhenSelectedBackupExists_DeletesOnlySelectedBackup()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var firstManifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        var secondManifest = CreateBackup(
            instance,
            SecondBackupTime
        );

        var result = _processor.DeleteBackup(
            instance,
            firstManifest.BackupName
        );

        Assert.IsFalse(Directory.Exists(Path.Combine(_backupsPath, firstManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, secondManifest.BackupName)));
        Assert.HasCount(1, result.Entries);
        Assert.AreEqual(firstManifest.BackupName, result.Entries.Single().BackupName);
    }

    /// <summary>
    /// Verifies that deleting a missing completed backup throws without deleting an existing backup.
    /// </summary>
    [TestMethod]
    public void DeleteBackup_WhenSelectedBackupDoesNotExist_ThrowsDirectoryNotFoundException()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var existingManifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => _processor.DeleteBackup(
                instance,
                "Missing Backup"
            )
        );

        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, existingManifest.BackupName)));
    }

    /// <summary>
    /// Verifies that a backup name containing parent traversal cannot select an outside directory for deletion.
    /// </summary>
    [TestMethod]
    public void DeleteBackup_WhenBackupNameContainsParentTraversal_DoesNotDeleteOutsideDirectory()
    {
        var outsideDirectoryPath = Path.Combine(_instancePath, "Outside");

        Directory.CreateDirectory(outsideDirectoryPath);
        File.WriteAllText(Path.Combine(outsideDirectoryPath, "keep.dat"), "Keep data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => _processor.DeleteBackup(
                instance,
                Path.Combine("..", "Outside")
            )
        );

        Assert.IsTrue(Directory.Exists(outsideDirectoryPath));
        Assert.IsTrue(File.Exists(Path.Combine(outsideDirectoryPath, "keep.dat")));
    }

    #endregion

    #region Bulk Deletion Tests

    /// <summary>
    /// Verifies that deleting all backups removes every completed backup belonging to the instance.
    /// </summary>
    [TestMethod]
    public void DeleteAllBackups_WhenMultipleBackupsExist_DeletesEveryCompletedBackup()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var firstManifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        var secondManifest = CreateBackup(
            instance,
            SecondBackupTime
        );

        var result = _processor.DeleteAllBackups(instance);

        Assert.IsFalse(Directory.Exists(Path.Combine(_backupsPath, firstManifest.BackupName)));
        Assert.IsFalse(Directory.Exists(Path.Combine(_backupsPath, secondManifest.BackupName)));
        Assert.HasCount(2, result.Entries);
    }

    /// <summary>
    /// Verifies that deleting all backups returns an empty result when no completed backups exist.
    /// </summary>
    [TestMethod]
    public void DeleteAllBackups_WhenNoCompletedBackupsExist_ReturnsEmptyResult()
    {
        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var result = _processor.DeleteAllBackups(instance);

        Assert.AreEqual(DeletionTime, result.CompletedUtc);
        Assert.IsEmpty(result.Entries);
    }

    /// <summary>
    /// Verifies that deleting all completed backups preserves in-progress and unrelated directories.
    /// </summary>
    [TestMethod]
    public void DeleteAllBackups_WhenUnmanagedDirectoriesExist_PreservesUnmanagedDirectories()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var completedManifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        var inProgressPath = Path.Combine(_backupsPath, ".in-progress-test");
        var unrelatedPath = Path.Combine(_backupsPath, "Unrelated Directory");

        Directory.CreateDirectory(inProgressPath);
        Directory.CreateDirectory(unrelatedPath);

        File.WriteAllText(Path.Combine(inProgressPath, "temporary.dat"), "Temporary data");
        File.WriteAllText(Path.Combine(unrelatedPath, "unrelated.dat"), "Unrelated data");

        var result = _processor.DeleteAllBackups(instance);

        Assert.IsFalse(Directory.Exists(Path.Combine(_backupsPath, completedManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(inProgressPath));
        Assert.IsTrue(Directory.Exists(unrelatedPath));
        Assert.HasCount(1, result.Entries);
    }

    /// <summary>
    /// Verifies that a malformed completed-backup manifest prevents bulk deletion before a valid backup is modified.
    /// </summary>
    [TestMethod]
    public void DeleteAllBackups_WhenManifestIsMalformed_DoesNotDeleteValidBackup()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var validManifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        var malformedBackupPath = Path.Combine(_backupsPath, "Malformed Backup");

        Directory.CreateDirectory(malformedBackupPath);
        File.WriteAllText(
            Path.Combine(malformedBackupPath, "manifest.json"),
            "{ invalid JSON }"
        );

        Assert.ThrowsExactly<InvalidDataException>(
            () => _processor.DeleteAllBackups(instance)
        );

        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, validManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(malformedBackupPath));
    }

    #endregion

    #region Batch Deletion Tests

    /// <summary>
    /// Verifies that batch deletion removes only the requested completed backups.
    /// </summary>
    [TestMethod]
    public void DeleteBackups_WhenSubsetIsRequested_DeletesOnlyRequestedBackups()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var firstManifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        var secondManifest = CreateBackup(
            instance,
            SecondBackupTime
        );

        var thirdManifest = CreateBackup(
            instance,
            SecondBackupTime.AddDays(1)
        );

        var result = _processor.DeleteBackups(
            instance,
            [
                firstManifest.BackupName,
                thirdManifest.BackupName
            ]
        );

        Assert.IsFalse(Directory.Exists(Path.Combine(_backupsPath, firstManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, secondManifest.BackupName)));
        Assert.IsFalse(Directory.Exists(Path.Combine(_backupsPath, thirdManifest.BackupName)));
        Assert.HasCount(2, result.Entries);
    }

    /// <summary>
    /// Verifies that a missing requested backup prevents every other requested backup from being deleted.
    /// </summary>
    [TestMethod]
    public void DeleteBackups_WhenOneRequestedBackupIsMissing_DoesNotDeleteExistingBackup()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var existingManifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => _processor.DeleteBackups(
                instance,
                [
                    existingManifest.BackupName,
                    "Missing Backup"
                ]
            )
        );

        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, existingManifest.BackupName)));
    }

    /// <summary>
    /// Verifies that requesting the same backup more than once is rejected before deletion begins.
    /// </summary>
    [TestMethod]
    public void DeleteBackups_WhenBackupNameIsDuplicated_DoesNotDeleteBackup()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var manifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        Assert.ThrowsExactly<ArgumentException>(
            () => _processor.DeleteBackups(
                instance,
                [
                    manifest.BackupName,
                    manifest.BackupName
                ]
            )
        );

        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, manifest.BackupName)));
    }

    #endregion

    #region Result Tests

    /// <summary>
    /// Verifies that deletion reports its completion time and the actual files and bytes removed from the backup directory.
    /// </summary>
    [TestMethod]
    public void DeleteBackup_WhenSuccessful_ReturnsDeletionSummary()
    {
        const string sourceContent = "1234567890";

        File.WriteAllText(
            Path.Combine(_sourcePath, "save.dat"),
            sourceContent
        );

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var manifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        var result = _processor.DeleteBackup(
            instance,
            manifest.BackupName
        );

        var resultEntry = result.Entries.Single();

        Assert.AreEqual(DeletionTime, result.CompletedUtc);
        Assert.AreEqual(manifest.BackupName, resultEntry.BackupName);
        Assert.AreEqual(BackupKind.Manual, resultEntry.Kind);
        Assert.AreEqual(FirstBackupTime, resultEntry.CreatedUtc);
        Assert.IsGreaterThanOrEqualTo(2L, resultEntry.FileCount);
        Assert.IsGreaterThanOrEqualTo(sourceContent.Length, resultEntry.TotalBytes);
    }

    /// <summary>
    /// Verifies that deletion results preserve the kind of a deleted pre-restore backup.
    /// </summary>
    [TestMethod]
    public void DeleteBackup_WhenBackupIsPreRestore_ReturnsPreRestoreKind()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var manifest = CreateBackup(
            instance,
            FirstBackupTime,
            BackupKind.PreRestore
        );

        var result = _processor.DeleteBackup(
            instance,
            manifest.BackupName
        );

        Assert.AreEqual(
            BackupKind.PreRestore,
            result.Entries.Single().Kind
        );
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates an enabled instance context containing the supplied targets.
    /// </summary>
    /// <param name="targets">The targets assigned to the instance.</param>
    /// <returns>A runtime instance context suitable for backup-maintenance tests.</returns>
    private InstanceContext CreateInstanceContext(params TargetPath[] targets)
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
                Targets = [.. targets]
            }
        };
    }

    /// <summary>
    /// Creates a target with the supplied test values.
    /// </summary>
    /// <param name="id">The stable target identifier.</param>
    /// <param name="source">The configured source path.</param>
    /// <param name="backupPath">The relative destination inside a backup.</param>
    /// <param name="type">The source filesystem-entry type.</param>
    /// <returns>A configured target suitable for backup-maintenance tests.</returns>
    private static TargetPath CreateTarget(
        string id,
        string source,
        string backupPath,
        TargetPathType type
    )
    {
        return new TargetPath
        {
            Id = id,
            Name = id,
            Enabled = true,
            Required = true,
            AllowClear = false,
            Source = source,
            Type = type,
            BackupPath = backupPath
        };
    }

    /// <summary>
    /// Creates a completed backup using the specified creation time and backup kind.
    /// </summary>
    /// <param name="instance">The instance to back up.</param>
    /// <param name="backupTime">The UTC creation time assigned to the backup.</param>
    /// <param name="kind">The reason the backup is being created.</param>
    /// <returns>The manifest describing the completed backup.</returns>
    private static BackupManifest CreateBackup(
        InstanceContext instance,
        DateTimeOffset backupTime,
        BackupKind kind = BackupKind.Manual
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
    /// Supplies a fixed UTC time for deterministic backup and deletion timestamps.
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