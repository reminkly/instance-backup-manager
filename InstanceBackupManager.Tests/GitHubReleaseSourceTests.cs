using System.Net;
using InstanceBackupManager.Processing.Updates;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests GitHub release response parsing, request construction, and invalid-response handling.
/// </summary>
[TestClass]
public sealed class GitHubReleaseSourceTests
{
    #region Release Tests

    /// <summary>
    /// Verifies that the latest-release response is converted into provider-independent release metadata.
    /// </summary>
    [TestMethod]
    public async Task GetLatestReleaseAsync_WhenResponseIsValid_ReturnsRelease()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "tag_name": "v0.6.0",
              "name": "Instance Backup Manager v0.6.0",
              "html_url": "https://github.com/reminkly/instance-backup-manager/releases/tag/v0.6.0",
              "body": "Release notes"
            }
            """
        );

        using var httpClient = new HttpClient(handler);
        var source = new GitHubReleaseSource(
            httpClient,
            "reminkly",
            "instance-backup-manager"
        );

        var release = await source.GetLatestReleaseAsync();

        Assert.AreEqual("v0.6.0", release.TagName);
        Assert.AreEqual("Instance Backup Manager v0.6.0", release.Name);
        Assert.AreEqual("Release notes", release.Notes);
        Assert.AreEqual(
            new Uri("https://github.com/reminkly/instance-backup-manager/releases/tag/v0.6.0"),
            release.PageUri
        );
    }

    /// <summary>
    /// Verifies that requests target the expected repository endpoint and identify the application to GitHub.
    /// </summary>
    [TestMethod]
    public async Task GetLatestReleaseAsync_WhenCalled_UsesExpectedEndpointAndHeaders()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "tag_name": "v0.6.0",
              "html_url": "https://github.com/reminkly/instance-backup-manager/releases/tag/v0.6.0"
            }
            """
        );

        using var httpClient = new HttpClient(handler);
        var source = new GitHubReleaseSource(
            httpClient,
            "reminkly",
            "instance-backup-manager"
        );

        await source.GetLatestReleaseAsync();

        Assert.AreEqual(
            new Uri("https://api.github.com/repos/reminkly/instance-backup-manager/releases/latest"),
            handler.RequestUri
        );
        Assert.IsTrue(handler.HasUserAgent);
    }

    /// <summary>
    /// Verifies that a response missing required release metadata is rejected.
    /// </summary>
    [TestMethod]
    public async Task GetLatestReleaseAsync_WhenTagIsMissing_ThrowsInvalidDataException()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "html_url": "https://github.com/reminkly/instance-backup-manager/releases/latest"
            }
            """
        );

        using var httpClient = new HttpClient(handler);
        var source = new GitHubReleaseSource(
            httpClient,
            "reminkly",
            "instance-backup-manager"
        );

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => source.GetLatestReleaseAsync()
        );
    }

    #endregion

    #region Test Types

    /// <summary>
    /// Returns a deterministic JSON response and captures important request metadata.
    /// </summary>
    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        #region Properties

        internal Uri? RequestUri { get; private set; }

        internal bool HasUserAgent { get; private set; }

        #endregion

        #region Protected Methods

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;
            HasUserAgent = request.Headers.UserAgent.Count > 0;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson)
                }
            );
        }

        #endregion
    }

    #endregion
}
