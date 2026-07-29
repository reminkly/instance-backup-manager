using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Updates;
using InstanceBackupManager.Processing.Updates;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests release-tag parsing and installed-versus-published version comparison.
/// </summary>
[TestClass]
public sealed class UpdateProcessorTests
{
    #region Update Comparison Tests

    /// <summary>
    /// Verifies that a greater published version is reported as available.
    /// </summary>
    [TestMethod]
    public async Task CheckForUpdatesAsync_WhenPublishedVersionIsNewer_ReturnsUpdateAvailable()
    {
        var processor = CreateProcessor(
            new Version(0, 5, 0),
            "v0.6.0"
        );

        var result = await processor.CheckForUpdatesAsync();

        Assert.AreEqual(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.AreEqual(new Version(0, 6, 0, 0), result.LatestVersion);
    }

    /// <summary>
    /// Verifies that three-part release tags and four-part assembly versions compare equally.
    /// </summary>
    [TestMethod]
    public async Task CheckForUpdatesAsync_WhenVersionsDifferOnlyByMissingRevision_ReturnsUpToDate()
    {
        var processor = CreateProcessor(
            new Version(0, 5, 0, 0),
            "v0.5.0"
        );

        var result = await processor.CheckForUpdatesAsync();

        Assert.AreEqual(UpdateCheckStatus.UpToDate, result.Status);
    }

    /// <summary>
    /// Verifies that an installed version newer than the latest published release is not treated as needing an update.
    /// </summary>
    [TestMethod]
    public async Task CheckForUpdatesAsync_WhenInstalledVersionIsNewer_ReturnsUpToDate()
    {
        var processor = CreateProcessor(
            new Version(0, 7, 0),
            "v0.6.0"
        );

        var result = await processor.CheckForUpdatesAsync();

        Assert.AreEqual(UpdateCheckStatus.UpToDate, result.Status);
    }

    /// <summary>
    /// Verifies that suffixes on a release tag do not prevent comparison of its numeric version.
    /// </summary>
    [TestMethod]
    public async Task CheckForUpdatesAsync_WhenTagContainsSuffix_ParsesNumericVersion()
    {
        var processor = CreateProcessor(
            new Version(0, 5, 0),
            "v0.6.0+release"
        );

        var result = await processor.CheckForUpdatesAsync();

        Assert.AreEqual(new Version(0, 6, 0, 0), result.LatestVersion);
    }

    /// <summary>
    /// Verifies that a release tag without a numeric version is rejected.
    /// </summary>
    [TestMethod]
    public async Task CheckForUpdatesAsync_WhenTagIsInvalid_ThrowsInvalidDataException()
    {
        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => CreateProcessor(
                new Version(0, 5, 0),
                "latest"
            ).CheckForUpdatesAsync()
        );
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates an update processor backed by one deterministic release.
    /// </summary>
    private static UpdateProcessor CreateProcessor(
        Version installedVersion,
        string releaseTag
    )
    {
        return new UpdateProcessor(
            installedVersion,
            new TestReleaseSource(releaseTag)
        );
    }

    #endregion

    #region Test Types

    /// <summary>
    /// Returns deterministic release metadata without performing a network request.
    /// </summary>
    private sealed class TestReleaseSource(string releaseTag) : IReleaseSource
    {
        /// <inheritdoc/>
        public Task<ReleaseInfo> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new ReleaseInfo
                {
                    TagName = releaseTag,
                    Name = $"Release {releaseTag}",
                    PageUri = new Uri("https://github.com/reminkly/instance-backup-manager/releases/latest")
                }
            );
        }
    }

    #endregion
}
