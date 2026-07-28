using System.Text.Json;
using System.Text.Json.Nodes;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Catalogs;
using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests current-configuration matching, restore validation, restore execution, and restore result reporting.
/// </summary>
[TestClass]
public sealed class BackupCatalogTests
{
    #region Fields

    private static readonly DateTimeOffset FirstBackupTime = new(2026, 7, 28, 18, 30, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondBackupTime = new(2026, 7, 29, 19, 45, 10, TimeSpan.Zero);

    private string _testRootPath = null!;
    private string _instancePath = null!;
    private string _backupsPath = null!;
    private string _sourcePath = null!;
    private BackupCatalog _catalog = null!;

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
        _backupsPath = Path.Combine(
            _instancePath,
            BackupStorageConstants.BackupsDirectoryName
        );

        _sourcePath = Path.Combine(_testRootPath, "Source Data");

        Directory.CreateDirectory(_instancePath);
        Directory.CreateDirectory(_sourcePath);

        _catalog = new BackupCatalog();
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

    #region Backup Discovery Tests

    /// <summary>
    /// Verifies that discovering backups returns an empty collection when the backups directory does not exist.
    /// </summary>
    [TestMethod]
    public void DiscoverBackups_WhenBackupsDirectoryDoesNotExist_ReturnsEmptyCollection()
    {
        var instance = CreateInstanceContext();

        var backups = _catalog.DiscoverBackups(instance);

        Assert.IsEmpty(backups);
    }

    /// <summary>
    /// Verifies that incomplete and unmanaged directories are not returned as completed backups.
    /// </summary>
    [TestMethod]
    public void DiscoverBackups_WhenDirectoriesDoNotContainManifests_SkipsDirectories()
    {
        Directory.CreateDirectory(_backupsPath);

        Directory.CreateDirectory(
            Path.Combine(
                _backupsPath,
                $"{BackupStorageConstants.InProgressDirectoryPrefix}test"
            )
        );

        Directory.CreateDirectory(
            Path.Combine(
                _backupsPath,
                "Not A Backup"
            )
        );

        var instance = CreateInstanceContext();

        var backups = _catalog.DiscoverBackups(instance);

        Assert.IsEmpty(backups);
    }

    /// <summary>
    /// Verifies that completed backups are returned from newest to oldest.
    /// </summary>
    [TestMethod]
    public void DiscoverBackups_WhenMultipleBackupsExist_ReturnsNewestBackupFirst()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateFileTarget(sourceFilePath)
        );

        var firstBackupProcessor = new BackupProcessor(
            new FixedTimeProvider(FirstBackupTime)
        );

        var secondBackupProcessor = new BackupProcessor(
            new FixedTimeProvider(SecondBackupTime)
        );

        var firstManifest = firstBackupProcessor.CreateBackup(instance);
        var secondManifest = secondBackupProcessor.CreateBackup(instance);

        var backups = _catalog
            .DiscoverBackups(instance)
            .ToList();

