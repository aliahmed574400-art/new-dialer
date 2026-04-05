using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using System.Text;
using System.Text.Json;
using NewDialer.Contracts.Agents;
using NewDialer.Contracts.Analytics;
using NewDialer.Contracts.Auth;
using NewDialer.Contracts.Dialer;
using NewDialer.Contracts.Leads;
using NewDialer.Contracts.Platform;

namespace NewDialer.Desktop.Services;

public sealed class NewDialerApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient = new();
    private string _apiBaseUrl;
    private string? _accessToken;

    public NewDialerApiClient(string apiBaseUrl)
    {
        _apiBaseUrl = NormalizeBaseUrl(apiBaseUrl);
    }

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => _apiBaseUrl = NormalizeBaseUrl(value);
    }

    public string? AccessToken
    {
        get => _accessToken;
        set => _accessToken = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public Task<SessionDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        return SendAsync<SessionDto>(HttpMethod.Post, "api/auth/login", request, includeAuthorization: false, cancellationToken);
    }

    public Task<SessionDto> SignupAdminAsync(AdminSignupRequest request, CancellationToken cancellationToken)
    {
        return SendAsync<SessionDto>(HttpMethod.Post, "api/auth/admin-signup", request, includeAuthorization: false, cancellationToken);
    }

    public Task LogoutAsync(CancellationToken cancellationToken)
    {
        return SendAsync(HttpMethod.Post, "api/auth/logout", payload: null, includeAuthorization: true, cancellationToken);
    }

    public Task<IReadOnlyList<LeadDto>> GetLeadsAsync(bool assignedOnly, CancellationToken cancellationToken)
    {
        var path = assignedOnly ? "api/leads/assigned" : "api/leads";
        return SendAsync<IReadOnlyList<LeadDto>>(HttpMethod.Get, path, payload: null, includeAuthorization: true, cancellationToken);
    }

    public Task<IReadOnlyList<AgentAssignmentOptionDto>> GetAgentOptionsAsync(CancellationToken cancellationToken)
    {
        return SendAsync<IReadOnlyList<AgentAssignmentOptionDto>>(HttpMethod.Get, "api/leads/agents", payload: null, includeAuthorization: true, cancellationToken);
    }

    public Task<IReadOnlyList<LeadImportBatchDto>> GetImportBatchesAsync(CancellationToken cancellationToken)
    {
        return SendAsync<IReadOnlyList<LeadImportBatchDto>>(HttpMethod.Get, "api/leads/import-batches", payload: null, includeAuthorization: true, cancellationToken);
    }

    public async Task<LeadImportResultDto> ImportLeadsAsync(string filePath, Guid? defaultAgentId, string notes, CancellationToken cancellationToken)
    {
        using var stream = OpenImportFileStream(filePath);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", Path.GetFileName(filePath));
        form.Add(new StringContent(notes ?? string.Empty), "notes");

        if (defaultAgentId.HasValue)
        {
            form.Add(new StringContent(defaultAgentId.Value.ToString()), "defaultAgentId");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_apiBaseUrl), "api/leads/import"));
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        request.Content = form;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, cancellationToken);
        }

        var result = await response.Content.ReadFromJsonAsync<LeadImportResultDto>(JsonOptions, cancellationToken);
        if (result is null)
        {
            throw new InvalidOperationException("The server returned an empty import response.");
        }

        return result;
    }

    private static FileStream OpenImportFileStream(string filePath)
    {
        try
        {
            return new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException("The selected Excel file could not be found.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new InvalidOperationException("The selected Excel file location could not be found.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("The selected Excel file is not accessible. Close Excel, Explorer preview, or any app using the file and try again.");
        }
        catch (IOException)
        {
            throw new InvalidOperationException("The selected Excel file is currently in use. Close Excel, the preview pane, or any app using the file and try again.");
        }
    }

    public Task AssignLeadsAsync(Guid agentId, IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Post,
            "api/leads/assignments",
            new LeadAssignmentRequest(agentId, leadIds),
            includeAuthorization: true,
            cancellationToken);
    }

    public Task<IReadOnlyList<ScheduledCallListItemDto>> GetScheduledCallsAsync(CancellationToken cancellationToken)
    {
        return SendAsync<IReadOnlyList<ScheduledCallListItemDto>>(HttpMethod.Get, "api/schedules", payload: null, includeAuthorization: true, cancellationToken);
    }

    public Task<ScheduledCallListItemDto> CreateScheduledCallAsync(CreateScheduledCallRequest request, CancellationToken cancellationToken)
    {
        return SendAsync<ScheduledCallListItemDto>(HttpMethod.Post, "api/schedules", request, includeAuthorization: true, cancellationToken);
    }

    public Task<StartCallResponse> StartCallAsync(StartCallRequest request, CancellationToken cancellationToken)
    {
        return SendAsync<StartCallResponse>(HttpMethod.Post, "api/dialer/call/start", request, includeAuthorization: true, cancellationToken);
    }

    public Task HangUpAsync(HangUpCallRequest request, CancellationToken cancellationToken)
    {
        return SendAsync(HttpMethod.Post, "api/dialer/call/hangup", request, includeAuthorization: true, cancellationToken);
    }

    public Task<IReadOnlyList<TenantOverviewDto>> GetPlatformOverviewAsync(CancellationToken cancellationToken)
    {
        return SendAsync<IReadOnlyList<TenantOverviewDto>>(HttpMethod.Get, "api/platform/overview", payload: null, includeAuthorization: true, cancellationToken);
    }

    public Task<IReadOnlyList<AgentPerformanceDto>> GetAgentPerformanceAsync(CancellationToken cancellationToken)
    {
        return SendAsync<IReadOnlyList<AgentPerformanceDto>>(HttpMethod.Get, "api/agents/performance", payload: null, includeAuthorization: true, cancellationToken);
    }

    public Task<IReadOnlyList<AgentAdminDto>> GetAgentsAsync(CancellationToken cancellationToken)
    {
        return SendAsync<IReadOnlyList<AgentAdminDto>>(HttpMethod.Get, "api/agents", payload: null, includeAuthorization: true, cancellationToken);
    }

    public Task<AgentAdminDto> CreateAgentAsync(CreateAgentRequest request, CancellationToken cancellationToken)
    {
        return SendAsync<AgentAdminDto>(HttpMethod.Post, "api/agents", request, includeAuthorization: true, cancellationToken);
    }

    public Task<AgentAdminDto> UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken cancellationToken)
    {
        return SendAsync<AgentAdminDto>(HttpMethod.Put, $"api/agents/{agentId}", request, includeAuthorization: true, cancellationToken);
    }

    public Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken)
    {
        return SendAsync(HttpMethod.Delete, $"api/agents/{agentId}", payload: null, includeAuthorization: true, cancellationToken);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task SendAsync(
        HttpMethod method,
        string relativePath,
        object? payload,
        bool includeAuthorization,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, relativePath, payload, includeAuthorization);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, cancellationToken);
        }
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? payload,
        bool includeAuthorization,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, relativePath, payload, includeAuthorization);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, cancellationToken);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        if (result is null)
        {
            throw new InvalidOperationException("The server returned an empty response.");
        }

        return result;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, object? payload, bool includeAuthorization)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_apiBaseUrl), relativePath));
        if (includeAuthorization && !string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        if (payload is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");
        }

        return request;
    }

    private static async Task<InvalidOperationException> CreateExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new InvalidOperationException($"Request failed with status {(int)response.StatusCode}.");
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();
                foreach (var property in errorsElement.EnumerateObject())
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        var message = item.GetString();
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            messages.Add($"{property.Name}: {message}");
                        }
                    }
                }

                if (messages.Count > 0)
                {
                    return new InvalidOperationException(string.Join(Environment.NewLine, messages));
                }
            }

            if (root.TryGetProperty("detail", out var detailElement) && detailElement.ValueKind == JsonValueKind.String)
            {
                var detail = detailElement.GetString();
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    return new InvalidOperationException(detail);
                }
            }

            if (root.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
            {
                var title = titleElement.GetString();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return new InvalidOperationException(title);
                }
            }
        }
        catch (JsonException)
        {
        }

        return new InvalidOperationException(content);
    }

    private static string NormalizeBaseUrl(string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return "https://new-dialer.onrender.com/";
        }

        var trimmed = apiBaseUrl.Trim();
        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }
}
