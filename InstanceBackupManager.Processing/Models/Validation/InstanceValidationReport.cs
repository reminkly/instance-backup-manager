using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.Validation;

/// <summary>
/// Contains the complete non-mutating validation result for one loaded instance.
/// </summary>
public sealed class InstanceValidationReport
{
    #region Properties

    /// <summary>
    /// Gets the name of the validated instance.
    /// </summary>
    public required string InstanceName { get; init; }

    /// <summary>
    /// Gets the findings produced by validation.
    /// </summary>
    public required IReadOnlyCollection<ValidationFinding> Findings { get; init; }

    /// <summary>
    /// Gets the number of warning findings.
    /// </summary>
    public int WarningCount => Findings.Count(
        finding => finding.Severity == ValidationSeverity.Warning
    );

    /// <summary>
    /// Gets the number of error findings.
    /// </summary>
    public int ErrorCount => Findings.Count(
        finding => finding.Severity == ValidationSeverity.Error
    );

    /// <summary>
    /// Gets a value indicating whether validation completed without errors.
    /// </summary>
    public bool IsValid => ErrorCount == 0;

    #endregion
}
