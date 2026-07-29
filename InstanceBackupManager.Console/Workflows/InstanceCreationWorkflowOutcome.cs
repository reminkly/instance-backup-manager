namespace InstanceBackupManager.Console.Workflows;

/// <summary>
/// Describes how the application should proceed after the new-instance workflow finishes.
/// </summary>
internal enum InstanceCreationWorkflowOutcome
{
    /// <summary>
    /// The user cancelled creation and should return to instance selection.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The instance was created successfully and the application should exit so its configuration can be edited.
    /// </summary>
    Created,

    /// <summary>
    /// Creation failed and the user should return to instance selection after reviewing the error.
    /// </summary>
    Failed
}
