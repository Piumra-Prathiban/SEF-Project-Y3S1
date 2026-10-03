namespace SEF_Project.Api.AI.DeepSeek;

/// <summary>
/// Shared DeepSeek boundary: POSTs to the Chat Completions endpoint with
/// response_format json_object and returns choices[0].message.content as raw
/// JSON text. (Interface only for now; the HTTP implementation is the shared
/// team workstream.)
/// </summary>
public interface IDeepSeekChatCompletionsClient
{
    Task<string> CompleteJsonAsync(
        string systemInstruction,
        string userJsonInput,
        CancellationToken cancellationToken = default);
}
