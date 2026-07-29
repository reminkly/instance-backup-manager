using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Updates;
using InstanceBackupManager.Processing.Updates;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Coordinates release discovery and version comparison without depending on a specific release provider.
/// </summary>
public sealed class UpdateProcessor
{
    #region Properties

    /// <summary>
    /// Gets the installed application version used for comparison.
    /// </summary>
    private Version InstalledVersion { get; }

    /// <summary>
    /// Gets the source used to discover the latest published release.
    /// </summary>
    private IReleaseSource ReleaseSource { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes an update processor using the supplied installed version and release source.
    /// </summary>
    /// <param name="installedVersion">The version of the currently running application.</param>
    /// <param name="releaseSource">The provider used to discover the latest published release.</param>
    public UpdateProcessor(
        Version installedVersion,
        IReleaseSource releaseSource
    )
    {
        ArgumentNullException.ThrowIfNull(installedVersion);
        ArgumentNullException.ThrowIfNull(releaseSource);

        InstalledVersion = NormalizeVersion(installedVersion);
        ReleaseSource = releaseSource;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Discovers the latest published release and compares it with the installed application version.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel release discovery.</param>
    /// <returns>The latest release and comparison result.</returns>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var latestRelease = await ReleaseSource.GetLatestReleaseAsync(cancellationToken);
        var latestVersion = ParseReleaseVersion(latestRelease.TagName);

        return new UpdateCheckResult
        {
            Status = latestVersion > InstalledVersion
                ? UpdateCheckStatus.UpdateAvailable
                : UpdateCheckStatus.UpToDate,
            InstalledVersion = InstalledVersion,
            LatestVersion = latestVersion,
            LatestRelease = latestRelease
        };
    }

    #endregion

    #region Version Handling

    /// <summary>
    /// Parses common GitHub release tags such as v0.6.0 into a normalized four-part version.
    /// </summary>
    private static Version ParseReleaseVersion(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        var normalizedTag = tagName.Trim().TrimStart('v', 'V');
        var metadataIndex = normalizedTag.IndexOfAny(['-', '+']);

        if (metadataIndex >= 0)
        {
            normalizedTag = normalizedTag[..metadataIndex];
        }

        if (!Version.TryParse(normalizedTag, out var version))
        {
            throw new InvalidDataException(
                $"Release tag '{tagName}' does not contain a supported numeric version."
            );
        }

        return NormalizeVersion(version);
    }

    /// <summary>
    /// Normalizes versions to four numeric components so 0.5.0 and 0.5.0.0 compare equally.
    /// </summary>
    private static Version NormalizeVersion(Version version)
    {
        return new Version(
            version.Major,
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0)
        );
    }

    #endregion
}
