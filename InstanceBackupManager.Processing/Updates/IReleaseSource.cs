using InstanceBackupManager.Processing.Models.Updates;

namespace InstanceBackupManager.Processing.Updates;

/// <summary>
/// Defines a source capable of discovering the latest published application release.
/// </summary>
public interface IReleaseSource
{
    /// <summary>
    /// Gets the latest published stable release.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the network operation.</param>
    /// <returns>The latest stable release metadata.</returns>
    Task<ReleaseInfo> GetLatestReleaseAsync(CancellationToken cancellationToken = default);
}
