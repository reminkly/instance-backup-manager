using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Processing.Configuration;

/// <summary>
/// Discovers immediate instance directories and identifies whether each contains a configuration file.
/// </summary>
internal sealed class InstanceDiscoveryService
{
    #region Internal Methods

    /// <summary>
    /// Discovers every immediate instance directory beneath the supplied instances root.
    /// </summary>
    /// <param name="instancesPath">The directory containing individual instance directories.</param>
    /// <returns>A read-only collection ordered alphabetically by instance name.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="instancesPath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    internal IReadOnlyCollection<InstanceDescriptor> Discover(string instancesPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancesPath);

        Directory.CreateDirectory(instancesPath);

        var instances = Directory
            .EnumerateDirectories(instancesPath)
            .Select(CreateDescriptor)
            .OrderBy(
                instance => instance.Name,
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();

        return instances.AsReadOnly();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Creates a runtime descriptor for one discovered instance directory.
    /// </summary>
    /// <param name="instancePath">The discovered instance-directory path.</param>
    /// <returns>A descriptor containing the normalized path and configuration status.</returns>
    private static InstanceDescriptor CreateDescriptor(string instancePath)
    {
        var fullInstancePath = Path.GetFullPath(instancePath);

        var configPath = Path.Combine(
            fullInstancePath,
            BackupStorageConstants.InstanceConfigurationFileName
        );

        return new InstanceDescriptor
        {
            Name = Path.GetFileName(fullInstancePath),
            InstancePath = fullInstancePath,
            HasConfiguration = File.Exists(configPath)
        };
    }

    #endregion
}