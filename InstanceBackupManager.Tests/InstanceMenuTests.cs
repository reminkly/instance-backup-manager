using InstanceBackupManager.Console.Commands;
using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests command registration, availability filtering, dispatch, and result handling in the configured-instance menu.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class InstanceMenuTests
{
    #region Fields

    private TextReader _originalInput = null!;
    private TextWriter _originalOutput = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Preserves the process console streams before each test.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _originalInput = System.Console.In;
        _originalOutput = System.Console.Out;
    }

    /// <summary>
    /// Restores the process console streams after each test.
    /// </summary>
    [TestCleanup]
    public void TestCleanup()
    {
        System.Console.SetIn(_originalInput);
        System.Console.SetOut(_originalOutput);
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Verifies that a menu cannot be created without any registered commands.
    /// </summary>
    [TestMethod]
    public void Constructor_WhenNoCommandsAreRegistered_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new InstanceMenu([])
        );
    }

    /// <summary>
    /// Verifies that command selections must be unique without regard to casing.
    /// </summary>
    [TestMethod]
    public void Constructor_WhenSelectionsAreDuplicated_ThrowsArgumentException()
    {
        IReadOnlyCollection<IInstanceCommand> commands =
        [
            new TestInstanceCommand("A", "First"),
            new TestInstanceCommand("a", "Second")
        ];

        Assert.ThrowsExactly<ArgumentException>(
            () => new InstanceMenu(commands)
        );
    }

    #endregion

    #region Dispatch Tests

    /// <summary>
    /// Verifies that selecting a registered command executes it and then allows the user to return.
    /// </summary>
    [TestMethod]
    public void Run_WhenAvailableCommandIsSelected_ExecutesCommand()
    {
        var command = new TestInstanceCommand("1", "Test command");
        var menu = new InstanceMenu([command]);

        SetConsoleInput("1", "0");

        var result = menu.Run(CreateInstanceContext());

        Assert.AreEqual(0, result);
        Assert.AreEqual(1, command.ExecutionCount);
    }

    /// <summary>
    /// Verifies that unavailable commands are neither displayed nor executed.
    /// </summary>
    [TestMethod]
    public void Run_WhenCommandIsUnavailable_HidesAndDoesNotExecuteCommand()
    {
        var command = new TestInstanceCommand(
            selection: "1",
            description: "Hidden command",
            isAvailable: false
        );

        var menu = new InstanceMenu([command]);
        var output = SetConsoleInput("0");

        var result = menu.Run(CreateInstanceContext());

        Assert.AreEqual(0, result);
        Assert.AreEqual(0, command.ExecutionCount);
        Assert.IsFalse(
            output
                .ToString()
                .Contains(
                    "Hidden command",
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>
    /// Verifies that a nonzero command result is returned immediately by the menu.
    /// </summary>
    [TestMethod]
    public void Run_WhenCommandFails_ReturnsCommandResult()
    {
        var command = new TestInstanceCommand(
            selection: "1",
            description: "Failing command",
            result: 7
        );

        var menu = new InstanceMenu([command]);

        SetConsoleInput("1");

        var result = menu.Run(CreateInstanceContext());

        Assert.AreEqual(7, result);
        Assert.AreEqual(1, command.ExecutionCount);
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Redirects console input and output for one menu test.
    /// </summary>
    /// <param name="lines">The input lines returned to the menu.</param>
    /// <returns>The writer capturing console output.</returns>
    private static StringWriter SetConsoleInput(params string[] lines)
    {
        var input = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        var output = new StringWriter();

        System.Console.SetIn(new StringReader(input));
        System.Console.SetOut(output);

        return output;
    }

    /// <summary>
    /// Creates a minimal enabled instance context for menu tests.
    /// </summary>
    /// <returns>A runtime instance context.</returns>
    private static InstanceContext CreateInstanceContext()
    {
        return new InstanceContext
        {
            InstancePath = Path.GetTempPath(),
            ConfigPath = Path.Combine(Path.GetTempPath(), "instance.json"),
            BackupsPath = Path.Combine(Path.GetTempPath(), "backups"),
            Config = new InstanceConfig
            {
                Name = "Test Instance",
                Enabled = true,
                Targets = []
            }
        };
    }

    #endregion

    #region Test Types

    /// <summary>
    /// Provides a configurable command implementation for menu tests.
    /// </summary>
    private sealed class TestInstanceCommand : IInstanceCommand
    {
        #region Properties

        /// <inheritdoc/>
        public string Selection { get; }

        /// <inheritdoc/>
        public string Description { get; }

        /// <summary>
        /// Gets the number of times the command has been executed.
        /// </summary>
        internal int ExecutionCount { get; private set; }

        /// <summary>
        /// Gets the availability result returned by the command.
        /// </summary>
        private bool Availability { get; }

        /// <summary>
        /// Gets the result returned when the command executes.
        /// </summary>
        private int Result { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a configurable test command.
        /// </summary>
        internal TestInstanceCommand(
            string selection,
            string description,
            bool isAvailable = true,
            int result = 0
        )
        {
            Selection = selection;
            Description = description;
            Availability = isAvailable;
            Result = result;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc/>
        public bool IsAvailable(InstanceContext instance)
        {
            ArgumentNullException.ThrowIfNull(instance);

            return Availability;
        }

        /// <inheritdoc/>
        public int Execute(InstanceContext instance)
        {
            ArgumentNullException.ThrowIfNull(instance);

            ExecutionCount++;

            return Result;
        }

        #endregion
    }

    #endregion
}
