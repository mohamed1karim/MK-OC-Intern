// Calls Groq's OpenAI-compatible chat completions API to rewrite a short
// product description into a more detailed one at product-creation time.
// The API key comes straight from the GROQ_API_KEY environment variable
// (loaded from the repo-root .env by db.api's Program.cs at startup) rather
// than IConfiguration, since this project (db.Service) is a plain class
// library with no Microsoft.Extensions.Configuration reference — matching
// how the rest of this service layer stays framework-agnostic.
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace db.Service.Ai;

public class GroqProductDescriptionEnhancer : IProductDescriptionEnhancer
{
    // Groq's current flagship general-purpose model. If Groq retires this
    // model name, this is the only place that needs to change.
    private const string Model = "llama-3.3-70b-versatile";

    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public GroqProductDescriptionEnhancer(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        _apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
    }

    public async Task<string> EnhanceAsync(string productName, string shortDescription, CancellationToken cancellationToken = default)
    {
        // Nothing to enhance, or no key configured — fall back to the
        // original text rather than blocking product creation on the AI call.
        if (string.IsNullOrWhiteSpace(shortDescription) || string.IsNullOrWhiteSpace(_apiKey))
        {
            return shortDescription;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(new
            {
                model = Model,
                // Lower than a "creative writing" temperature on purpose — this
                // should read as a factual expansion, not embellished ad copy.
                temperature = 0.3,
                max_tokens = 200,
                messages = new object[]
                {
                    new { role = "system", content = AiPrompts.ProductDescriptionSystem },
                    new { role = "user", content = AiPrompts.ProductDescriptionUser(productName, shortDescription) }
                }
            });

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return shortDescription;
            }

            var payload = await response.Content.ReadFromJsonAsync<GroqChatResponse>(cancellationToken: cancellationToken);
            var enhanced = payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            return string.IsNullOrWhiteSpace(enhanced) ? shortDescription : enhanced;
        }
        catch
        {
            // Network hiccup, timeout, malformed response, etc. — the admin
            // still gets their product created with their own description.
            return shortDescription;
        }
    }
}
