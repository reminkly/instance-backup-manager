using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Constants;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests coordination of instance discovery, configuration loading, validation, and runtime-context creation through the configuration facade.
/// </summary>
[TestClass]
public sealed class ConfigProcessorTests
{
    #region Fields

    private string _testRootPath = null!;
    private string _instancePath = null!;
    private ConfigProcessor _processor = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Creates an isolated temporary directory and configuration processor before each test.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _testRootPath = Path.Combine(
            Path.GetTempPath(),
            "InstanceBackupManagerTests",
            Guid.NewGuid().ToString("N")
        );

        _instancePath = Path.Combine(
            _testRootPath,
            "Test Instance"
        );

        Directory.CreateDirectory(_testRootPath);

        _processor = new ConfigProcessor();
    }

    /// <summary>
    /// Removes the isolated temporary directory after each test.
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

    #region LoadInstances Tests

    /// <summary>
    /// Verifies that loading a missing instances directory creates it and returns an empty collection.
    /// </summary>
    [TestMethod]
    public void LoadInstances_WhenInstancesDirectoryDoesNotExist_CreatesDirectoryAndReturnsEmptyCollection()
    {
        var instancesPath = Path.Combine(
            _testRootPath,
            BackupStorageConstants.InstancesDirectoryName
        );

        var instances = _processor.LoadInstances(instancesPath);

        Assert.IsTrue(Directory.Exists(instancesPath));
        Assert.IsEmpty(instances);
    }

    /// <summary>
    /// Verifies that instance directories without configuration files are not returned as loaded instances.
    /// </summary>
    [TestMethod]
    public void LoadInstances_WhenInstanceHasNoConfiguration_SkipsInstance()
    {
        var instancesPath = Path.Combine(
            _testRootPath,
            BackupStorageConstants.InstancesDirectoryName
        );

        var unconfiguredInstancePath = Path.Combine(
            instancesPath,
            "Unconfigured Instance"
        );

        Directory.CreateDirectory(unconfiguredInstancePath);

        var instances = _processor.LoadInstances(instancesPath);

        Assert.IsEmpty(instances);
    }

    /// <summary>
    /// Verifies that configured instance directories are loaded and returned as runtime contexts.
    /// </summary>
    [TestMethod]
    public void LoadInstances_WhenInstanceIsConfigured_ReturnsInstance()
    {
        var instancesPath = Path.Combine(
            _testRootPath,
            BackupStorageConstants.InstancesDirectoryName
        );

        var configuredInstancePath = Path.Combine(
            instancesPath,
            "Configured Instance"
        );

        _processor.CreateSkeletonConfig(configuredInstancePath);

        var instances = _processor.LoadInstances(instancesPath);

        Assert.HasCount(1, instances);

        var instance = instances.Single();

        Assert.AreEqual("Configured Instance", instance.Config.Name);
        Assert.AreEqual(Path.GetFullPath(configuredInstancePath), instance.InstancePath);
    }

    #endregion

    #region LoadInstance Tests

    /// <summary>
    /// Verifies that loading an instance creates its backups directory when it does not already exist.
    /// </summary>
    [TestMethod]
    public void LoadInstance_WhenBackupsDirectoryDoesNotExist_CreatesBackupsDirectory()
    {
        _processor.CreateSkeletonConfig(_instancePath);

        var instance = _processor.LoadInstance(_instancePath);

        Assert.IsTrue(Directory.Exists(instance.BackupsPath));

        Assert.AreEqual(
            Path.Combine(
                Path.GetFullPath(_instancePath),
                BackupStorageConstants.BackupsDirectoryName
            ),
            instance.BackupsPath
        );
    }

    /// <summary>
    /// Verifies that loading a valid instance returns its expected configuration and normalized runtime paths.
    /// </summary>
    [TestMethod]
    public void LoadInstance_WhenConfigurationIsValid_ReturnsExpectedContext()
    {
        _processor.CreateSkeletonConfig(_instancePath);

        var instance = _processor.LoadInstance(_instancePath);
        var fullInstancePath = Path.GetFullPath(_instancePath);

        Assert.AreEqual(fullInstancePath, instance.InstancePath);

        Assert.AreEqual(
            Path.Combine(
                fullInstancePath,
                BackupStorageConstants.InstanceConfigurationFileName
            ),
            instance.ConfigPath
        );

        Assert.AreEqual(
            Path.Combine(
                fullInstancePath,
                BackupStorageConstants.BackupsDirectoryName
            ),
            instance.BackupsPath
        );

        Assert.AreEqual("Test Instance", instance.Config.Name);
    }

    /// <summary>
    /// Verifies that loading an instance without a configuration file throws a file-not-found exception.
    /// </summary>
    [TestMethod]
    public void LoadInstance_WhenConfigurationDoesNotExist_ThrowsFileNotFoundException()
    {
        Assert.ThrowsExactly<FileNotFoundException>(
            () => _processor.LoadInstance(_instancePath)
        );
    }

    /// <summary>
    /// Verifies that loading an instance validates its configuration before creating a runtime context.
    /// </summary>
    [TestMethod]
    public void LoadInstance_WhenConfigurationIsInvalid_ThrowsInvalidDataException()
    {
        Directory.CreateDirectory(_instancePath);

        var configPath = Path.Combine(
            _instancePath,
            BackupStorageConstants.InstanceConfigurationFileName
        );

        File.WriteAllText(
            configPath,
            """
            {
              "schemaVersion": 2,
              "name": "",
              "enabled": true,
              "targets": []
            }
            """
        );

        Assert.ThrowsExactly<InvalidDataException>(
            () => _processor.LoadInstance(_instancePath)
        );
    }

    #endregion

    #region Skeleton Facade Tests

    /// <summary>
    /// Verifies that the facade creates a skeleton configuration that can be loaded as a valid instance.
    /// </summary>
    [TestMethod]
    public void CreateSkeletonConfig_WhenCalled_CreatesLoadableInstance()
    {
        _processor.CreateSkeletonConfig(_instancePath);

        var instance = _processor.LoadInstance(_instancePath);

        Assert.AreEqual("Test Instance", instance.Config.Name);
        Assert.AreEqual(BackupStorageConstants.SupportedInstanceConfigurationSchemaVersion, instance.Config.SchemaVersion);
        Assert.HasCount(1, instance.Config.Targets);
        Assert.IsFalse(instance.Config.Targets.Single().Enabled);
    }

    #endregion
}