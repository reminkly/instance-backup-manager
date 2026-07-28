namespace InstanceBackupManager.Processing.Enums;

/// <summary>
/// Identifies the severity of an instance-validation finding.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Indicates that a validation check completed successfully.
    /// </summary>
    Success = 0,

    /// <summary>
    /// Indicates a condition that does not prevent operations but may require attention.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Indicates a condition that can prevent a safe operation.
    /// </summary>
    Error = 2
}
