using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests backup discovery, manifest validation, current-configuration matching, and restore behavior.
/// </summary>
[TestClass]
public sealed class RestoreProcessorTests
{
    #region Fields

    private static readonly DateTimeOffset FirstBackupTime = new(2026, 7, 28, 18, 30, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondBackupTime = new(2026, 7, 29, 19, 45, 10, TimeSpan.Zero);
    private static readonly DateTimeOffset RestoreTime = new(2026, 7, 30, 20, 15, 30, TimeSpan.Zero);

    private string _testRootPath = null!;
    private string _instancePath = null!;
    private string _backupsPath = null!;
    private string _sourcePath = null!;
    private RestoreProcessor _processor = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Creates isolated instance and source directories before each test.
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

        _processor = new RestoreProcessor(
            new FixedTimeProvider(RestoreTime)
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

    #region Directory Restore Tests

    /// <summary>
    /// Verifies that restoring a directory overwrites files contained in the backup.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenDirectoryFileHasChanged_OverwritesFile()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Original save data");

        var target = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var instance = CreateInstanceContext(target);
        var manifest = CreateBackup(instance, FirstBackupTime);

        File.WriteAllText(sourceFilePath, "Changed save data");

        var result = _processor.RestoreBackup(
            instance,
            manifest.BackupName
        );

        Assert.AreEqual("Original save data", File.ReadAllText(sourceFilePath));
        Assert.HasCount(1, result.Entries);
        Assert.AreEqual(1L, result.Entries.Single().FileCount);
    }

    /// <summary>
    /// Verifies that restoring a directory preserves unrelated files that are not present in the backup.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenDestinationContainsUnrelatedFile_PreservesUnrelatedFile()
    {
        var backedUpFilePath = Path.Combine(_sourcePath, "save.dat");
        var unrelatedFilePath = Path.Combine(_sourcePath, "new-file.dat");

        File.WriteAllText(backedUpFilePath, "Original save data");

        var target = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var instance = CreateInstanceContext(target);
        var manifest = CreateBackup(instance, FirstBackupTime);

        File.WriteAllText(backedUpFilePath, "Changed save data");
        File.WriteAllText(unrelatedFilePath, "Unrelated data");

        _processor.RestoreBackup(
            instance,
            manifest.BackupName
        );

        Assert.AreEqual("Original save data", File.ReadAllText(backedUpFilePath));
        Assert.AreEqual("Unrelated data", File.ReadAllText(unrelatedFilePath));
    }

    /// <summary>
    /// Verifies that restoring a directory recreates files and directories that no longer exist at the destination.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenDestinationContentsWereDeleted_RecreatesContents()
    {
        var nestedSourcePath = Path.Combine(_sourcePath, "Nested");
        var nestedFilePath = Path.Combine(nestedSourcePath, "mod.txt");

        Directory.CreateDirectory(nestedSourcePath);
        File.WriteAllText(nestedFilePath, "Mod data");

        var target = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var instance = CreateInstanceContext(target);
        var manifest = CreateBackup(instance, FirstBackupTime);

        Directory.Delete(nestedSourcePath, recursive: true);

        _processor.RestoreBackup(
            instance,
            manifest.BackupName
        );

        Assert.IsTrue(File.Exists(nestedFilePath));
        Assert.AreEqual("Mod data", File.ReadAllText(nestedFilePath));
    }

    #endregion

    #region File Restore Tests

    /// <summary>
    /// Verifies that restoring a single-file target overwrites the current file.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenFileTargetHasChanged_OverwritesFile()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "settings.ini");

        File.WriteAllText(sourceFilePath, "fullscreen=true");

        var target = CreateTarget(
            id: "settings",
            source: sourceFilePath,
            backupPath: Path.Combine("settings", "settings.ini"),
            type: TargetPathType.File
        );

        var instance = CreateInstanceContext(target);
        var manifest = CreateBackup(instance, FirstBackupTime);

        File.WriteAllText(sourceFilePath, "fullscreen=false");

        var result = _processor.RestoreBackup(
            instance,
            manifest.BackupName
        );

        Assert.AreEqual("fullscreen=true", File.ReadAllText(sourceFilePath));
        Assert.HasCount(1, result.Entries);
        Assert.AreEqual(1L, result.Entries.Single().FileCount);
    }

    /// <summary>
    /// Verifies that restoring a single-file target recreates a missing destination file.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenFileTargetWasDeleted_RecreatesFile()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "settings.ini");

        File.WriteAllText(sourceFilePath, "fullscreen=true");

        var target = CreateTarget(
            id: "settings",
            source: sourceFilePath,
            backupPath: Path.Combine("settings", "settings.ini"),
            type: TargetPathType.File
        );

