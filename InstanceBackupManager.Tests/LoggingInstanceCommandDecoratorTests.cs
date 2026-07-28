using InstanceBackupManager.Console.Commands;
using InstanceBackupManager.Console.Logging;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests logging added around instance commands by the command decorator.
/// </summary>
[TestClass]
public sealed class LoggingInstanceCommandDecoratorTests
{
    #region Tests

    /// <summary>
    /// Verifies that successful command execution records start and completion messages.
    /// </summary>
    [TestMethod]
    public void Execute_WhenCommandSucceeds_LogsStartAndCompletion()
    {
        var logger = new RecordingLogger();
        var decorator = new LoggingInstanceCommandDecorator(
            new StubCommand(result: 0),
            logger
        );

        var result = decorator.Execute(CreateInstance());

        Assert.AreEqual(0, result);
        Assert.HasCount(2, logger.InformationMessages);
        Assert.IsEmpty(logger.WarningMessages);
        Assert.IsEmpty(logger.Errors);
    }

    /// <summary>
    /// Verifies that a nonzero command result is recorded as a warning.
    /// </summary>
    [TestMethod]
    public void Execute_WhenCommandReturnsFailure_LogsWarning()
    {
        var logger = new RecordingLogger();
        var decorator = new LoggingInstanceCommandDecorator(
            new StubCommand(result: 1),
            logger
        );

        var result = decorator.Execute(CreateInstance());

        Assert.AreEqual(1, result);
        Assert.HasCount(1, logger.InformationMessages);
        Assert.HasCount(1, logger.WarningMessages);
        Assert.IsEmpty(logger.Errors);
    }

    /// <summary>
    /// Verifies that an unexpected command exception is logged and rethrown.
    /// </summary>
    [TestMethod]
    public void Execute_WhenCommandThrows_LogsAndRethrowsException()
    {
        var expectedException = new IOException("Test failure.");
        var logger = new RecordingLogger();
        var decorator = new LoggingInstanceCommandDecorator(
            new StubCommand(expectedException),
            logger
        );

        var actualException = Assert.ThrowsExactly<IOException>(
            () => decorator.Execute(CreateInstance())
        );

        Assert.AreSame(expectedException, actualException);
        Assert.HasCount(1, logger.Errors);
        Assert.AreSame(expectedException, logger.Errors[0].Exception);
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a minimal loaded instance for command tests.
    /// </summary>
    private static InstanceContext CreateInstance()
    {
        return new InstanceContext
        {
            InstancePath = Path.GetTempPath(),
            ConfigPath = Path.Combine(
                Path.GetTempPath(),
                "instance.json"
            ),
            BackupsPath = Path.Combine(
                Path.GetTempPath(),
                "backups"
            ),
            Config = new InstanceConfig
            {
                Name = "Test Instance"
            }
        };
    }

    #endregion

    #region Test Types

    /// <summary>
    /// Provides controllable command behavior for decorator tests.
    /// </summary>
    private sealed class StubCommand : IInstanceCommand
    {
        private int Result { get; }

        private Exception? Exception { get; }

        public string Selection => "1";

        public string Description => "Test command";

        internal StubCommand(int result)
        {
            Result = result;
        }

        internal StubCommand(Exception exception)
        {
            Exception = exception;
        }

        public bool IsAvailable(InstanceContext instance)
        {
            return true;
        }

        public int Execute(InstanceContext instance)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            return Result;
        }
    }

    /// <summary>
    /// Stores logger calls in memory for assertions.
    /// </summary>
    private sealed class RecordingLogger : IApplicationLogger
    {
        internal List<string> InformationMessages { get; } = [];

        internal List<string> WarningMessages { get; } = [];

        internal List<RecordedError> Errors { get; } = [];

        public void LogInformation(string message)
        {
            InformationMessages.Add(message);
        }

        public void LogWarning(string message)
        {
            WarningMessages.Add(message);
        }

        public void LogError(
            string message,
            Exception? exception = null
        )
        {
            Errors.Add(
                new RecordedError(
                    message,
                    exception
                )
            );
        }
    }

    private sealed record RecordedError(
        string Message,
        Exception? Exception
    );

    #endregion
}
