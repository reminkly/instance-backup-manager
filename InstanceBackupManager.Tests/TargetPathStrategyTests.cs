using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Strategies;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests file and directory target strategies for backup, restore, inspection, clearing, and strategy resolution.
/// </summary>
[TestClass]
public sealed class TargetPathStrategyTests
{
    #region Fields

    private string _testRootPath = null!;
    private string _sourcePath = null!;
    private string _destinationPath = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Creates isolated source and destination paths before each test.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _testRootPath = Path.Combine(
            Path.GetTempPath(),
            "InstanceBackupManagerTests",
            Guid.NewGuid().ToString("N")
        );

        _sourcePath = Path.Combine(_testRootPath, "Source");
        _destinationPath = Path.Combine(_testRootPath, "Destination");

        Directory.CreateDirectory(_sourcePath);
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

    #region File Strategy Tests

    /// <summary>
    /// Verifies that the file strategy backs up a file and reports its size.
    /// </summary>
    [TestMethod]
    public void FileStrategy_Backup_CopiesFileAndReturnsStatistics()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");
        var destinationFilePath = Path.Combine(_destinationPath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        IBackupTargetStrategy strategy = new FileTargetStrategy();

        var result = strategy.Backup(
            sourceFilePath,
            destinationFilePath
        );

        Assert.IsTrue(File.Exists(destinationFilePath));
        Assert.AreEqual("Save data", File.ReadAllText(destinationFilePath));
        Assert.AreEqual(1L, result.FileCount);
        Assert.AreEqual(9L, result.TotalBytes);
    }

    /// <summary>
    /// Verifies that the file strategy restores and overwrites an existing file.
    /// </summary>
    [TestMethod]
    public void FileStrategy_Restore_OverwritesDestination()
    {
        var payloadFilePath = Path.Combine(_sourcePath, "save.dat");
        var destinationFilePath = Path.Combine(_destinationPath, "save.dat");

        Directory.CreateDirectory(_destinationPath);
        File.WriteAllText(payloadFilePath, "Original");
        File.WriteAllText(destinationFilePath, "Changed");

        IRestoreTargetStrategy strategy = new FileTargetStrategy();

        var result = strategy.Restore(
            payloadFilePath,
            destinationFilePath
        );

        Assert.AreEqual("Original", File.ReadAllText(destinationFilePath));
        Assert.AreEqual(1L, result.FileCount);
        Assert.AreEqual(8L, result.TotalBytes);
    }

    /// <summary>
    /// Verifies that the file strategy inspects and clears an existing file.
    /// </summary>
    [TestMethod]
    public void FileStrategy_InspectAndClear_ReportsAndDeletesFile()
    {
        var sourceFilePath = Path.Combine(_sourcePath, "save.dat");

        File.WriteAllText(sourceFilePath, "Save data");

        IClearTargetStrategy strategy = new FileTargetStrategy();

        var result = strategy.Inspect(sourceFilePath);

        strategy.Clear(sourceFilePath);

        Assert.AreEqual(1L, result.FileCount);
        Assert.AreEqual(9L, result.TotalBytes);
        Assert.IsFalse(File.Exists(sourceFilePath));
    }

    #endregion

    #region Directory Strategy Tests

    /// <summary>
    /// Verifies that the directory strategy backs up nested contents and preserves empty directories.
    /// </summary>
    [TestMethod]
    public void DirectoryStrategy_Backup_CopiesNestedContentsAndEmptyDirectories()
    {
        var nestedSourcePath = Path.Combine(_sourcePath, "Nested");
        var emptySourcePath = Path.Combine(_sourcePath, "Empty");

        Directory.CreateDirectory(nestedSourcePath);
        Directory.CreateDirectory(emptySourcePath);
        File.WriteAllText(Path.Combine(nestedSourcePath, "mod.txt"), "Mod");

        IBackupTargetStrategy strategy = new DirectoryTargetStrategy();

        var result = strategy.Backup(
            _sourcePath,
            _destinationPath
        );

        Assert.IsTrue(File.Exists(Path.Combine(_destinationPath, "Nested", "mod.txt")));
        Assert.IsTrue(Directory.Exists(Path.Combine(_destinationPath, "Empty")));
        Assert.AreEqual(1L, result.FileCount);
        Assert.AreEqual(3L, result.TotalBytes);
    }

