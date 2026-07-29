namespace InstanceBackupManager.Console.Workflows;

/// <summary>
/// Describes how the application should proceed after presenting an out-of-date configuration.
/// </summary>
internal enum ConfigurationUpdateWorkflowOutcome
{
    UpgradeConfiguration,
    ReturnToInstances,
    ExitApplication
}
