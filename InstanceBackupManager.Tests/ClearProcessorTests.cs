using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests clear eligibility, filesystem behavior, path safety, and operation results.
/// </summary>
[TestClass]
public sealed class ClearProcessorTests
{
    #region Fields

    private static readonly DateTimeOffset ClearTime = new(2026, 7, 31, 18, 30, 15, TimeSpan.Zero);

    private string _testRootPath = null!;
    private string _instancePath = null!;
    private string _backupsPath = null!;
    private string _sourcePath = null!;
    private ClearProcessor _processor = null!;

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

        _processor = new ClearProcessor(
            new FixedTimeProvider(ClearTime)
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

    #region Directory Clear Tests

    /// <summary>
    /// Verifies that clearing a directory removes its files while preserving the configured root directory.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenDirectoryContainsFiles_RemovesFilesAndPreservesRootDirectory()
    {
        var firstFilePath = Path.Combine(_sourcePath, "first.dat");
        var secondFilePath = Path.Combine(_sourcePath, "second.dat");

        File.WriteAllText(firstFilePath, "First file");
        File.WriteAllText(secondFilePath, "Second file");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "data",
                source: _sourcePath,
                type: TargetPathType.Directory
            )
        );

        var result = _processor.ClearInstance(instance);

        Assert.IsTrue(Directory.Exists(_sourcePath));
        Assert.IsFalse(File.Exists(firstFilePath));
        Assert.IsFalse(File.Exists(secondFilePath));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(_sourcePath));
        Assert.HasCount(1, result.Entries);
    }

    /// <summary>
    /// Verifies that clearing a directory recursively removes nested files and directories.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenDirectoryContainsNestedDirectories_RemovesNestedContents()
    {
        var nestedDirectoryPath = Path.Combine(_sourcePath, "Mods", "Enabled");
        var nestedFilePath = Path.Combine(nestedDirectoryPath, "example.mod");

        Directory.CreateDirectory(nestedDirectoryPath);
        File.WriteAllText(nestedFilePath, "Mod data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "mods",
                source: _sourcePath,
                type: TargetPathType.Directory
            )
        );

        _processor.ClearInstance(instance);

        Assert.IsTrue(Directory.Exists(_sourcePath));
        Assert.IsFalse(Directory.Exists(Path.Combine(_sourcePath, "Mods")));
        Assert.IsFalse(File.Exists(nestedFilePath));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(_sourcePath));
    }

    /// <summary>
    /// Verifies that clearing a missing directory succeeds without creating the directory.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenDirectoryDoesNotExist_ReturnsZeroStatistics()
    {
        var missingDirectoryPath = Path.Combine(_testRootPath, "Missing Directory");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "missing",
                source: missingDirectoryPath,
                type: TargetPathType.Directory
            )
        );

        var result = _processor.ClearInstance(instance);
        var resultEntry = result.Entries.Single();

        Assert.IsFalse(Directory.Exists(missingDirectoryPath));
        Assert.AreEqual(0L, resultEntry.FileCount);
        Assert.AreEqual(0L, resultEntry.TotalBytes);
    }

    #endregion

    #region File Clear Tests

    /// <summary>
    /// Verifies that clearing a configured file removes the file while preserving its containing directory.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenTargetIsFile_DeletesFile()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "save",
                source: sourceFilePath,
                type: TargetPathType.File
            )
        );

        var result = _processor.ClearInstance(instance);
        var resultEntry = result.Entries.Single();

        Assert.IsFalse(File.Exists(sourceFilePath));
        Assert.IsTrue(Directory.Exists(_sourcePath));
        Assert.AreEqual(1L, resultEntry.FileCount);
        Assert.AreEqual("Save data".Length, resultEntry.TotalBytes);
    }

    /// <summary>
    /// Verifies that clearing a missing configured file succeeds with zero removal statistics.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenFileDoesNotExist_ReturnsZeroStatistics()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "missing.dat");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "save",
                source: sourceFilePath,
                type: TargetPathType.File
            )
        );

        var result = _processor.ClearInstance(instance);
        var resultEntry = result.Entries.Single();

        Assert.IsFalse(File.Exists(sourceFilePath));
        Assert.AreEqual(0L, resultEntry.FileCount);
        Assert.AreEqual(0L, resultEntry.TotalBytes);
    }

    #endregion

    #region Eligibility Tests

    /// <summary>
    /// Verifies that a disabled target is not cleared even when it permits clearing.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenTargetIsDisabled_DoesNotClearTarget()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "save",
                source: sourceFilePath,
                type: TargetPathType.File,
                enabled: false
            )
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.ClearInstance(instance)
        );

        Assert.IsTrue(File.Exists(sourceFilePath));
    }

    /// <summary>
    /// Verifies that a target is not cleared unless it explicitly permits the clear operation.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenTargetDoesNotAllowClear_DoesNotClearTarget()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "save",
                source: sourceFilePath,
                type: TargetPathType.File,
                allowClear: false
            )
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.ClearInstance(instance)
        );

        Assert.IsTrue(File.Exists(sourceFilePath));
    }

    /// <summary>
    /// Verifies that eligible targets are cleared while disabled and disallowed targets remain unchanged.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenTargetsHaveDifferentEligibility_OnlyClearsEligibleTargets()
    {
        var eligibleFilePath = Path.Combine(_sourcePath, "eligible.dat");
        var disabledFilePath = Path.Combine(_sourcePath, "disabled.dat");
        var disallowedFilePath = Path.Combine(_sourcePath, "disallowed.dat");

        File.WriteAllText(eligibleFilePath, "Eligible");
        File.WriteAllText(disabledFilePath, "Disabled");
        File.WriteAllText(disallowedFilePath, "Disallowed");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "eligible",
                source: eligibleFilePath,
                type: TargetPathType.File
            ),
            CreateTarget(
                id: "disabled",
                source: disabledFilePath,
                type: TargetPathType.File,
                enabled: false
            ),
            CreateTarget(
                id: "disallowed",
                source: disallowedFilePath,
                type: TargetPathType.File,
                allowClear: false
            )
        );

        var result = _processor.ClearInstance(instance);

        Assert.IsFalse(File.Exists(eligibleFilePath));
        Assert.IsTrue(File.Exists(disabledFilePath));
        Assert.IsTrue(File.Exists(disallowedFilePath));
        Assert.HasCount(1, result.Entries);
        Assert.AreEqual("eligible", result.Entries.Single().TargetId);
    }

    /// <summary>
    /// Verifies that a disabled instance cannot clear any configured targets.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenInstanceIsDisabled_ThrowsInvalidOperationException()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        var target = CreateTarget(
            id: "save",
            source: sourceFilePath,
            type: TargetPathType.File
        );

        var instance = CreateInstanceContext(
            enabled: false,
            targets: [target]
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.ClearInstance(instance)
        );

        Assert.IsTrue(File.Exists(sourceFilePath));
    }

    #endregion

    #region Path Safety Tests

    /// <summary>
    /// Verifies that a filesystem root cannot be used as a clear target.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenTargetIsFilesystemRoot_ThrowsInvalidOperationException()
    {
        var rootPath = Path.GetPathRoot(_testRootPath)!;

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "root",
                source: rootPath,
                type: TargetPathType.Directory
            )
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.ClearInstance(instance)
        );
    }

    /// <summary>
    /// Verifies that the instance directory itself cannot be cleared.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenTargetIsInstanceDirectory_ThrowsInvalidOperationException()
    {
        var instance = CreateInstanceContext(
            CreateTarget(
                id: "instance",
                source: _instancePath,
                type: TargetPathType.Directory
            )
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.ClearInstance(instance)
        );

        Assert.IsTrue(Directory.Exists(_instancePath));
    }

    /// <summary>
    /// Verifies that a target overlapping the backups directory cannot be cleared.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenTargetOverlapsBackupsDirectory_ThrowsInvalidOperationException()
    {
        Directory.CreateDirectory(_backupsPath);

        var backupFilePath = Path.Combine(_backupsPath, "backup.dat");

        File.WriteAllText(backupFilePath, "Backup data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "backups",
                source: _backupsPath,
                type: TargetPathType.Directory
            )
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.ClearInstance(instance)
        );

        Assert.IsTrue(File.Exists(backupFilePath));
    }

    /// <summary>
    /// Verifies that overlapping clear targets are rejected before either target is modified.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenTargetsOverlap_ThrowsBeforeDeletingFiles()
    {
        var nestedDirectoryPath = Path.Combine(_sourcePath, "Nested");
        var sourceFilePath = Path.Combine(nestedDirectoryPath, "save.dat");

        Directory.CreateDirectory(nestedDirectoryPath);
        File.WriteAllText(sourceFilePath, "Save data");

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "parent",
                source: _sourcePath,
                type: TargetPathType.Directory
            ),
            CreateTarget(
                id: "child",
                source: nestedDirectoryPath,
                type: TargetPathType.Directory
            )
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.ClearInstance(instance)
        );

        Assert.IsTrue(File.Exists(sourceFilePath));
    }

    /// <summary>
    /// Verifies that all targets are validated before an otherwise valid target is modified.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenLaterTargetIsUnsafe_DoesNotClearEarlierTarget()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");
        Directory.CreateDirectory(_backupsPath);

        var instance = CreateInstanceContext(
            CreateTarget(
                id: "save",
                source: sourceFilePath,
                type: TargetPathType.File
            ),
            CreateTarget(
                id: "backups",
                source: _backupsPath,
                type: TargetPathType.Directory
            )
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.ClearInstance(instance)
        );

        Assert.IsTrue(File.Exists(sourceFilePath));
    }

    /// <summary>
    /// Verifies that an existing directory cannot be cleared through a target configured as a file.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenTargetTypeDoesNotMatchSource_ThrowsInvalidOperationException()
    {
        var instance = CreateInstanceContext(
            CreateTarget(
                id: "incorrect-type",
                source: _sourcePath,
                type: TargetPathType.File
            )
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.ClearInstance(instance)
        );

        Assert.IsTrue(Directory.Exists(_sourcePath));
    }

    #endregion

    #region Result Tests

    /// <summary>
    /// Verifies that a completed clear operation reports its time, target information, file count, and removed byte count.
    /// </summary>
    [TestMethod]
    public void ClearInstance_WhenSuccessful_ReturnsClearSummary()
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
                id: "data",
                source: _sourcePath,
                type: TargetPathType.Directory
            )
        );

        var result = _processor.ClearInstance(instance);
        var resultEntry = result.Entries.Single();

        Assert.AreEqual(ClearTime, result.CompletedUtc);
        Assert.HasCount(1, result.Entries);
        Assert.AreEqual("data", resultEntry.TargetId);
        Assert.AreEqual("data", resultEntry.TargetName);
        Assert.AreEqual(Path.GetFullPath(_sourcePath), resultEntry.SourcePath);
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
    /// <returns>A runtime instance context suitable for clear tests.</returns>
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
    /// <returns>A runtime instance context suitable for clear tests.</returns>
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
    /// <param name="type">The source filesystem-entry type.</param>
    /// <param name="enabled">A value indicating whether the target is enabled.</param>
    /// <param name="allowClear">A value indicating whether the target permits clearing.</param>
    /// <returns>A configured target suitable for clear tests.</returns>
    private static TargetPath CreateTarget(
        string id,
        string source,
        TargetPathType type,
        bool enabled = true,
        bool allowClear = true
    )
    {
        return new TargetPath
        {
            Id = id,
            Name = id,
            Enabled = enabled,
            Required = true,
            AllowClear = allowClear,
            Source = source,
            Type = type
        };
    }

    #endregion

    #region Test Types

    /// <summary>
    /// Supplies a fixed UTC time so clear completion timestamps can be tested deterministically.
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