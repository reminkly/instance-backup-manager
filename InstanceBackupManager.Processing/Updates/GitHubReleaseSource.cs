using System.Net.Http.Headers;
using System.Text.Json;
using InstanceBackupManager.Processing.Models.Updates;

namespace InstanceBackupManager.Processing.Updates;

/// <summary>
/// Retrieves the latest stable release for a public repository through the GitHub Releases API.
/// </summary>
public sealed class GitHubReleaseSource : IReleaseSource
{
    #region Properties

    /// <summary>
    /// Gets the HTTP client used to call the GitHub API.
    /// </summary>
    private HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the absolute API endpoint for the repository's latest stable release.
    /// </summary>
    private Uri LatestReleaseUri { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a GitHub release source for the supplied public repository.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call GitHub.</param>
    /// <param name="repositoryOwner">The GitHub account that owns the repository.</param>
    /// <param name="repositoryName">The repository containing published releases.</param>
    public GitHubReleaseSource(
        HttpClient httpClient,
        string repositoryOwner,
        string repositoryName
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryOwner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);

        HttpClient = httpClient;
        LatestReleaseUri = new Uri(
            $"https://api.github.com/repos/{Uri.EscapeDataString(repositoryOwner)}/{Uri.EscapeDataString(repositoryName)}/releases/latest"
        );
    }

    #endregion

    #region Public Methods

    /// <inheritdoc/>
    public async Task<ReleaseInfo> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            LatestReleaseUri
        );

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json")
        );
        request.Headers.UserAgent.ParseAdd("InstanceBackupManager-UpdateCheck");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            contentStream,
            cancellationToken: cancellationToken
        );

        var root = document.RootElement;
        var tagName = GetRequiredString(root, "tag_name");
        var pageUrl = GetRequiredString(root, "html_url");
        var releaseName = GetOptionalString(root, "name");

        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri))
        {
            throw new InvalidDataException("GitHub returned an invalid release-page URL.");
        }

        return new ReleaseInfo
        {
            TagName = tagName,
            Name = string.IsNullOrWhiteSpace(releaseName) ? tagName : releaseName,
            PageUri = pageUri,
            Notes = GetOptionalString(root, "body")
        };
    }

    #endregion

    #region JSON Helpers

    /// <summary>
    /// Reads a required non-empty string property from a GitHub API response.
    /// </summary>
    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        var value = GetOptionalString(element, propertyName);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"GitHub did not return required release property '{propertyName}'.")
            : value;
    }

    /// <summary>
    /// Reads an optional string property from a GitHub API response.
    /// </summary>
    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    #endregion
}
