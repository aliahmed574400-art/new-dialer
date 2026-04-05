using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NewDialer.Application.Abstractions;
using NewDialer.Application.Models;

namespace NewDialer.Infrastructure.Zoom;

public sealed class ZoomPhoneGateway(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IOptions<ZoomPhoneOptions> options) : IZoomPhoneGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string AccessTokenCacheKey = "zoom-phone-access-token";

    public async Task<ZoomCallStartResult> StartOutboundCallAsync(OutboundDialRequest request, CancellationToken cancellationToken)
    {
        var configuredOptions = options.Value;
        if (string.IsNullOrWhiteSpace(configuredOptions.StartCallPath))
        {
            return new ZoomCallStartResult(
                Succeeded: false,
                ExternalCallId: string.Empty,
                Message: "Zoom call-start path is not configured yet. Set ZoomPhone:StartCallPath after confirming the exact Zoom Phone endpoint for your account.");
        }

        var client = await CreateAuthorizedClientAsync(cancellationToken);
        var targetPath = ResolvePath(configuredOptions.StartCallPath, string.Empty, configuredOptions.SharedUserId);

        var payload = new Dictionary<string, object?>
        {
            ["phone_number"] = request.PhoneNumber,
            ["display_name"] = request.DisplayName,
            ["shared_user_id"] = configuredOptions.SharedUserId,
            ["shared_caller_id"] = configuredOptions.SharedCallerId,
            ["lead_id"] = request.LeadId,
            ["agent_id"] = request.AgentId,
            ["tenant_id"] = request.TenantId,
        };

        using var response = await client.PostAsJsonAsync(targetPath, payload, JsonOptions, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ZoomCallStartResult(
                Succeeded: false,
                ExternalCallId: string.Empty,
                Message: $"Zoom start call request failed with HTTP {(int)response.StatusCode}: {TrimResponse(responseContent)}");
        }

        var externalCallId = ExtractExternalCallId(responseContent);
        return new ZoomCallStartResult(
            Succeeded: true,
            ExternalCallId: externalCallId,
            Message: string.IsNullOrWhiteSpace(externalCallId)
                ? "Zoom call request accepted, but no call identifier was found in the response."
                : "Zoom call request accepted.");
    }

    public async Task HangUpAsync(string externalCallId, CancellationToken cancellationToken)
    {
        var configuredOptions = options.Value;
        if (string.IsNullOrWhiteSpace(configuredOptions.HangUpPath))
        {
            throw new InvalidOperationException("Zoom hang-up path is not configured yet. Set ZoomPhone:HangUpPath after confirming the exact Zoom Phone endpoint for your account.");
        }

        var client = await CreateAuthorizedClientAsync(cancellationToken);
        var targetPath = ResolvePath(configuredOptions.HangUpPath, externalCallId, configuredOptions.SharedUserId);

        using var request = new HttpRequestMessage(HttpMethod.Post, targetPath)
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["call_id"] = externalCallId,
                ["shared_user_id"] = configuredOptions.SharedUserId,
            }, options: JsonOptions),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Zoom hang-up request failed with HTTP {(int)response.StatusCode}: {TrimResponse(detail)}");
        }
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken cancellationToken)
    {
        var configuredOptions = options.Value;
        ValidateAuthConfiguration(configuredOptions);

        var client = httpClientFactory.CreateClient("ZoomPhone");
        client.BaseAddress = new Uri(AppendTrailingSlash(configuredOptions.BaseUrl));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        return client;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue<string>(AccessTokenCacheKey, out var cachedToken)
            && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        var configuredOptions = options.Value;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{configuredOptions.TokenUrl}?grant_type=account_credentials&account_id={Uri.EscapeDataString(configuredOptions.AccountId)}");

        var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{configuredOptions.ClientId}:{configuredOptions.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);

        var client = httpClientFactory.CreateClient("ZoomPhoneOAuth");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Zoom OAuth token request failed with HTTP {(int)response.StatusCode}: {TrimResponse(content)}");
        }

        var tokenResponse = JsonSerializer.Deserialize<ZoomTokenResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("Zoom OAuth token response was empty.");

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Zoom OAuth token response did not contain an access token.");
        }

        var expiresInSeconds = Math.Max(60, tokenResponse.ExpiresIn - 60);
        memoryCache.Set(
            AccessTokenCacheKey,
            tokenResponse.AccessToken,
            TimeSpan.FromSeconds(expiresInSeconds));

        return tokenResponse.AccessToken;
    }

    private static void ValidateAuthConfiguration(ZoomPhoneOptions configuredOptions)
    {
        if (string.IsNullOrWhiteSpace(configuredOptions.AccountId)
            || string.IsNullOrWhiteSpace(configuredOptions.ClientId)
            || string.IsNullOrWhiteSpace(configuredOptions.ClientSecret))
        {
            throw new InvalidOperationException(
                "Zoom shared-account credentials are not configured. Set ZoomPhone:AccountId, ClientId, and ClientSecret.");
        }
    }

    private static string ResolvePath(string configuredPath, string externalCallId, string sharedUserId)
    {
        return configuredPath
            .Replace("{callId}", Uri.EscapeDataString(externalCallId), StringComparison.OrdinalIgnoreCase)
            .Replace("{externalCallId}", Uri.EscapeDataString(externalCallId), StringComparison.OrdinalIgnoreCase)
            .Replace("{sharedUserId}", Uri.EscapeDataString(sharedUserId), StringComparison.OrdinalIgnoreCase);
    }

    private static string AppendTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
    }

    private static string ExtractExternalCallId(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;
        var propertyNames = new[] { "call_id", "callId", "id", "uuid" };

        foreach (var propertyName in propertyNames)
        {
            if (TryReadString(root, propertyName, out var value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string TrimResponse(string content)
    {
        return string.IsNullOrWhiteSpace(content)
            ? "No response body was returned."
            : content.Length <= 300 ? content : content[..300];
    }

    private sealed record ZoomTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
