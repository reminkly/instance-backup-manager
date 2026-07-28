using System.Globalization;

namespace InstanceBackupManager.Console.Logging;

/// <summary>
/// Writes best-effort application events to daily text log files.
/// </summary>
internal sealed class FileApplicationLogger : IApplicationLogger
{
    #region Fields

    private readonly object _writeLock = new();

    #endregion

    #region Properties

    /// <summary>
    /// Gets the directory containing application log files.
    /// </summary>
    private string LogsPath { get; }

    /// <summary>
    /// Gets the time provider used to timestamp log entries.
    /// </summary>
    private TimeProvider TimeProvider { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a file logger using system time.
    /// </summary>
    /// <param name="logsPath">The directory that will contain application logs.</param>
    internal FileApplicationLogger(string logsPath)
        : this(
            logsPath,
            TimeProvider.System
        )
    {
    }

    /// <summary>
    /// Initializes a file logger using the specified time provider.
    /// </summary>
    /// <param name="logsPath">The directory that will contain application logs.</param>
    /// <param name="timeProvider">The time provider used to timestamp entries.</param>
    internal FileApplicationLogger(
        string logsPath,
        TimeProvider timeProvider
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsPath);
        ArgumentNullException.ThrowIfNull(timeProvider);

        LogsPath = Path.GetFullPath(logsPath);
        TimeProvider = timeProvider;
    }

    #endregion

    #region Public Methods

    /// <inheritdoc/>
    public void LogInformation(string message)
    {
        Write(
            "INFORMATION",
            message,
            exception: null
        );
    }

    /// <inheritdoc/>
    public void LogWarning(string message)
    {
        Write(
            "WARNING",
            message,
            exception: null
        );
    }

    /// <inheritdoc/>
    public void LogError(
        string message,
        Exception? exception = null
    )
    {
        Write(
            "ERROR",
            message,
            exception
        );
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Writes one log entry without allowing logging failures to interrupt the application.
    /// </summary>
    /// <param name="level">The event severity label.</param>
    /// <param name="message">The event message.</param>
    /// <param name="exception">The optional associated exception.</param>
    private void Write(
        string level,
        string message,
        Exception? exception
    )
    {
        try
        {
            var timestamp = TimeProvider.GetUtcNow();
            var logPath = Path.Combine(
                LogsPath,
                $"instance-backup-manager-{timestamp:yyyy-MM-dd}.log"
            );

            var entry = string.Create(
                CultureInfo.InvariantCulture,
                $"{timestamp:O} [{level}] {message}{Environment.NewLine}"
            );

            if (exception is not null)
            {
                entry += exception + Environment.NewLine;
            }

            lock (_writeLock)
            {
                Directory.CreateDirectory(LogsPath);
                File.AppendAllText(
                    logPath,
                    entry
                );
            }
        }
        catch
        {
            // Logging is deliberately best-effort and must never prevent the requested application operation.
        }
    }

    #endregion
}