        var instance = CreateInstanceContext(target);
        var manifest = CreateBackup(instance, FirstBackupTime);

        File.Delete(sourceFilePath);

        _processor.RestoreBackup(
            instance,
            manifest.BackupName
        );

        Assert.IsTrue(File.Exists(sourceFilePath));
        Assert.AreEqual("fullscreen=true", File.ReadAllText(sourceFilePath));
    }

    #endregion

    #region Current Configuration Tests

    /// <summary>
    /// Verifies that restoration uses the current configured source path instead of the historical manifest source.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenCurrentSourceHasChanged_RestoresToCurrentSource()
    {
        var originalSourceFilePath = Path.Combine(_sourcePath, "save.dat");
        var currentSourcePath = Path.Combine(_testRootPath, "Current Source");
        var currentSourceFilePath = Path.Combine(currentSourcePath, "save.dat");

        File.WriteAllText(originalSourceFilePath, "Original save data");
        Directory.CreateDirectory(currentSourcePath);
        File.WriteAllText(currentSourceFilePath, "Current destination data");

        var originalTarget = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var originalInstance = CreateInstanceContext(originalTarget);
        var manifest = CreateBackup(originalInstance, FirstBackupTime);

        var currentTarget = CreateTarget(
            id: "data",
            source: currentSourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var currentInstance = CreateInstanceContext(currentTarget);

        var result = _processor.RestoreBackup(
            currentInstance,
            manifest.BackupName
        );

        Assert.AreEqual("Original save data", File.ReadAllText(currentSourceFilePath));
        Assert.AreEqual("Original save data", File.ReadAllText(originalSourceFilePath));
        Assert.AreEqual(Path.GetFullPath(currentSourcePath), result.Entries.Single().DestinationPath);
    }

    /// <summary>
    /// Verifies that a currently disabled target is not restored.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenCurrentTargetIsDisabled_DoesNotRestoreTarget()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Original save data");

        var originalTarget = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var originalInstance = CreateInstanceContext(originalTarget);
        var manifest = CreateBackup(originalInstance, FirstBackupTime);

        File.WriteAllText(sourceFilePath, "Changed save data");

        var disabledTarget = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory,
            enabled: false
        );

        var currentInstance = CreateInstanceContext(disabledTarget);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.RestoreBackup(
                currentInstance,
                manifest.BackupName
            )
        );

        Assert.AreEqual("Changed save data", File.ReadAllText(sourceFilePath));
    }

    /// <summary>
    /// Verifies that restoration fails when a manifest target no longer exists in the current configuration.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenTargetDoesNotExistInCurrentConfiguration_ThrowsInvalidDataException()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var originalTarget = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var originalInstance = CreateInstanceContext(originalTarget);
        var manifest = CreateBackup(originalInstance, FirstBackupTime);
        var currentInstance = CreateInstanceContext();

        Assert.ThrowsExactly<InvalidDataException>(
            () => _processor.RestoreBackup(
                currentInstance,
                manifest.BackupName
            )
        );
    }

    /// <summary>
    /// Verifies that restoration fails when the current target type differs from the manifest target type.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenCurrentTargetTypeHasChanged_ThrowsInvalidDataException()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var originalTarget = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var originalInstance = CreateInstanceContext(originalTarget);
        var manifest = CreateBackup(originalInstance, FirstBackupTime);

        var currentTarget = CreateTarget(
            id: "data",
            source: Path.Combine(_testRootPath, "restored.dat"),
            backupPath: "data",
            type: TargetPathType.File
        );

        var currentInstance = CreateInstanceContext(currentTarget);

        Assert.ThrowsExactly<InvalidDataException>(
            () => _processor.RestoreBackup(
                currentInstance,
                manifest.BackupName
            )
        );
    }

    #endregion

    #region Failure Tests

    /// <summary>
    /// Verifies that restoration fails when the selected backup directory does not exist.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenBackupDoesNotExist_ThrowsDirectoryNotFoundException()
    {
        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => _processor.RestoreBackup(
                instance,
                "Missing Backup"
            )
        );
    }

    /// <summary>
    /// Verifies that restoration rejects a backup name containing parent traversal.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenBackupNameEscapesBackupsDirectory_ThrowsInvalidDataException()
    {
        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        Assert.ThrowsExactly<InvalidDataException>(
            () => _processor.RestoreBackup(
                instance,
                Path.Combine("..", "Outside")
            )
        );
    }

    /// <summary>
    /// Verifies that restoration fails when a manifest payload has been removed.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenPayloadIsMissing_ThrowsDirectoryNotFoundException()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var target = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var instance = CreateInstanceContext(target);
        var manifest = CreateBackup(instance, FirstBackupTime);
        var payloadPath = Path.Combine(_backupsPath, manifest.BackupName, "data");

        Directory.Delete(payloadPath, recursive: true);

        Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => _processor.RestoreBackup(
                instance,
                manifest.BackupName
            )
        );
    }

    /// <summary>
    /// Verifies that a disabled instance cannot receive restored data.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenInstanceIsDisabled_ThrowsInvalidOperationException()
    {
        File.WriteAllText(Path.Combine(_sourcePath, "save.dat"), "Save data");

        var target = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var enabledInstance = CreateInstanceContext(target);
        var manifest = CreateBackup(enabledInstance, FirstBackupTime);

        var disabledInstance = CreateInstanceContext(
            enabled: false,
            targets: [target]
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.RestoreBackup(
                disabledInstance,
                manifest.BackupName
            )
        );
    }

    #endregion

    #region Result Tests

    /// <summary>
    /// Verifies that a successful restore reports its completion time, file count, and restored byte count.
    /// </summary>
    [TestMethod]
    public void RestoreBackup_WhenSuccessful_ReturnsRestoreSummary()
    {
        const string firstFileContent = "12345";
        const string secondFileContent = "1234567890";

        File.WriteAllText(
            Path.Combine(_sourcePath, "first.dat"),
            firstFileContent
        );

        File.WriteAllText(
            Path.Combine(_sourcePath, "second.dat"),
            secondFileContent
        );

        var target = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var instance = CreateInstanceContext(target);
        var manifest = CreateBackup(instance, FirstBackupTime);

        var result = _processor.RestoreBackup(
            instance,
            manifest.BackupName
        );

        Assert.AreEqual(manifest.BackupName, result.BackupName);
        Assert.AreEqual(RestoreTime, result.CompletedUtc);
        Assert.HasCount(1, result.Entries);

        var resultEntry = result.Entries.Single();

        Assert.AreEqual("data", resultEntry.TargetId);
        Assert.AreEqual(TargetPathType.Directory, resultEntry.Type);
        Assert.AreEqual(2L, resultEntry.FileCount);
        Assert.AreEqual(firstFileContent.Length + secondFileContent.Length, resultEntry.TotalBytes);
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates an enabled instance context containing the supplied targets.
    /// </summary>
    /// <param name="targets">The targets assigned to the instance.</param>
    /// <returns>A runtime instance context suitable for restore tests.</returns>
    private InstanceContext CreateInstanceContext(params TargetPath[] targets)
    {
        return CreateInstanceContext(
            enabled: true,
            targets: targets
        );
    }

    /// <summary>
    /// Creates an instance context with the specified enabled state and targets.
    /// </summary>
    /// <param name="enabled">A value indicating whether the instance is enabled.</param>
    /// <param name="targets">The targets assigned to the instance.</param>
    /// <returns>A runtime instance context suitable for restore tests.</returns>
    private InstanceContext CreateInstanceContext(
        bool enabled,
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
                Enabled = enabled,
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
    /// <param name="enabled">A value indicating whether the target is enabled.</param>
    /// <param name="required">A value indicating whether the source must exist when creating a backup.</param>
    /// <returns>A configured target suitable for restore tests.</returns>
    private static TargetPath CreateTarget(
        string id,
        string source,
        string backupPath,
        TargetPathType type,
        bool enabled = true,
        bool required = true
    )
    {
        return new TargetPath
        {
            Id = id,
            Name = id,
            Enabled = enabled,
            Required = required,
            AllowClear = false,
            Source = source,
            Type = type,
            BackupPath = backupPath
        };
    }

    /// <summary>
    /// Creates a completed backup using a deterministic creation time.
    /// </summary>
    /// <param name="instance">The instance to back up.</param>
    /// <param name="backupTime">The UTC creation time assigned to the backup.</param>
    /// <returns>The manifest describing the completed backup.</returns>
    private static BackupManifest CreateBackup(
        InstanceContext instance,
        DateTimeOffset backupTime
    )
    {
        var backupProcessor = new BackupProcessor(
            new FixedTimeProvider(backupTime)
        );

        return backupProcessor.CreateBackup(instance);
    }

    #endregion

    #region Test Types

    /// <summary>
    /// Supplies a fixed UTC time so backup and restore timestamps can be tested deterministically.
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        #region Properties

        /// <summary>
        /// Gets the UTC value returned by the time provider.
        /// </summary>
        private DateTimeOffset UtcNow { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new fixed time provider.
        /// </summary>
        /// <param name="utcNow">The UTC date and time returned by the provider.</param>
        internal FixedTimeProvider(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the fixed UTC date and time supplied during construction.
        /// </summary>
        /// <returns>The fixed UTC date and time.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return UtcNow;
        }

        #endregion
    }

    #endregion
}