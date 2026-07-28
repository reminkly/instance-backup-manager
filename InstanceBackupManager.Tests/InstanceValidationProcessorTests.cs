using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests non-mutating validation of configured instances and targets.
/// </summary>
[TestClass]
public sealed class InstanceValidationProcessorTests
{
    #region Fields

    private string _testRootPath = null!;

    #endregion

    #region Test Initialization

    [TestInitialize]
    public void TestInitialize()
    {
        _testRootPath = Path.Combine(
            Path.GetTempPath(),
            "InstanceBackupManagerTests",
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(_testRootPath);
    }

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

    #region Tests

    /// <summary>
    /// Verifies that an existing required file produces no validation errors.
    /// </summary>
    [TestMethod]
    public void Validate_WhenRequiredFileExists_ReturnsValidReport()
    {
        var sourcePath = Path.Combine(
            _testRootPath,
            "save.dat"
        );

        File.WriteAllText(
            sourcePath,
            "save"
        );

        var processor = new InstanceValidationProcessor();
        var report = processor.Validate(
            CreateInstance(
                new TargetPath
                {
                    Id = "save",
                    Name = "Save",
                    Source = sourcePath,
                    Type = TargetPathType.File,
                    BackupPath = "saves/save.dat"
                }
            )
        );

        Assert.IsTrue(report.IsValid);
        Assert.AreEqual(0, report.ErrorCount);
    }

    /// <summary>
    /// Verifies that a missing required source is reported as an error.
    /// </summary>
    [TestMethod]
    public void Validate_WhenRequiredFileIsMissing_ReturnsError()
    {
        var processor = new InstanceValidationProcessor();
        var report = processor.Validate(
            CreateInstance(
                new TargetPath
                {
                    Id = "save",
                    Name = "Save",
                    Source = Path.Combine(
                        _testRootPath,
                        "missing.dat"
                    ),
                    Type = TargetPathType.File,
                    BackupPath = "saves/save.dat"
                }
            )
        );

        Assert.IsFalse(report.IsValid);
        Assert.AreEqual(1, report.ErrorCount);
    }

    /// <summary>
    /// Verifies that a missing optional source is reported as a warning rather than an error.
    /// </summary>
    [TestMethod]
    public void Validate_WhenOptionalFileIsMissing_ReturnsWarning()
    {
        var processor = new InstanceValidationProcessor();
        var report = processor.Validate(
            CreateInstance(
                new TargetPath
                {
                    Id = "save",
                    Name = "Save",
                    Required = false,
                    Source = Path.Combine(
                        _testRootPath,
                        "missing.dat"
                    ),
                    Type = TargetPathType.File,
                    BackupPath = "saves/save.dat"
                }
            )
        );

        Assert.IsTrue(report.IsValid);
        Assert.AreEqual(1, report.WarningCount);
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a loaded instance containing one configured target.
    /// </summary>
    private InstanceContext CreateInstance(TargetPath target)
    {
        var instancePath = Path.Combine(
            _testRootPath,
            "Instance"
        );

        var backupsPath = Path.Combine(
            instancePath,
            "backups"
        );

        Directory.CreateDirectory(backupsPath);

        return new InstanceContext
        {
            InstancePath = instancePath,
            ConfigPath = Path.Combine(
                instancePath,
                "instance.json"
            ),
            BackupsPath = backupsPath,
            Config = new InstanceConfig
            {
                Name = "Test Instance",
                Targets =
                [
                    target
                ]
            }
        };
    }

    #endregion
}
