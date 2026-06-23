using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;

namespace TFTCoach.Infrastructure.InferenceService;

/// <summary>
/// Calls the local LM Studio OpenAI-compatible embeddings endpoint. Inference is
/// strictly local — the base address must point at the local LM Studio server.
/// </summary>
public sealed class LMStudioClient(
    HttpClient httpClient,
    string embeddingModel,
    string chatModel,
    ILogger<LMStudioClient> logger) : ILMStudioClient
{
    public async Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new EmbeddingRequest(embeddingModel, text);
            using var response = await httpClient.PostAsJsonAsync("embeddings", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken);
            var vector = payload?.Data?.FirstOrDefault()?.Embedding;
            if (vector is null || vector.Count == 0)
            {
                throw new InvalidOperationException("LM Studio returned an empty embedding.");
            }

            return vector;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "LM Studio embedding request failed.");
            throw;
        }
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ChatRequest(
                chatModel,
                [
                    new ChatMessage("system", systemPrompt),
                    new ChatMessage("user", userMessage),
                ]);

            using var response = await httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
            var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("LM Studio returned an empty completion.");
            }

            return content;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "LM Studio chat completion request failed.");
            throw;
        }
    }

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("data")] List<EmbeddingData>? Data);

    private sealed record EmbeddingData(
        [property: JsonPropertyName("embedding")] List<float>? Embedding);

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse(
        [property: JsonPropertyName("choices")] List<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message);
}
