namespace InstanceBackupManager.Console.Logging;

/// <summary>
/// Defines best-effort application logging operations.
/// </summary>
internal interface IApplicationLogger
{
    #region Methods

    /// <summary>
    /// Records an informational application event.
    /// </summary>
    /// <param name="message">The event message.</param>
    void LogInformation(string message);

    /// <summary>
    /// Records a warning application event.
    /// </summary>
    /// <param name="message">The warning message.</param>
    void LogWarning(string message);

    /// <summary>
    /// Records an application error.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The optional exception associated with the error.</param>
    void LogError(
        string message,
        Exception? exception = null
    );

    #endregion
}
