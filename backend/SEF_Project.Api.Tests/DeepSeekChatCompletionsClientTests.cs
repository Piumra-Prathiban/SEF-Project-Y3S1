using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SEF_Project.Api.AI.DeepSeek;

namespace SEF_Project.Api.Tests;

public class DeepSeekChatCompletionsClientTests
{
    [Fact]
    public async Task CompleteJsonAsync_SendsJsonModeRequestAndReturnsContent()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "choices": [
                        {
                          "message": {
                            "role": "assistant",
                            "content": "{\"recommendations\":[]}"
                          },
                          "finish_reason": "stop"
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = CreateClient(handler);

        var result = await client.CompleteJsonAsync(
            "Return JSON only.",
            "{\"objective\":\"Review stock\"}");

        Assert.Equal("{\"recommendations\":[]}", result);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal(
            "https://api.deepseek.com/chat/completions",
            capturedRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", capturedRequest.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(capturedBody!);
        Assert.Equal("deepseek-chat", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            "json_object",
            body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(2, body.RootElement.GetProperty("messages").GetArrayLength());
    }

    [Fact]
    public async Task CompleteJsonAsync_WithoutApiKey_FailsBeforeSending()
    {
        var handler = new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called."));
        var client = CreateClient(handler, apiKey: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CompleteJsonAsync("Return JSON.", "{}"));

        Assert.Equal(
            "DeepSeek configuration requires an API key.",
            exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CompleteJsonAsync_WhenContentIsMissing_FailsSafely()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":null}}]}",
                    Encoding.UTF8,
                    "application/json")
            }));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.CompleteJsonAsync("Return JSON.", "{}"));

        Assert.Equal("DeepSeek returned no JSON message content.", exception.Message);
    }

    [Fact]
    public async Task CompleteJsonAsync_WhenHttpFails_DoesNotExposeResponseBody()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("sensitive provider response")
            }));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CompleteJsonAsync("Return JSON.", "{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.DoesNotContain("sensitive", exception.Message);
    }

    private static DeepSeekChatCompletionsClient CreateClient(
        HttpMessageHandler handler,
        string? apiKey = "test-key") =>
        new(
            new HttpClient(handler),
            Options.Create(new DeepSeekOptions
            {
                ApiKey = apiKey
            }));

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _send;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _send(request);
        }
    }
}
