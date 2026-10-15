
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ShadPs4Plus.PKGExtractor.GUI.Updates;

public sealed class UpdateChecker
{
    private const string ReleasesApiUrl =
        "https://api.github.com/repos/Dubstroy/ShadPs4Plus-PKG-Extractor-GUI/releases/latest";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(
                "ShadPs4Plus-PKG-Extractor-GUI",
                "1.0.0"));

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));

        return client;
    }

    public async Task<GitHubReleaseInfo?> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(
            ReleasesApiUrl,
            cancellationToken);

        // Todavía no hay ninguna Release publicada.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using JsonDocument document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

        JsonElement root = document.RootElement;

        string? tagName = root
            .GetProperty("tag_name")
            .GetString();

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new InvalidDataException(
                "GitHub no devolvió una etiqueta de versión válida.");
        }

        string versionText = tagName.Trim();

        if (versionText.StartsWith(
            "v",
            StringComparison.OrdinalIgnoreCase))
        {
            versionText = versionText[1..];
        }

        if (!Version.TryParse(
    versionText,
    out Version? latestVersion))
        {
            throw new InvalidDataException(
                $"La versión publicada en GitHub no es válida: {tagName}");
        }

        string releaseName =
            root.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString() ?? tagName
                : tagName;

        string releaseNotes =
            root.TryGetProperty(
                "body",
                out JsonElement bodyElement)
                    ? bodyElement.GetString() ?? string.Empty
                    : string.Empty;

        string releaseUrl =
            root.TryGetProperty(
                "html_url",
                out JsonElement urlElement)
                    ? urlElement.GetString() ?? string.Empty
                    : string.Empty;

        return new GitHubReleaseInfo(
            latestVersion,
            tagName,
            releaseName,
            releaseNotes,
            releaseUrl);
    }
}

public sealed record GitHubReleaseInfo(
    Version Version,
    string TagName,
    string Name,
    string ReleaseNotes,
    string ReleaseUrl);

