using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests optional stored filenames and restoration to the current configured source filename.
/// </summary>
[TestClass]
public sealed class StoredNameTests
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

    #region Backup and Restore Tests

    [TestMethod]
    public void CreateAndRestore_WhenStoredNameIsConfigured_UsesAliasInBackupAndOriginalDestinationOnRestore()
    {
        var sourcePath = Path.Combine(
            _testRoot,
            "Source",
            "Legend of Zelda, The - The Minish Cap (USA).SaveRAM"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "backup value");

        var instance = CreateInstance(
            new TargetPath
            {
                Id = "minish-cap-save",
                Name = "Minish Cap SaveRAM",
                Source = sourcePath,
                Type = TargetPathType.File,
                StoredName = "minish-cap.SaveRAM"
            }
        );

        var manifest = new BackupProcessor().CreateBackup(instance);
        var manifestEntry = manifest.Entries.Single();
        var storedPath = Path.Combine(
            _backupsPath,
            manifest.BackupName,
            manifestEntry.BackupPath
        );

        Assert.AreEqual(
            Path.Combine("targets", "minish-cap-save", "minish-cap.SaveRAM"),
            manifestEntry.BackupPath
        );
        Assert.IsTrue(File.Exists(storedPath));

        File.WriteAllText(sourcePath, "current value");

        var previewTarget = new RestorePreviewProcessor(
            new InstanceBackupManager.Processing.Catalogs.BackupCatalog()
        )
            .CreatePreview(instance, manifest.BackupName)
            .Targets
            .Single();

        Assert.AreEqual(sourcePath, previewTarget.DestinationPath);
        Assert.AreEqual(Path.GetFileName(sourcePath), previewTarget.Files.Single().RelativePath);
        Assert.AreEqual(RestoreFileChangeKind.Overwrite, previewTarget.Files.Single().ChangeKind);

        new RestoreProcessor().RestoreBackup(
            instance,
            manifest.BackupName
        );

        Assert.AreEqual("backup value", File.ReadAllText(sourcePath));
        Assert.IsFalse(
            File.Exists(
                Path.Combine(
                    Path.GetDirectoryName(sourcePath)!,
                    "minish-cap.SaveRAM"
                )
            )
        );
    }

    [TestMethod]
    public void CreateBackup_WhenStoredNameIsMissing_PreservesOriginalSourceFilename()
    {
        var sourcePath = Path.Combine(_testRoot, "Source", "original.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "save data");

        var instance = CreateInstance(
            new TargetPath
            {
                Id = "save",
                Name = "Save",
                Source = sourcePath,
                Type = TargetPathType.File
            }
        );

        var manifest = new BackupProcessor().CreateBackup(instance);

        Assert.AreEqual(
            Path.Combine("targets", "save", "original.sav"),
            manifest.Entries.Single().BackupPath
        );
    }

    #endregion

    #region Validation Tests

    [TestMethod]
    [DataRow("../escape.sav")]
    [DataRow("subfolder/save.sav")]
    [DataRow("CON.sav")]
    [DataRow("trailing.")]
    public void ValidateConfig_WhenStoredNameIsUnsafe_ReturnsValidationError(string storedName)
    {
        var target = new TargetPath
        {
            Id = "save",
            Name = "Save",
            Source = "game.sav",
            Type = TargetPathType.File,
            StoredName = storedName
        };

        var errors = new ConfigProcessor().ValidateConfig(
            CreateInstance(target).Config,
            _instancePath
        );

        Assert.IsTrue(errors.Any(error => error.Contains("invalid stored filename", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ValidateConfig_WhenDirectoryHasStoredName_ReturnsValidationError()
    {
        var target = new TargetPath
        {
            Id = "mods",
            Name = "Mods",
            Source = "mods",
            Type = TargetPathType.Directory,
            StoredName = "renamed"
        };

        var errors = new ConfigProcessor().ValidateConfig(
            CreateInstance(target).Config,
            _instancePath
        );

        Assert.IsTrue(errors.Any(error => error.Contains("only configure StoredName", StringComparison.OrdinalIgnoreCase)));
    }

    #endregion

    #region Test Helpers

    private InstanceContext CreateInstance(TargetPath target)
    {
        return new InstanceContext
        {
            InstancePath = _instancePath,
            ConfigPath = Path.Combine(_instancePath, "instance.json"),
            BackupsPath = _backupsPath,
            Config = new InstanceConfig
            {
                Name = "Stored Name Test",
                Targets = [target]
            }
        };
    }

    #endregion
}
