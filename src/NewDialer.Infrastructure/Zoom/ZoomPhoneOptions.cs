namespace NewDialer.Infrastructure.Zoom;

public sealed class ZoomPhoneOptions
{
    public const string SectionName = "ZoomPhone";

    public string BaseUrl { get; set; } = "https://api.zoom.us/v2";

    public string TokenUrl { get; set; } = "https://zoom.us/oauth/token";

    public string AccountId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string SharedUserId { get; set; } = string.Empty;

    public string SharedCallerId { get; set; } = string.Empty;

    public string StartCallPath { get; set; } = string.Empty;

    public string HangUpPath { get; set; } = string.Empty;
}
