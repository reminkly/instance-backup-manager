using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Catalogs;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests non-mutating restore comparison and selective target execution.
/// </summary>
[TestClass]
public sealed class RestorePreviewProcessorTests
{
    #region Fields

    private string _testRoot = null!;
    private string _instancePath = null!;
    private string _backupsPath = null!;

    #endregion

    #region Test Initialization

    [TestInitialize]
    public void TestInitialize()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "InstanceBackupManagerTests", Guid.NewGuid().ToString("N"));
        _instancePath = Path.Combine(_testRoot, "Instance");
        _backupsPath = Path.Combine(_instancePath, "backups");
        Directory.CreateDirectory(_backupsPath);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    #endregion

    #region Preview Tests

    [TestMethod]
    public void CreatePreview_WhenDirectoryHasDifferences_ClassifiesEveryFileWithoutChangingDestination()
    {
        var sourcePath = Path.Combine(_testRoot, "Source");
        Directory.CreateDirectory(sourcePath);
        File.WriteAllText(Path.Combine(sourcePath, "unchanged.txt"), "same");
        File.WriteAllText(Path.Combine(sourcePath, "overwrite.txt"), "backup value");
        File.WriteAllText(Path.Combine(sourcePath, "create.txt"), "new file");

        var instance = CreateInstance(
            CreateDirectoryTarget("data", sourcePath)
        );

        var manifest = new BackupProcessor().CreateBackup(instance);

        File.WriteAllText(Path.Combine(sourcePath, "overwrite.txt"), "current value");
        File.Delete(Path.Combine(sourcePath, "create.txt"));
        File.WriteAllText(Path.Combine(sourcePath, "preserve.txt"), "destination only");

        var preview = new RestorePreviewProcessor(new BackupCatalog()).CreatePreview(
            instance,
            manifest.BackupName
        );
        var target = preview.Targets.Single();

        Assert.AreEqual(1L, target.CreateCount);
        Assert.AreEqual(1L, target.OverwriteCount);
        Assert.AreEqual(1L, target.UnchangedCount);
        Assert.AreEqual(1L, target.PreserveCount);
        Assert.AreEqual("current value", File.ReadAllText(Path.Combine(sourcePath, "overwrite.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(sourcePath, "create.txt")));
        Assert.AreEqual("destination only", File.ReadAllText(Path.Combine(sourcePath, "preserve.txt")));
    }

    #endregion

    #region Selective Restore Tests

    [TestMethod]
    public void RestoreBackup_WhenOneTargetIsSelected_RestoresOnlyThatTarget()
    {
        var firstPath = Path.Combine(_testRoot, "First", "first.sav");
        var secondPath = Path.Combine(_testRoot, "Second", "second.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(secondPath)!);
        File.WriteAllText(firstPath, "first backup");
        File.WriteAllText(secondPath, "second backup");

        var instance = CreateInstance(
            CreateFileTarget("first", firstPath),
            CreateFileTarget("second", secondPath)
        );

        var manifest = new BackupProcessor().CreateBackup(instance);
        File.WriteAllText(firstPath, "first current");
        File.WriteAllText(secondPath, "second current");

        var result = new RestoreProcessor().RestoreBackup(
            instance,
            manifest.BackupName,
            ["first"]
        );

        Assert.HasCount(1, result.Entries);
        Assert.AreEqual("first", result.Entries.Single().TargetId);
        Assert.AreEqual("first backup", File.ReadAllText(firstPath));
        Assert.AreEqual("second current", File.ReadAllText(secondPath));
    }

    [TestMethod]
    public void RestoreBackup_WhenSelectedTargetDoesNotExist_ChangesNoDestinationFiles()
    {
        var filePath = Path.Combine(_testRoot, "Data", "game.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "backup");

        var instance = CreateInstance(CreateFileTarget("save", filePath));
        var manifest = new BackupProcessor().CreateBackup(instance);
        File.WriteAllText(filePath, "current");

        Assert.ThrowsExactly<InvalidDataException>(
            () => new RestoreProcessor().RestoreBackup(
                instance,
                manifest.BackupName,
                ["missing"]
            )
        );

        Assert.AreEqual("current", File.ReadAllText(filePath));
    }

    #endregion

    #region Metadata Integration Tests

    [TestMethod]
    public void CreateBackup_WhenNotesAndTagsAreProvided_PersistsNormalizedMetadata()
    {
        var filePath = Path.Combine(_testRoot, "Data", "game.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "save data");

        var instance = CreateInstance(CreateFileTarget("save", filePath));
        var manifest = new BackupProcessor().CreateBackup(
            instance,
            BackupKind.Manual,
            "Milestone",
            "  Before final boss  ",
            [" story ", "STORY", "milestone"]
        );

        var discovered = new BackupCatalog()
            .GetCompletedBackup(instance, manifest.BackupName)
            .Manifest;

        Assert.AreEqual("Before final boss", discovered.Notes);
        CollectionAssert.AreEqual(
            new[] { "story", "milestone" },
            discovered.Tags.ToArray()
        );
    }

    #endregion

    #region Test Helpers

    private InstanceContext CreateInstance(params TargetPath[] targets)
    {
        return new InstanceContext
        {
            InstancePath = _instancePath,
            ConfigPath = Path.Combine(_instancePath, "instance.json"),
            BackupsPath = _backupsPath,
            Config = new InstanceConfig
            {
                Name = "Test Instance",
                Targets = [.. targets]
            }
        };
    }

    private static TargetPath CreateDirectoryTarget(
        string id,
        string source
    )
    {
        return CreateTarget(id, source, TargetPathType.Directory);
    }

    private static TargetPath CreateFileTarget(
        string id,
        string source
    )
    {
        return CreateTarget(id, source, TargetPathType.File);
    }

    private static TargetPath CreateTarget(
        string id,
        string source,
        TargetPathType type
    )
    {
        return new TargetPath
        {
            Id = id,
            Name = id,
            Source = source,
            Type = type,
            Required = true
        };
    }

    #endregion
}
