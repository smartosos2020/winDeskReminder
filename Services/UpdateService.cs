using System.Reflection;
using System.Net.Http;
using System.Text.Json;
using WinDeskReminder.Models;

namespace WinDeskReminder.Services;

public sealed class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/smartosos2020/winDeskReminder/releases/latest";
    private readonly HttpClient _httpClient = new();

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var currentVersionText = GetCurrentVersionText();
        var currentVersion = ParseVersion(currentVersionText);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("WinDeskReminder");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(false, currentVersionText, null, null, "还没有发布版本。");
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            var releaseUrl = root.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : null;
            var latestText = tag.TrimStart('v', 'V');
            var latestVersion = ParseVersion(latestText);

            if (latestVersion > currentVersion)
            {
                return new UpdateCheckResult(
                    true,
                    currentVersionText,
                    latestText,
                    releaseUrl,
                    $"发现新版本 {latestText}，当前版本 {currentVersionText}。");
            }

            return new UpdateCheckResult(false, currentVersionText, latestText, releaseUrl, $"当前已经是最新版本：{currentVersionText}。");
        }
        catch (Exception exception)
        {
            LogService.Write(exception);
            return new UpdateCheckResult(false, currentVersionText, null, null, $"检查更新失败：{exception.Message}");
        }
    }

    private static string GetCurrentVersionText()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return string.IsNullOrWhiteSpace(version) ? "0.0.0" : version;
    }

    private static Version ParseVersion(string value)
    {
        return Version.TryParse(value, out var version) ? version : new Version(0, 0, 0);
    }
}
