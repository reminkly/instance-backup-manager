using InstanceBackupManager.Processing.Configuration;
using InstanceBackupManager.Processing.Constants;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests discovery, configuration-status detection, and ordering of immediate instance directories.
/// </summary>
[TestClass]
public sealed class InstanceDiscoveryServiceTests
{
    #region Fields

    private string _testRootPath = null!;
    private InstanceDiscoveryService _discoveryService = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Creates an isolated test directory and discovery service before each test.
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
        _discoveryService = new InstanceDiscoveryService();
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

    #region Discovery Tests

    /// <summary>
    /// Verifies that discovering a missing instances directory creates it and returns an empty collection.
    /// </summary>
    [TestMethod]
    public void Discover_WhenInstancesDirectoryDoesNotExist_CreatesDirectoryAndReturnsEmptyCollection()
    {
        var instancesPath = Path.Combine(
            _testRootPath,
            BackupStorageConstants.InstancesDirectoryName
        );

        var instances = _discoveryService.Discover(instancesPath);

        Assert.IsTrue(Directory.Exists(instancesPath));
        Assert.IsEmpty(instances);
    }

    /// <summary>
    /// Verifies that discovery identifies configured and unconfigured instance directories.
    /// </summary>
    [TestMethod]
    public void Discover_WhenConfiguredAndUnconfiguredInstancesExist_ReturnsConfigurationStatus()
    {
        var instancesPath = Path.Combine(
            _testRootPath,
            BackupStorageConstants.InstancesDirectoryName
        );

        var configuredInstancePath = Path.Combine(
            instancesPath,
            "Configured Instance"
        );

        var unconfiguredInstancePath = Path.Combine(
            instancesPath,
            "Unconfigured Instance"
        );

        Directory.CreateDirectory(configuredInstancePath);
        Directory.CreateDirectory(unconfiguredInstancePath);

        File.WriteAllText(
            Path.Combine(
                configuredInstancePath,
                BackupStorageConstants.InstanceConfigurationFileName
            ),
            "{}"
        );

        var instances = _discoveryService.Discover(instancesPath);

        Assert.HasCount(2, instances);

        var configuredInstance = instances.Single(
            instance => instance.Name == "Configured Instance"
        );

        var unconfiguredInstance = instances.Single(
            instance => instance.Name == "Unconfigured Instance"
        );

        Assert.IsTrue(configuredInstance.HasConfiguration);
        Assert.IsFalse(unconfiguredInstance.HasConfiguration);
        Assert.AreEqual(Path.GetFullPath(configuredInstancePath), configuredInstance.InstancePath);
        Assert.AreEqual(Path.GetFullPath(unconfiguredInstancePath), unconfiguredInstance.InstancePath);
    }

    /// <summary>
    /// Verifies that discovered instances are ordered alphabetically without regard to casing.
    /// </summary>
    [TestMethod]
    public void Discover_WhenMultipleInstancesExist_ReturnsInstancesInAlphabeticalOrder()
    {
        var instancesPath = Path.Combine(
            _testRootPath,
            BackupStorageConstants.InstancesDirectoryName
        );

        Directory.CreateDirectory(Path.Combine(instancesPath, "Zulu"));
        Directory.CreateDirectory(Path.Combine(instancesPath, "alpha"));
        Directory.CreateDirectory(Path.Combine(instancesPath, "Bravo"));

        var instanceNames = _discoveryService
            .Discover(instancesPath)
            .Select(instance => instance.Name)
            .ToList();

        CollectionAssert.AreEqual(
            new List<string>
            {
                "alpha",
                "Bravo",
                "Zulu"
            },
            instanceNames
        );
    }

    /// <summary>
    /// Verifies that files placed directly beneath the instances root are not treated as instances.
    /// </summary>
    [TestMethod]
    public void Discover_WhenRootContainsFiles_IgnoresFiles()
    {
        var instancesPath = Path.Combine(
            _testRootPath,
            BackupStorageConstants.InstancesDirectoryName
        );

        Directory.CreateDirectory(instancesPath);
        File.WriteAllText(Path.Combine(instancesPath, "readme.txt"), "Not an instance");

        var instances = _discoveryService.Discover(instancesPath);

        Assert.IsEmpty(instances);
    }

    #endregion
}
