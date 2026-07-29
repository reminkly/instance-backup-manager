using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Exceptions;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests schema-version-2 backup-root resolution and targeted rejection of older instance configurations.
/// </summary>
[TestClass]
public sealed class SchemaVersion2ConfigurationTests
{
    #region Fields

    private string _testRootPath = null!;
    private string _instancePath = null!;
    private string _configPath = null!;
    private ConfigProcessor _processor = null!;

    #endregion

    #region Test Initialization

    [TestInitialize]
    public void TestInitialize()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), "InstanceBackupManagerTests", Guid.NewGuid().ToString("N"));
        _instancePath = Path.Combine(_testRootPath, "Test Instance");
        _configPath = Path.Combine(_instancePath, "instance.json");
        Directory.CreateDirectory(_instancePath);
        _processor = new ConfigProcessor();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, recursive: true);
        }
    }

    #endregion

    #region Schema Tests

    /// <summary>
    /// Verifies that a version-1 configuration produces the targeted update exception before normal validation.
    /// </summary>
    [TestMethod]
    public void LoadInstance_WhenSchemaVersionIsOne_ThrowsTargetedSchemaException()
    {
        File.WriteAllText(
            _configPath,
            """
            {
              "SchemaVersion": 1,
              "Name": "Old Instance",
              "Targets": []
            }
            """
        );

        var exception = Assert.ThrowsExactly<UnsupportedInstanceConfigurationSchemaException>(
            () => _processor.LoadInstance(_instancePath)
        );

        Assert.AreEqual(1, exception.ConfiguredVersion);
        Assert.AreEqual(2, exception.SupportedVersion);
        Assert.AreEqual(_configPath, exception.ConfigPath);
    }

    /// <summary>
    /// Verifies that an absolute backup root is used as the runtime storage location.
    /// </summary>
    [TestMethod]
    public void LoadInstance_WhenBackupRootIsAbsolute_UsesExternalStoragePath()
    {
        var externalRoot = Path.Combine(_testRootPath, "External Backups");
        var json = $$"""
        {
          "SchemaVersion": 2,
          "Name": "External Instance",
          "BackupRoot": "{{externalRoot.Replace('\\', '/')}}",
          "Targets": []
        }
        """;

        File.WriteAllText(_configPath, json);

        var instance = _processor.LoadInstance(_instancePath);

        Assert.AreEqual(Path.GetFullPath(externalRoot), instance.BackupsPath);
        Assert.IsTrue(Directory.Exists(externalRoot));
    }

    /// <summary>
    /// Verifies that the registered migration upgrades version 1, preserves configured values, and saves the unchanged original.
    /// </summary>
    [TestMethod]
    public void UpgradeConfig_WhenSchemaVersionIsOne_MigratesAndPreservesOriginal()
    {
        const string originalJson = """
            {
              "SchemaVersion": 1,
              "Name": "Migrated Instance",
              "Enabled": true,
              "Retention": {
                "ManualBackupsToKeep": 7,
                "PreRestoreBackupsToKeep": 3
              },
              "Targets": [
                {
                  "Id": "save-ram",
                  "Name": "Save RAM",
                  "Enabled": false,
                  "Required": true,
                  "AllowClear": false,
                  "Source": "game.sav",
                  "Type": "file",
                  "BackupPath": "saves/game.sav"
                }
              ]
            }
            """;

        File.WriteAllText(
            _configPath,
            originalJson
        );

        var result = _processor.UpgradeConfig(_configPath);
        var upgradedConfig = _processor.LoadConfig(_configPath);
        var upgradedJson = File.ReadAllText(_configPath);

        Assert.AreEqual(1, result.PreviousVersion);
        Assert.AreEqual(2, result.CurrentVersion);
        Assert.AreEqual(2, upgradedConfig.SchemaVersion);
        Assert.AreEqual("backups", upgradedConfig.BackupRoot);
        Assert.AreEqual("Migrated Instance", upgradedConfig.Name);
        Assert.AreEqual(7, upgradedConfig.Retention?.ManualBackupsToKeep);
        Assert.AreEqual("game.sav", upgradedConfig.Targets.Single().Source);
        Assert.IsFalse(upgradedConfig.Targets.Single().Enabled);
        Assert.IsFalse(upgradedJson.Contains("\"BackupPath\"", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(originalJson, File.ReadAllText(result.BackupPath));
    }

    /// <summary>
    /// Verifies that an existing migration backup is never overwritten by a later upgrade attempt.
    /// </summary>
    [TestMethod]
    public void UpgradeConfig_WhenBackupNameAlreadyExists_UsesNumberedBackupName()
    {
        File.WriteAllText(
            _configPath,
            """
            {
              "SchemaVersion": 1,
              "Name": "Old Instance",
              "Targets": []
            }
            """
        );

        var existingBackupPath = Path.Combine(
            _instancePath,
            "instance.schema-v1.backup.json"
        );

        File.WriteAllText(
            existingBackupPath,
            "do not overwrite"
        );

        var result = _processor.UpgradeConfig(_configPath);

        Assert.AreEqual("do not overwrite", File.ReadAllText(existingBackupPath));
        Assert.AreEqual(
            Path.Combine(_instancePath, "instance.schema-v1.backup-2.json"),
            result.BackupPath
        );
        Assert.IsTrue(File.Exists(result.BackupPath));
    }

    /// <summary>
    /// Verifies that configurations without a registered path are rejected without changing the active file.
    /// </summary>
    [TestMethod]
    public void UpgradeConfig_WhenNoMigrationPathExists_LeavesConfigurationUnchanged()
    {
        const string originalJson = """
            {
              "SchemaVersion": 0,
              "Name": "Unsupported Instance",
              "Targets": []
            }
            """;

        File.WriteAllText(
            _configPath,
            originalJson
        );

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _processor.UpgradeConfig(_configPath)
        );

        Assert.AreEqual(originalJson, File.ReadAllText(_configPath));
        Assert.IsEmpty(
            Directory.GetFiles(
                _instancePath,
                "*.backup*.json"
            )
        );
    }

    #endregion
}
