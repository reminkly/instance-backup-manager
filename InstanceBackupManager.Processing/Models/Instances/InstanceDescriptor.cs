namespace InstanceBackupManager.Processing.Models.Instances;

/// <summary>
/// Describes an instance directory discovered beneath the portable application's instances directory.
/// </summary>
public sealed class InstanceDescriptor
{
    #region Properties

    /// <summary>
    /// Gets the directory name displayed for the discovered instance.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the normalized absolute path of the discovered instance directory.
    /// </summary>
    public required string InstancePath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the instance directory contains an <c>instance.json</c> configuration file.
    /// </summary>
    public bool HasConfiguration { get; init; }

    #endregion
}