    /// <summary>
    /// Verifies that the directory strategy restores backed-up contents while preserving unrelated destination files.
    /// </summary>
    [TestMethod]
    public void DirectoryStrategy_Restore_PreservesUnrelatedDestinationFiles()
    {
        var payloadFilePath = Path.Combine(_sourcePath, "save.dat");
        var destinationFilePath = Path.Combine(_destinationPath, "save.dat");
        var unrelatedFilePath = Path.Combine(_destinationPath, "unrelated.dat");

        Directory.CreateDirectory(_destinationPath);
        File.WriteAllText(payloadFilePath, "Original");
        File.WriteAllText(destinationFilePath, "Changed");
        File.WriteAllText(unrelatedFilePath, "Unrelated");

        IRestoreTargetStrategy strategy = new DirectoryTargetStrategy();

        var result = strategy.Restore(
            _sourcePath,
            _destinationPath
        );

        Assert.AreEqual("Original", File.ReadAllText(destinationFilePath));
        Assert.AreEqual("Unrelated", File.ReadAllText(unrelatedFilePath));
        Assert.AreEqual(1L, result.FileCount);
        Assert.AreEqual(8L, result.TotalBytes);
    }

    /// <summary>
    /// Verifies that the directory strategy inspects and clears contents while preserving the configured root.
    /// </summary>
    [TestMethod]
    public void DirectoryStrategy_InspectAndClear_PreservesRootDirectory()
    {
        var nestedPath = Path.Combine(_sourcePath, "Nested");

        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(_sourcePath, "first.dat"), "12345");
        File.WriteAllText(Path.Combine(nestedPath, "second.dat"), "123");

        IClearTargetStrategy strategy = new DirectoryTargetStrategy();

        var result = strategy.Inspect(_sourcePath);

        strategy.Clear(_sourcePath);

        Assert.AreEqual(2L, result.FileCount);
        Assert.AreEqual(8L, result.TotalBytes);
        Assert.IsTrue(Directory.Exists(_sourcePath));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(_sourcePath));
    }

    #endregion

    #region Resolver Tests

    /// <summary>
    /// Verifies that the resolver returns the strategy registered for the requested target type.
    /// </summary>
    [TestMethod]
    public void Resolve_WhenMatchingStrategyExists_ReturnsStrategy()
    {
        IReadOnlyCollection<IBackupTargetStrategy> strategies =
        [
            new FileTargetStrategy(),
            new DirectoryTargetStrategy()
        ];

        var strategy = TargetPathStrategyResolver.Resolve(
            strategies,
            TargetPathType.File
        );

        Assert.IsInstanceOfType<FileTargetStrategy>(strategy);
    }

    /// <summary>
    /// Verifies that the resolver rejects a target type without a registered strategy.
    /// </summary>
    [TestMethod]
    public void Resolve_WhenMatchingStrategyDoesNotExist_ThrowsInvalidOperationException()
    {
        IReadOnlyCollection<IBackupTargetStrategy> strategies =
        [
            new FileTargetStrategy()
        ];

        Assert.ThrowsExactly<InvalidOperationException>(
            () => TargetPathStrategyResolver.Resolve(
                strategies,
                TargetPathType.Directory
            )
        );
    }

    /// <summary>
    /// Verifies that the resolver rejects duplicate strategies for the same target type.
    /// </summary>
    [TestMethod]
    public void Resolve_WhenMultipleStrategiesMatch_ThrowsInvalidOperationException()
    {
        IReadOnlyCollection<IBackupTargetStrategy> strategies =
        [
            new FileTargetStrategy(),
            new FileTargetStrategy()
        ];

        Assert.ThrowsExactly<InvalidOperationException>(
            () => TargetPathStrategyResolver.Resolve(
                strategies,
                TargetPathType.File
            )
        );
    }

    #endregion
}