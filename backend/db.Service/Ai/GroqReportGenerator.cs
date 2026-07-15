// Calls Groq's OpenAI-compatible chat completions API to turn computed order
// statistics into a plain-language report (an overview plus buy/don't-buy
// recommendations). Uses its own GROQ_REPORT_API_KEY rather than the
// description enhancer's GROQ_API_KEY, so the two AI features can be rate-
// limited/rotated independently.
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace db.Service.Ai;

public class GroqReportGenerator : IReportGenerator
{
    private const string Model = "llama-3.3-70b-versatile";

    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public GroqReportGenerator(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        _apiKey = Environment.GetEnvironmentVariable("GROQ_REPORT_API_KEY");
    }

    public async Task<string> GenerateAsync(string statsSummary, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return "AI report unavailable: GROQ_REPORT_API_KEY is not configured. Raw statistics are shown above.";
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(new
            {
                model = Model,
                temperature = 0.4,
                max_tokens = 700,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an inventory and sales analyst for a small warehouse management " +
                                  "system, writing this week's report. You're given real order statistics " +
                                  "covering the last 7 days — order counts by status, the mean and standard " +
                                  "deviation of sales and restock order values, and per-product demand (units " +
                                  "sold, mean/std dev units per order, current stock, revenue). Write a clear, " +
                                  "plain-language weekly report a store manager " +
                                  "could act on, with exactly three sections: '## Overview' (a short summary " +
                                  "of how the business performed), '## Keep Buying' (products with strong or " +
                                  "steady demand worth reordering, one line each with a reason), and " +
                                  "'## Reconsider' (products with weak, erratic, or overstocked demand that " +
                                  "may not be worth reordering, one line each with a reason). Only use the " +
                                  "numbers you're given — never invent figures."
                    },
                    new
                    {
                        role = "user",
                        content = statsSummary
                    }
                }
            });

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return "AI report unavailable right now (the AI provider returned an error). Raw statistics are shown above.";
            }

            var payload = await response.Content.ReadFromJsonAsync<GroqChatResponse>(cancellationToken: cancellationToken);
            var text = payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            return string.IsNullOrWhiteSpace(text)
                ? "AI report unavailable right now (empty response). Raw statistics are shown above."
                : text;
        }
        catch
        {
            return "AI report unavailable right now (network error). Raw statistics are shown above.";
        }
    }
}
