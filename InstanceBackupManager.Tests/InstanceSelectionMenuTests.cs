using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests redirected selection of existing instances, creation, update checks, and exit from the application-level menu.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class InstanceSelectionMenuTests
{
    #region Fields

    private TextReader _originalInput = null!;
    private TextWriter _originalOutput = null!;

    #endregion

    #region Test Initialization

    [TestInitialize]
    public void TestInitialize()
    {
        _originalInput = System.Console.In;
        _originalOutput = System.Console.Out;
        System.Console.SetOut(new StringWriter());
    }

    [TestCleanup]
    public void TestCleanup()
    {
        System.Console.SetIn(_originalInput);
        System.Console.SetOut(_originalOutput);
    }

    #endregion

    #region Selection Tests

    /// <summary>
    /// Verifies that creation remains available when no instance directories have been discovered.
    /// </summary>
    [TestMethod]
    public void Select_WhenNoInstancesExist_AllowsCreation()
    {
        SetConsoleInput("n");

        var result = InstanceSelectionMenu.Select(Array.Empty<InstanceDescriptor>());

        Assert.IsFalse(result.IsCancelled);
        Assert.AreEqual(ApplicationMenuAction.CreateInstance, result.Value!.Action);
        Assert.IsNull(result.Value.Instance);
    }

    /// <summary>
    /// Verifies that the update shortcut returns an explicit update-check action.
    /// </summary>
    [TestMethod]
    public void Select_WhenUpdateIsSelected_ReturnsUpdateAction()
    {
        SetConsoleInput("u");

        var result = InstanceSelectionMenu.Select(Array.Empty<InstanceDescriptor>());

        Assert.IsFalse(result.IsCancelled);
        Assert.AreEqual(ApplicationMenuAction.CheckForUpdates, result.Value!.Action);
        Assert.IsNull(result.Value.Instance);
    }

    /// <summary>
    /// Verifies that the numeric shortcut returns an open-instance action containing the selected instance.
    /// </summary>
    [TestMethod]
    public void Select_WhenExistingInstanceIsSelected_ReturnsInstanceAction()
    {
        var instance = new InstanceDescriptor
        {
            Name = "Test Instance",
            InstancePath = Path.GetFullPath("Test Instance"),
            HasConfiguration = true
        };

        SetConsoleInput("1");

        var result = InstanceSelectionMenu.Select(
            new List<InstanceDescriptor>
            {
                instance
            }.AsReadOnly()
        );

        Assert.IsFalse(result.IsCancelled);
        Assert.AreEqual(ApplicationMenuAction.OpenInstance, result.Value!.Action);
        Assert.AreSame(instance, result.Value.Instance);
    }

    /// <summary>
    /// Verifies that the zero shortcut exits even when no instances exist.
    /// </summary>
    [TestMethod]
    public void Select_WhenExitIsSelected_ReturnsCancelledResult()
    {
        SetConsoleInput("0");

        var result = InstanceSelectionMenu.Select(Array.Empty<InstanceDescriptor>());

        Assert.IsTrue(result.IsCancelled);
        Assert.IsNull(result.Value);
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Replaces console input with the supplied redirected menu selections.
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
