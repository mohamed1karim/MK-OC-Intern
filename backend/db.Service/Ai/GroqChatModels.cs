// Shared response shape for Groq's OpenAI-compatible chat completions API —
// used by both GroqProductDescriptionEnhancer and GroqReportGenerator, since
// they call the same endpoint format with different prompts/keys.
using System.Text.Json.Serialization;

namespace db.Service.Ai;

internal class GroqChatResponse
{
    [JsonPropertyName("choices")]
    public List<GroqChoice>? Choices { get; set; }
}

internal class GroqChoice
{
    [JsonPropertyName("message")]
    public GroqMessage? Message { get; set; }
}

internal class GroqMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
