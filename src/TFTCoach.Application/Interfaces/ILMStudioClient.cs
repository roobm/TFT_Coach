namespace TFTCoach.Application.Interfaces;

/// <summary>
/// Local LM Studio client. Inference happens entirely on the local machine —
/// no cloud endpoints are ever used.
/// </summary>
public interface ILMStudioClient
{
    /// <summary>
    /// Produces an embedding vector for the given text.
    /// </summary>
    Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a chat completion from a system prompt and user message.
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default);
}
