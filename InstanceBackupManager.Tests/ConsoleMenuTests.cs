using InstanceBackupManager.Console.Menus;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests reusable console-menu validation and redirected-input behavior.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ConsoleMenuTests
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
    }

    [TestCleanup]
    public void TestCleanup()
    {
        System.Console.SetIn(_originalInput);
        System.Console.SetOut(_originalOutput);
    }

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that a redirected shortcut selects its associated value.
    /// </summary>
    [TestMethod]
    public void Select_WhenRedirectedShortcutMatches_ReturnsValue()
    {
        SetConsoleInput("2");

        var result = ConsoleMenu.Select(
            "Test Menu",
            [
                new ConsoleMenuItem<string>("1", "First", "first"),
                new ConsoleMenuItem<string>("2", "Second", "second")
            ]
        );

        Assert.IsFalse(result.IsCancelled);
        Assert.AreEqual("second", result.Value);
    }

    /// <summary>
    /// Verifies that selecting a cancellation item returns a cancelled result.
    /// </summary>
    [TestMethod]
    public void Select_WhenCancellationItemIsSelected_ReturnsCancelledResult()
    {
        SetConsoleInput("0");

        var result = ConsoleMenu.Select(
            "Test Menu",
            [
                new ConsoleMenuItem<string>(
                    "0",
                    "Exit",
                    string.Empty,
                    IsCancellation: true
                )
            ]
        );

        Assert.IsTrue(result.IsCancelled);
        Assert.IsNull(result.Value);
    }

    /// <summary>
    /// Verifies that duplicate shortcuts are rejected without regard to casing.
    /// </summary>
    [TestMethod]
    public void Select_WhenShortcutsAreDuplicated_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ConsoleMenu.Select(
                "Test Menu",
                [
                    new ConsoleMenuItem<string>("A", "First", "first"),
                    new ConsoleMenuItem<string>("a", "Second", "second")
                ]
            )
        );
    }

    /// <summary>
    /// Verifies that a disabled item's shortcut is ignored and its unavailable state is displayed.
    /// </summary>
    [TestMethod]
    public void Select_WhenDisabledShortcutIsEntered_IgnoresShortcutAndDisplaysUnavailableState()
    {
        System.Console.SetIn(
            new StringReader(
                string.Join(
                    Environment.NewLine,
                    "1",
                    "2"
                ) + Environment.NewLine
            )
        );

        var output = new StringWriter();
        System.Console.SetOut(output);

        var result = ConsoleMenu.Select(
            "Test Menu",
            [
                new ConsoleMenuItem<string>(
                    "1",
                    "Disabled item",
                    "disabled",
                    IsEnabled: false
                ),
                new ConsoleMenuItem<string>("2", "Enabled item", "enabled")
            ]
        );

        Assert.AreEqual("enabled", result.Value);
        StringAssert.Contains(output.ToString(), "Disabled item [Unavailable]");
    }

    /// <summary>
    /// Verifies that a menu containing no enabled choices is rejected instead of entering an input loop.
    /// </summary>
    [TestMethod]
    public void Select_WhenEveryItemIsDisabled_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ConsoleMenu.Select(
                "Test Menu",
                [
                    new ConsoleMenuItem<string>(
                        "1",
                        "Disabled item",
                        "disabled",
                        IsEnabled: false
                    )
                ]
            )
        );
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Redirects console input and output for a menu test.
    /// </summary>
    private static void SetConsoleInput(params string[] lines)
    {
        System.Console.SetIn(
            new StringReader(
                string.Join(Environment.NewLine, lines) + Environment.NewLine
            )
        );

        System.Console.SetOut(new StringWriter());
    }

    #endregion
}
