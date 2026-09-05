using Backend.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Backend.Agents;

/// <summary>
/// Summary Agent – stub implementation.
/// TODO: Use SK Semantic Function with a prompt template to generate a
/// ≤250-word structured summary including key findings, validation issues
/// and missing sections via Azure OpenAI.
/// </summary>
public class SummaryAgent : ISummaryAgent
{
    private readonly ILogger<SummaryAgent> _logger;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public SummaryAgent(
        ILogger<SummaryAgent> logger,
        IConfiguration config,
        HttpClient httpClient)
    {
        _logger = logger;
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<StepResult<SummarizationResult>> SummarizeAsync(
        string text,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Generating AI summary using Azure OpenAI...");

            var endpoint = _config["AzureOpenAI:Endpoint"];
            var apiKey = _config["AzureOpenAI:ApiKey"];
            var deployment = _config["AzureOpenAI:ChatDeployment"];

            var url =
                $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version=2024-02-15-preview";

            var summaryInput = text.Length > 8000 ? text.Substring(0, 8000) : text;

            // Structured prompt
            var prompt = $$"""
                    You are an expert academic research assistant.

                    Analyze the research paper text below and return a structured JSON summary.

                    Return ONLY valid JSON in the following format:

                    {
                     "summary": "Short summary of the research paper",
                     "keyFindings": ["point1","point2","point3"],
                     "missingSections": ["section1","section2"],
                     "validationIssues": ["issue1","issue2"],
                     "topics":["topic1","topic2"],
                     "methodology": "Briefly describe the research methods, models, datasets, or experimental procedures used in the study."
                    }

                             

                    Research Paper Text:
                    {{summaryInput}}
             """;

            var requestBody = new
            {
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You summarize academic research papers."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                temperature = 0.2,
                max_tokens = 600
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);

            request.Headers.Add("api-key", apiKey);

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request, ct);

            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Azure OpenAI error: {error}", responseContent);

                return new StepResult<SummarizationResult>(
                    false,
                    null,
                    Error: responseContent,
                    ElapsedMs: sw.ElapsedMilliseconds);
            }

            using var doc = JsonDocument.Parse(responseContent);

            var aiText = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();


            if (string.IsNullOrWhiteSpace(aiText))
            {
                return new StepResult<SummarizationResult>(
                    false,
                    null,
                    "Empty AI response",
                    sw.ElapsedMilliseconds);
            }



            var result = JsonSerializer.Deserialize<SummarizationResult>(
                aiText!,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            sw.Stop();

            return new StepResult<SummarizationResult>(
                true,
                result!,
                ElapsedMs: sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();

            _logger.LogError(ex, "SummaryAgent failed");

            return new StepResult<SummarizationResult>(
                false,
                null,
                Error: ex.Message,
                ElapsedMs: sw.ElapsedMilliseconds);
        }
    }

}
