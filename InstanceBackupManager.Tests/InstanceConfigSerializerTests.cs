using InstanceBackupManager.Processing.Configuration;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests reading, deserializing, creating, and serializing instance configuration files.
/// </summary>
[TestClass]
public sealed class InstanceConfigSerializerTests
{
    #region Fields

    private string _testRootPath = null!;
    private InstanceConfigSerializer _serializer = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Creates an isolated test directory and serializer before each test.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _testRootPath = Path.Combine(
            Path.GetTempPath(),
            "InstanceBackupManagerTests",
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(_testRootPath);
        _serializer = new InstanceConfigSerializer();
    }

    /// <summary>
    /// Removes the isolated test directory after each test.
    /// </summary>
    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(
                _testRootPath,
                recursive: true
            );
        }
    }

    #endregion

    #region Load Tests

    /// <summary>
    /// Verifies that loading a missing configuration throws a file-not-found exception.
    /// </summary>
    [TestMethod]
    public void Load_WhenConfigurationDoesNotExist_ThrowsFileNotFoundException()
    {
        var configPath = Path.Combine(_testRootPath, "instance.json");

        Assert.ThrowsExactly<FileNotFoundException>(
            () => _serializer.Load(configPath)
        );
    }

    /// <summary>
    /// Verifies that a JSON null value is rejected as an empty configuration.
    /// </summary>
    [TestMethod]
    public void Load_WhenConfigurationContainsNull_ThrowsInvalidDataException()
    {
        var configPath = Path.Combine(_testRootPath, "instance.json");

        File.WriteAllText(configPath, "null");

        Assert.ThrowsExactly<InvalidDataException>(
            () => _serializer.Load(configPath)
        );
    }

    /// <summary>
    /// Verifies that property casing, comments, trailing commas, and string enum values are supported.
    /// </summary>
    [TestMethod]
    public void Load_WhenJsonUsesSupportedEditingFeatures_ReturnsConfiguration()
    {
        var configPath = Path.Combine(_testRootPath, "instance.json");

        File.WriteAllText(
            configPath,
            """
            {
              // User-authored comment
              "schemaVersion": 1,
              "name": "Test Instance",
              "enabled": true,
              "targets": [
                {
                  "id": "save",
                  "name": "Save File",
                  "enabled": true,
                  "required": true,
                  "allowClear": false,
                  "source": "save.dat",
                  "type": "file",
                  "backupPath": "save/save.dat",
                },
              ],
            }
            """
        );

        var config = _serializer.Load(configPath);
        var target = config.Targets.Single();

        Assert.AreEqual("Test Instance", config.Name);
        Assert.AreEqual(TargetPathType.File, target.Type);
        Assert.AreEqual("save/save.dat", target.BackupPath);
    }

    #endregion

    #region Create Tests

    /// <summary>
    /// Verifies that creating a configuration also creates its missing parent directory.
    /// </summary>
    [TestMethod]
    public void Create_WhenParentDirectoryDoesNotExist_CreatesDirectoryAndFile()
    {
        var configPath = Path.Combine(
            _testRootPath,
            "New Instance",
            "instance.json"
        );

        _serializer.Create(
            configPath,
            CreateConfiguration()
        );

        Assert.IsTrue(File.Exists(configPath));
    }

    /// <summary>
    /// Verifies that a created configuration can be loaded without losing its values.
    /// </summary>
    [TestMethod]
    public void Create_WhenSuccessful_WritesLoadableConfiguration()
    {
        var configPath = Path.Combine(_testRootPath, "instance.json");
        var expected = CreateConfiguration();

        _serializer.Create(
            configPath,
            expected
        );

        var actual = _serializer.Load(configPath);
        var target = actual.Targets.Single();

        Assert.AreEqual(expected.Name, actual.Name);
        Assert.AreEqual(TargetPathType.File, target.Type);
        Assert.AreEqual("save/save.dat", target.BackupPath);
    }

    /// <summary>
    /// Verifies that enum values are written as readable strings rather than numeric values.
    /// </summary>
    [TestMethod]
    public void Create_WhenSuccessful_WritesEnumAsString()
    {
        var configPath = Path.Combine(_testRootPath, "instance.json");

        _serializer.Create(
            configPath,
            CreateConfiguration()
        );

        var json = File.ReadAllText(configPath);

        StringAssert.Contains(json, "\"Type\": \"file\"");
    }

    /// <summary>
    /// Verifies that creating a configuration never overwrites an existing file.
    /// </summary>
    [TestMethod]
    public void Create_WhenConfigurationExists_ThrowsIOExceptionAndPreservesFile()
    {
        var configPath = Path.Combine(_testRootPath, "instance.json");

        File.WriteAllText(configPath, "Original content");

        Assert.ThrowsExactly<IOException>(
            () => _serializer.Create(
                configPath,
                CreateConfiguration()
            )
        );

        Assert.AreEqual("Original content", File.ReadAllText(configPath));
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a configuration suitable for serializer tests.
    /// </summary>
    /// <returns>A populated instance configuration.</returns>
    private static InstanceConfig CreateConfiguration()
    {
        return new InstanceConfig
        {
            Name = "Test Instance",
            Targets =
            [
                new TargetPath
                {
                    Id = "save",
                    Name = "Save File",
                    Enabled = true,
                    Required = true,
                    AllowClear = false,
                    Source = "save.dat",
                    Type = TargetPathType.File,
                    BackupPath = "save/save.dat"
                }
            ]
        };
    }

    #endregion
}
