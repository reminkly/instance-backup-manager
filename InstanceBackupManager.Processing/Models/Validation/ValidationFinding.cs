using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.Validation;

/// <summary>
/// Describes one successful, warning, or error result produced while validating an instance.
/// </summary>
public sealed class ValidationFinding
{
    #region Properties

    /// <summary>
    /// Gets the section to which the finding belongs.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets the severity of the finding.
    /// </summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>
    /// Gets the user-facing finding description.
    /// </summary>
    public required string Message { get; init; }

    #endregion
}
