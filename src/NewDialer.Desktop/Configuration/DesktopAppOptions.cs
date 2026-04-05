using System.IO;
using System.Text.Json;

namespace NewDialer.Desktop.Configuration;

public sealed class DesktopAppOptions
{
    private const string DefaultApiBaseUrl = "https://new-dialer.onrender.com/";
    private const string DefaultZoomUriScheme = "zoomphonecall";

    public string ApiBaseUrl { get; init; } = DefaultApiBaseUrl;

    public bool LaunchZoomWithDialer { get; init; } = true;

    public string ZoomUriScheme { get; init; } = DefaultZoomUriScheme;

    public int ZoomLaunchDelayMs { get; init; } = 2500;

    public int ZoomActionDelayMs { get; init; } = 200;

    public int AutoNextDialDelayMs { get; init; } = 3000;

    public string? ZoomExecutablePath { get; init; }

    public static DesktopAppOptions Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            return new DesktopAppOptions();
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("Desktop", out var desktopSection))
            {
                var configuredApiBaseUrl = ReadString(desktopSection, "ApiBaseUrl");
                return new DesktopAppOptions
                {
                    ApiBaseUrl = string.IsNullOrWhiteSpace(configuredApiBaseUrl) ? DefaultApiBaseUrl : configuredApiBaseUrl,
                    LaunchZoomWithDialer = ReadBoolean(desktopSection, "LaunchZoomWithDialer", defaultValue: true),
                    ZoomUriScheme = ReadString(desktopSection, "ZoomUriScheme") ?? DefaultZoomUriScheme,
                    ZoomLaunchDelayMs = ReadInt32(desktopSection, "ZoomLaunchDelayMs", defaultValue: 2500),
                    ZoomActionDelayMs = ReadInt32(desktopSection, "ZoomActionDelayMs", defaultValue: 200),
                    AutoNextDialDelayMs = ReadInt32(desktopSection, "AutoNextDialDelayMs", defaultValue: 3000),
                    ZoomExecutablePath = ReadString(desktopSection, "ZoomExecutablePath"),
                };
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        return new DesktopAppOptions();
    }

    private static string? ReadString(JsonElement section, string propertyName)
    {
        if (section.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        return null;
    }

    private static bool ReadBoolean(JsonElement section, string propertyName, bool defaultValue)
    {
        if (section.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        return defaultValue;
    }

    private static int ReadInt32(JsonElement section, string propertyName, int defaultValue)
    {
        if (section.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return defaultValue;
    }
}
