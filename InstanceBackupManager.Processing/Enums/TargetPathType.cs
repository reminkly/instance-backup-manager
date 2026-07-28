namespace InstanceBackupManager.Processing.Enums;

/// <summary>
/// Identifies the type of filesystem entry represented by a configured target path.
/// </summary>
public enum TargetPathType
{
    /// <summary>
    /// Indicates that the target type has not been configured or could not be determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Indicates that the target represents a single file.
    /// </summary>
    File = 1,

    /// <summary>
    /// Indicates that the target represents a directory and its contents.
    /// </summary>
    Directory = 2
}