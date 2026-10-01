using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SEF_Project.Api.AI.DeepSeek;

public sealed class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "deepseek-chat";

    public string Endpoint { get; set; } =
        "https://api.deepseek.com/chat/completions";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "DeepSeek configuration requires an API key.");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException(
                "DeepSeek configuration requires a model.");
        }

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "DeepSeek endpoint must be an absolute HTTPS URL.");
        }
    }
}

public interface IDeepSeekChatCompletionsClient
{
    Task<string> CompleteJsonAsync(
        string systemInstruction,
        string userJsonInput,
        CancellationToken cancellationToken = default);
}

public sealed class DeepSeekChatCompletionsClient :
    IDeepSeekChatCompletionsClient
{
    private readonly HttpClient _httpClient;
    private readonly DeepSeekOptions _options;

    public DeepSeekChatCompletionsClient(
        HttpClient httpClient,
        IOptions<DeepSeekOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> CompleteJsonAsync(
        string systemInstruction,
        string userJsonInput,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(systemInstruction))
        {
            throw new ArgumentException(
                "A DeepSeek system instruction is required.",
                nameof(systemInstruction));
        }

        if (string.IsNullOrWhiteSpace(userJsonInput))
        {
            throw new ArgumentException(
                "A DeepSeek JSON input is required.",
                nameof(userJsonInput));
        }

        _options.Validate();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            _options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user", content = userJsonInput }
            },
            response_format = new
            {
                type = "json_object"
            },
            stream = false
        });

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"DeepSeek request failed with HTTP status {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(
            cancellationToken);

        try
        {
            using var document = await JsonDocument.ParseAsync(
                responseStream,
                cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0 ||
                !choices[0].TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(content.GetString()))
            {
                throw new InvalidDataException(
                    "DeepSeek returned no JSON message content.");
            }

            return content.GetString()!;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "DeepSeek returned a malformed response envelope.",
                exception);
        }
    }
}