        Assert.HasCount(2, backups);
        Assert.AreEqual(secondManifest.BackupName, backups[0].BackupName);
        Assert.AreEqual(firstManifest.BackupName, backups[1].BackupName);
    }

    /// <summary>
    /// Verifies that malformed manifest JSON prevents a directory from being treated as a valid completed backup.
    /// </summary>
    [TestMethod]
    public void DiscoverBackups_WhenManifestContainsInvalidJson_ThrowsInvalidDataException()
    {
        var backupPath = Path.Combine(_backupsPath, "Invalid Backup");

        Directory.CreateDirectory(backupPath);

        File.WriteAllText(
            Path.Combine(
                backupPath,
                BackupStorageConstants.ManifestFileName
            ),
            "{ invalid JSON }"
        );

        var instance = CreateInstanceContext();

        Assert.ThrowsExactly<InvalidDataException>(
            () => _catalog.DiscoverBackups(instance)
        );
    }

    #endregion

    #region Completed Backup Tests

    /// <summary>
    /// Verifies that a completed backup can be loaded directly by its directory name.
    /// </summary>
    [TestMethod]
    public void GetCompletedBackup_WhenBackupExists_ReturnsBackupDescriptor()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateFileTarget(sourceFilePath)
        );

        var manifest = new BackupProcessor(
            new FixedTimeProvider(FirstBackupTime)
        ).CreateBackup(instance);

        var backup = _catalog.GetCompletedBackup(
            instance,
            manifest.BackupName
        );

        Assert.AreEqual(manifest.BackupName, backup.BackupName);
        Assert.AreEqual(Path.Combine(_backupsPath, manifest.BackupName), backup.BackupPath);
        Assert.AreEqual(manifest.CreatedUtc, backup.Manifest.CreatedUtc);
        Assert.AreEqual(manifest.InstanceName, backup.Manifest.InstanceName);
        Assert.HasCount(1, backup.Manifest.Entries);
    }

    /// <summary>
    /// Verifies that loading a missing backup reports that its directory does not exist.
    /// </summary>
    [TestMethod]
    public void GetCompletedBackup_WhenBackupDoesNotExist_ThrowsDirectoryNotFoundException()
    {
        var instance = CreateInstanceContext();

        Assert.ThrowsExactly<DirectoryNotFoundException>(
            () => _catalog.GetCompletedBackup(
                instance,
                "Missing Backup"
            )
        );
    }

    /// <summary>
    /// Verifies that a selected backup name cannot escape the instance's backups directory.
    /// </summary>
    [TestMethod]
    public void GetCompletedBackup_WhenBackupNameEscapesBackupsDirectory_ThrowsInvalidDataException()
    {
        var instance = CreateInstanceContext();

        Assert.ThrowsExactly<InvalidDataException>(
            () => _catalog.GetCompletedBackup(
                instance,
                Path.Combine("..", "Outside")
            )
        );
    }

    /// <summary>
    /// Verifies that a selected directory without a manifest is not considered a completed backup.
    /// </summary>
    [TestMethod]
    public void GetCompletedBackup_WhenManifestDoesNotExist_ThrowsFileNotFoundException()
    {
        const string backupName = "Incomplete Backup";

        Directory.CreateDirectory(
            Path.Combine(
                _backupsPath,
                backupName
            )
        );

        var instance = CreateInstanceContext();

        Assert.ThrowsExactly<FileNotFoundException>(
            () => _catalog.GetCompletedBackup(
                instance,
                backupName
            )
        );
    }

    #endregion

    #region Backup Kind Tests

    /// <summary>
    /// Verifies that discovery preserves the backup kind stored in the manifest.
    /// </summary>
    [TestMethod]
    public void DiscoverBackups_WhenManifestHasPreRestoreKind_ReturnsPreRestoreKind()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateFileTarget(sourceFilePath)
        );

        var backupProcessor = new BackupProcessor(
            new FixedTimeProvider(FirstBackupTime)
        );

        backupProcessor.CreateBackup(
            instance,
            BackupKind.PreRestore
        );

        var backup = _catalog
            .DiscoverBackups(instance)
            .Single();

        Assert.AreEqual(BackupKind.PreRestore, backup.Manifest.Kind);
    }

    /// <summary>
    /// Verifies that a legacy manifest without a backup kind is treated as a manual backup.
    /// </summary>
    [TestMethod]
    public void DiscoverBackups_WhenLegacyManifestHasNoKind_UsesManualKind()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateFileTarget(sourceFilePath)
        );

        var manifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        var manifestPath = Path.Combine(
            _backupsPath,
            manifest.BackupName,
            BackupStorageConstants.ManifestFileName
        );

        var manifestJson = File.ReadAllText(manifestPath);
        var manifestNode = JsonNode.Parse(manifestJson)!.AsObject();

        manifestNode.Remove("Kind");

        File.WriteAllText(
            manifestPath,
            manifestNode.ToJsonString(
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            )
        );

        var backup = _catalog
            .DiscoverBackups(instance)
            .Single();

        Assert.AreEqual(BackupKind.Manual, backup.Manifest.Kind);
    }

    /// <summary>
    /// Verifies that discovery rejects a manifest containing an unsupported numeric backup kind.
    /// </summary>
    [TestMethod]
    public void DiscoverBackups_WhenManifestHasUnsupportedKind_ThrowsInvalidDataException()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateFileTarget(sourceFilePath)
        );

        var manifest = CreateBackup(
            instance,
            FirstBackupTime
        );

        var manifestPath = Path.Combine(
            _backupsPath,
            manifest.BackupName,
            BackupStorageConstants.ManifestFileName
        );

        var manifestJson = File.ReadAllText(manifestPath);
        var manifestNode = JsonNode.Parse(manifestJson)!.AsObject();

        manifestNode["Kind"] = 999;

        File.WriteAllText(
            manifestPath,
            manifestNode.ToJsonString(
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            )
        );

        Assert.ThrowsExactly<InvalidDataException>(
            () => _catalog.DiscoverBackups(instance)
        );
    }

    #endregion

    #region Test Helpers

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

    /// <summary>
    /// Creates an enabled instance context containing the supplied targets.
    /// </summary>
    /// <param name="targets">The targets assigned to the instance.</param>
    /// <returns>A runtime instance context suitable for catalog tests.</returns>
    private InstanceContext CreateInstanceContext(params TargetPath[] targets)
    {
        return new InstanceContext
        {
            InstancePath = _instancePath,
            ConfigPath = Path.Combine(
                _instancePath,
                BackupStorageConstants.InstanceConfigurationFileName
            ),
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
    /// Creates a file target using the supplied source path.
    /// </summary>
    /// <param name="sourcePath">The absolute path of the source file.</param>
    /// <returns>A configured file target suitable for catalog tests.</returns>
    private static TargetPath CreateFileTarget(string sourcePath)
    {
        return new TargetPath
        {
            Id = "save",
            Name = "Save Data",
            Enabled = true,
            Required = true,
            AllowClear = false,
            Source = sourcePath,
            Type = TargetPathType.File,
            BackupPath = Path.Combine(
                "save",
                "save.dat"
            )
        };
    }

    #endregion

    #region Test Types

    /// <summary>
    /// Supplies a fixed UTC date and time so backup timestamps can be tested deterministically.
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