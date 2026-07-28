using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Processing.Tests;

/// <summary>
/// Tests timestamped backup creation, target copying, manifest generation, and incomplete-backup cleanup.
/// </summary>
[TestClass]
public sealed class BackupProcessorTests
{
    #region Fields

    private static readonly DateTimeOffset BackupTime = new(2026, 7, 28, 18, 30, 5, TimeSpan.Zero);

    private string _testRootPath = null!;
    private string _instancePath = null!;
    private string _backupsPath = null!;
    private string _sourcePath = null!;
    private BackupProcessor _processor = null!;

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

        _processor = new BackupProcessor(
            new FixedTimeProvider(BackupTime)
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

    #region Directory Backup Tests

    /// <summary>
    /// Verifies that a directory target and its files are copied into a timestamped backup directory.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenDirectoryTargetExists_CopiesDirectoryContents()
    {
        var nestedDirectoryPath = Path.Combine(_sourcePath, "Nested");
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");
        var nestedFilePath = Path.Combine(nestedDirectoryPath, "mod.txt");

        Directory.CreateDirectory(nestedDirectoryPath);
        File.WriteAllText(sourceFilePath, "Save data");
        File.WriteAllText(nestedFilePath, "Mod data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var manifest = _processor.CreateBackup(instance);
        var completedBackupPath = Path.Combine(_backupsPath, manifest.BackupName);

        Assert.IsTrue(Directory.Exists(completedBackupPath));
        Assert.IsTrue(File.Exists(Path.Combine(completedBackupPath, "data", "save.dat")));
        Assert.IsTrue(File.Exists(Path.Combine(completedBackupPath, "data", "Nested", "mod.txt")));

        Assert.AreEqual(
            "Save data",
            File.ReadAllText(Path.Combine(completedBackupPath, "data", "save.dat"))
        );

        Assert.AreEqual(
            "Mod data",
            File.ReadAllText(Path.Combine(completedBackupPath, "data", "Nested", "mod.txt"))
        );
    }

    /// <summary>
    /// Verifies that empty source directories are preserved in the backup.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenSourceContainsEmptyDirectory_PreservesEmptyDirectory()
    {
        var emptyDirectoryPath = Path.Combine(_sourcePath, "Empty Directory");

        Directory.CreateDirectory(emptyDirectoryPath);

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                backupPath: "data",
                type: TargetPathType.Directory
            )
        );

        var manifest = _processor.CreateBackup(instance);

        var backedUpEmptyDirectoryPath = Path.Combine(
            _backupsPath,
            manifest.BackupName,
            "data",
            "Empty Directory"
        );

        Assert.IsTrue(Directory.Exists(backedUpEmptyDirectoryPath));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(backedUpEmptyDirectoryPath));
    }

    #endregion

    #region File Backup Tests

    /// <summary>
    /// Verifies that a single-file target is copied to its configured backup path.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenFileTargetExists_CopiesFile()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "settings.ini");

        File.WriteAllText(sourceFilePath, "fullscreen=true");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "settings",
                source: sourceFilePath,
                backupPath: Path.Combine("settings", "settings.ini"),
                type: TargetPathType.File
            )
        );

        var manifest = _processor.CreateBackup(instance);

        var backedUpFilePath = Path.Combine(
            _backupsPath,
            manifest.BackupName,
            "settings",
            "settings.ini"
        );

        Assert.IsTrue(File.Exists(backedUpFilePath));
        Assert.AreEqual("fullscreen=true", File.ReadAllText(backedUpFilePath));
    }

    #endregion

    #region Target Selection Tests

    /// <summary>
    /// Verifies that disabled targets are not copied or included in the backup manifest.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenTargetIsDisabled_SkipsTarget()
    {
        var enabledSourcePath = Path.Combine(_testRootPath, "Enabled Source");
        var disabledSourcePath = Path.Combine(_testRootPath, "Disabled Source");

        Directory.CreateDirectory(enabledSourcePath);
        Directory.CreateDirectory(disabledSourcePath);

        File.WriteAllText(Path.Combine(enabledSourcePath, "enabled.txt"), "Enabled");
        File.WriteAllText(Path.Combine(disabledSourcePath, "disabled.txt"), "Disabled");

        var enabledTarget = CreateTarget(
            id: "enabled",
            source: enabledSourcePath,
            backupPath: "enabled",
            type: TargetPathType.Directory
        );

        var disabledTarget = CreateTarget(
            id: "disabled",
            source: disabledSourcePath,
            backupPath: "disabled",
            type: TargetPathType.Directory,
            enabled: false
        );

        var instance = CreateInstanceContext(
            enabledTarget,
            disabledTarget
        );

        var manifest = _processor.CreateBackup(instance);
        var completedBackupPath = Path.Combine(_backupsPath, manifest.BackupName);

        Assert.HasCount(1, manifest.Entries);
        Assert.AreEqual("enabled", manifest.Entries.Single().TargetId);
        Assert.IsTrue(Directory.Exists(Path.Combine(completedBackupPath, "enabled")));
        Assert.IsFalse(Directory.Exists(Path.Combine(completedBackupPath, "disabled")));
    }

    /// <summary>
    /// Verifies that an instance without enabled targets cannot create an empty backup.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenNoTargetsAreEnabled_ThrowsInvalidOperationException()
    {
        var disabledTarget = CreateTarget(
            id: "disabled",
            source: _sourcePath,
            backupPath: "disabled",
            type: TargetPathType.Directory,
            enabled: false
        );

        var instance = CreateInstanceContext(disabledTarget);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.CreateBackup(instance)
        );
    }

    /// <summary>
    /// Verifies that a disabled instance cannot create a backup.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenInstanceIsDisabled_ThrowsInvalidOperationException()
    {
        var target = CreateTarget(
            id: "data",
            source: _sourcePath,
            backupPath: "data",
            type: TargetPathType.Directory
        );

        var instance = CreateInstanceContext(
            enabled: false,
            targets: [target]
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.CreateBackup(instance)
        );
    }

    #endregion

    #region Manifest Tests

    /// <summary>
    /// Verifies that a completed backup contains a manifest file with aggregate information about the copied target.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenSuccessful_WritesManifest()
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

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "saves",
                source: _sourcePath,
                backupPath: "saves",
                type: TargetPathType.Directory
            )
        );

        var manifest = _processor.CreateBackup(instance);

        var manifestPath = Path.Combine(
            _backupsPath,
            manifest.BackupName,
            "manifest.json"
        );

        Assert.IsTrue(File.Exists(manifestPath));
        Assert.AreEqual("Test Instance", manifest.InstanceName);
        Assert.AreEqual(BackupTime, manifest.CreatedUtc);
        Assert.HasCount(1, manifest.Entries);

        var entry = manifest.Entries.Single();

        Assert.AreEqual("saves", entry.TargetId);
        Assert.AreEqual(TargetPathType.Directory, entry.Type);
        Assert.AreEqual(2L, entry.FileCount);
        Assert.AreEqual(firstFileContent.Length + secondFileContent.Length, entry.TotalBytes);
    }

    #endregion

    #region Backup Naming Tests

    /// <summary>
    /// Verifies that multiple backups created at the same reported time receive unique directory names.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenTimestampAlreadyExists_CreatesUniqueBackupName()
    {
        File.WriteAllText(
            Path.Combine(_sourcePath, "save.dat"),
            "Save data"
        );

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "saves",
                source: _sourcePath,
                backupPath: "saves",
                type: TargetPathType.Directory
            )
        );

        var firstManifest = _processor.CreateBackup(instance);
        var secondManifest = _processor.CreateBackup(instance);

        Assert.AreNotEqual(firstManifest.BackupName, secondManifest.BackupName);
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, firstManifest.BackupName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(_backupsPath, secondManifest.BackupName)));
    }

    #endregion

    #region Failure Cleanup Tests

    /// <summary>
    /// Verifies that a missing file target causes the backup to fail and removes the temporary backup directory.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenFileTargetDoesNotExist_RemovesTemporaryBackup()
    {
        var missingFilePath = Path.Combine(_sourcePath, "missing.dat");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "missing",
                source: missingFilePath,
                backupPath: Path.Combine("missing", "missing.dat"),
                type: TargetPathType.File
            )
        );

        Assert.ThrowsExactly<FileNotFoundException>(
            () => _processor.CreateBackup(instance)
        );

        Assert.IsTrue(Directory.Exists(_backupsPath));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(_backupsPath));
    }

    /// <summary>
    /// Verifies that a missing directory target causes the backup to fail and removes the temporary backup directory.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenDirectoryTargetDoesNotExist_RemovesTemporaryBackup()
    {
        var missingDirectoryPath = Path.Combine(_testRootPath, "Missing Source");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "missing",
                source: missingDirectoryPath,
                backupPath: "missing",
                type: TargetPathType.Directory
            )
        );

        Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => _processor.CreateBackup(instance)
        );

        Assert.IsTrue(Directory.Exists(_backupsPath));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(_backupsPath));
    }

    /// <summary>
    /// Verifies that a backup destination cannot escape the temporary backup directory.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenBackupPathEscapesBackupDirectory_ThrowsInvalidDataException()
    {
        File.WriteAllText(
            Path.Combine(_sourcePath, "save.dat"),
            "Save data"
        );

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "unsafe",
                source: _sourcePath,
                backupPath: Path.Combine("..", "escaped"),
                type: TargetPathType.Directory
            )
        );

        Assert.ThrowsExactly<InvalidDataException>(
            () => _processor.CreateBackup(instance)
        );

        Assert.IsTrue(Directory.Exists(_backupsPath));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(_backupsPath));
        Assert.IsFalse(Directory.Exists(Path.Combine(_backupsPath, "..", "escaped")));
    }

    #endregion

    #region Optional Target Tests

    /// <summary>
    /// Verifies that a missing optional target is skipped when another enabled target is available.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenOptionalTargetDoesNotExist_SkipsOptionalTarget()
    {
        var existingFilePath = Path.Combine(_sourcePath, "save.dat");
        var missingFilePath = Path.Combine(_sourcePath, "optional.rtc");

        File.WriteAllText(existingFilePath, "Save data");

        var requiredTarget = CreateTarget(
            id: "save",
            source: existingFilePath,
            backupPath: Path.Combine("saves", "save.dat"),
            type: TargetPathType.File
        );

        var optionalTarget = CreateTarget(
            id: "rtc",
            source: missingFilePath,
            backupPath: Path.Combine("saves", "optional.rtc"),
            type: TargetPathType.File,
            required: false
        );

        var instance = CreateInstanceContext(
            requiredTarget,
            optionalTarget
        );

        var manifest = _processor.CreateBackup(instance);
        var completedBackupPath = Path.Combine(_backupsPath, manifest.BackupName);

        Assert.HasCount(1, manifest.Entries);
        Assert.AreEqual("save", manifest.Entries.Single().TargetId);
        Assert.IsTrue(File.Exists(Path.Combine(completedBackupPath, "saves", "save.dat")));
        Assert.IsFalse(File.Exists(Path.Combine(completedBackupPath, "saves", "optional.rtc")));
    }

    /// <summary>
    /// Verifies that a backup fails when every enabled target is optional and currently missing.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenAllOptionalTargetsAreMissing_ThrowsInvalidOperationException()
    {
        var optionalTarget = CreateTarget(
            id: "rtc",
            source: Path.Combine(_sourcePath, "missing.rtc"),
            backupPath: Path.Combine("saves", "missing.rtc"),
            type: TargetPathType.File,
            required: false
        );

        var instance = CreateInstanceContext(optionalTarget);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.CreateBackup(instance)
        );

        Assert.IsTrue(Directory.Exists(_backupsPath));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(_backupsPath));
    }

    /// <summary>
    /// Verifies that a missing required file continues to fail the backup operation.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenRequiredFileDoesNotExist_ThrowsFileNotFoundException()
    {
        var requiredTarget = CreateTarget(
            id: "save",
            source: Path.Combine(_sourcePath, "missing.sav"),
            backupPath: Path.Combine("saves", "missing.sav"),
            type: TargetPathType.File,
            required: true
        );

        var instance = CreateInstanceContext(requiredTarget);

        Assert.ThrowsExactly<FileNotFoundException>(
            () => _processor.CreateBackup(instance)
        );
    }

    #endregion

    #region Backup Kind Tests

    /// <summary>
    /// Verifies that a backup created without an explicit kind is identified as a manual backup.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenKindIsNotSpecified_UsesManualKind()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "save",
                source: sourceFilePath,
                backupPath: Path.Combine("save", "save.dat"),
                type: TargetPathType.File
            )
        );

        var manifest = _processor.CreateBackup(instance);

        Assert.AreEqual(BackupKind.Manual, manifest.Kind);
    }

    /// <summary>
    /// Verifies that a backup explicitly created before restoration is identified as a pre-restore backup.
    /// </summary>
    [TestMethod]
    public void CreateBackup_WhenPreRestoreKindIsSpecified_UsesPreRestoreKind()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "save",
                source: sourceFilePath,
                backupPath: Path.Combine("save", "save.dat"),
                type: TargetPathType.File
            )
        );

        var manifest = _processor.CreateBackup(
            instance,
            BackupKind.PreRestore
        );

        Assert.AreEqual(BackupKind.PreRestore, manifest.Kind);
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates an enabled instance context containing the supplied targets.
    /// </summary>
    /// <param name="targets">The targets assigned to the instance.</param>
    /// <returns>A runtime instance context suitable for backup tests.</returns>
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
    /// <returns>A runtime instance context suitable for backup tests.</returns>
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
    /// <param name="required">A value indicating whether the source must exist when a backup is created.</param>
    /// <returns>A configured target suitable for backup tests.</returns>
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

    #endregion

    #region Test Types

    /// <summary>
    /// Supplies a fixed UTC time so backup naming and manifest timestamps can be tested deterministically.
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