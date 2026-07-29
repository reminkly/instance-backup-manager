using InstanceBackupManager.Console.Workflows;
using InstanceBackupManager.Processing;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests successful, cancelled, and failed interactive instance-creation outcomes.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class InstanceCreationWorkflowTests
{
    #region Fields

    private TextReader _originalInput = null!;
    private TextWriter _originalOutput = null!;
    private StringWriter _output = null!;
    private string _testRootPath = null!;
    private string _instancesPath = null!;
    private InstanceCreationWorkflow _workflow = null!;

    #endregion

    #region Test Initialization

    [TestInitialize]
    public void TestInitialize()
    {
        _originalInput = System.Console.In;
        _originalOutput = System.Console.Out;
        _output = new StringWriter();
        _testRootPath = Path.Combine(
            Path.GetTempPath(),
            "InstanceBackupManagerTests",
            Guid.NewGuid().ToString("N")
        );

        _instancesPath = Path.Combine(_testRootPath, "Instances");
        _workflow = new InstanceCreationWorkflow(
            new InstanceCreationProcessor(
                new ConfigProcessor()
            )
        );

        System.Console.SetOut(_output);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        System.Console.SetIn(_originalInput);
        System.Console.SetOut(_originalOutput);

        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(
                _testRootPath,
                recursive: true
            );
        }
    }

    #endregion

    #region Outcome Tests

    /// <summary>
    /// Verifies that successful creation requests application exit and tells the user to edit the generated configuration.
    /// </summary>
    [TestMethod]
    public void Run_WhenCreationSucceeds_ReturnsCreatedAndDisplaysRestartInstructions()
    {
        SetConsoleInput(
            "Test Instance",
            string.Empty,
            "y"
        );

        var outcome = _workflow.Run(_instancesPath);

        Assert.AreEqual(InstanceCreationWorkflowOutcome.Created, outcome);
        StringAssert.Contains(_output.ToString(), "Update instance.json before restarting Instance Backup Manager.");
        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    _instancesPath,
                    "Test Instance",
                    "instance.json"
                )
            )
        );
    }

    /// <summary>
    /// Verifies that an empty display name cancels creation and returns to instance selection.
    /// </summary>
    [TestMethod]
    public void Run_WhenNameIsBlank_ReturnsCancelled()
    {
        SetConsoleInput(string.Empty);

        var outcome = _workflow.Run(_instancesPath);

        Assert.AreEqual(InstanceCreationWorkflowOutcome.Cancelled, outcome);
        Assert.IsFalse(Directory.Exists(_instancesPath));
    }

    /// <summary>
    /// Verifies that a recoverable creation error returns to instance selection rather than exiting the application.
    /// </summary>
    [TestMethod]
    public void Run_WhenCreationFails_ReturnsFailed()
    {
        Directory.CreateDirectory(
            Path.Combine(_instancesPath, "Existing")
        );

        SetConsoleInput(
            "Existing",
            string.Empty,
            "y"
        );

        var outcome = _workflow.Run(_instancesPath);

        Assert.AreEqual(InstanceCreationWorkflowOutcome.Failed, outcome);
        StringAssert.Contains(_output.ToString(), "The instance could not be created.");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Replaces console input with the supplied workflow responses.
    /// </summary>
    private static void SetConsoleInput(params string[] lines)
    {
        System.Console.SetIn(
            new StringReader(
                string.Join(
                    Environment.NewLine,
                    lines
                )
            )
        );
    }

    #endregion
}
