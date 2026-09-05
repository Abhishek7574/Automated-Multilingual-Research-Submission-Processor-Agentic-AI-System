using Azure;
using Azure.AI.ContentSafety;
using Backend.Models;
using System.Diagnostics;

namespace Backend.Agents;

/// <summary>
/// Content Safety Agent – stub implementation.
/// TODO: Replace with Azure AI Content Safety SDK to detect toxicity,
/// hate speech, violence and illicit content. Flag for HITL when flagged.
/// Decoupled from business-rule Validation Agent.
/// </summary>
public class ContentSafetyAgent : IContentSafetyAgent
{
    private readonly ILogger<ContentSafetyAgent> _logger;
    private readonly ContentSafetyClient _client;

    private const int MaxChunkSize = 9000;

    public ContentSafetyAgent(
        IConfiguration config,
        ILogger<ContentSafetyAgent> logger)
    {
        _logger = logger;

        var endpoint = config["AzureContentSafety:Endpoint"];
        var key = config["AzureContentSafety:Key"];

        _client = new ContentSafetyClient(
            new Uri(endpoint),
            new AzureKeyCredential(key));
    }

    public async Task<StepResult<ContentSafetyResult>> CheckAsync(
        string text,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new StepResult<ContentSafetyResult>(
                    false,
                    null,
                    "Text is empty",
                    sw.ElapsedMilliseconds);
            }

            _logger.LogInformation(
                "[ContentSafetyAgent] Scanning {Length} characters",
                text.Length);

            var chunks = SplitText(text, MaxChunkSize);

            var flags = new List<SafetyFlag>();

            foreach (var chunk in chunks)
            {
                var request = new AnalyzeTextOptions(chunk);

                Response<AnalyzeTextResult> response =
                    await _client.AnalyzeTextAsync(request, ct);

                var result = response.Value;

                foreach (var category in result.CategoriesAnalysis)
                {
                    var severity = (category.Severity ?? 0) / 7.0;

                    if (category.Severity > 0)
                    {
                        flags.Add(new SafetyFlag(
                            MapCategory(category.Category),
                            severity,
                            $"Detected {category.Category} severity {category.Severity}"
                        ));
                    }
                }
            }

            bool isSafe = flags.Count == 0;

            string rating =
                flags.Any(f => f.Severity > 0.7) ? "HighRisk" :
                flags.Any() ? "ModerateRisk" :
                "Safe";

            var resultModel = new ContentSafetyResult(
                IsSafe: isSafe,
                Flags: flags,
                OverallRating: rating
            );

            sw.Stop();

            _logger.LogInformation(
                "[ContentSafetyAgent] Completed. Safe={Safe}, Flags={Count}",
                isSafe,
                flags.Count);

            return new StepResult<ContentSafetyResult>(
                true,
                resultModel,
                null,
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();

            _logger.LogError(ex,
                "[ContentSafetyAgent] Content safety check failed");

            return new StepResult<ContentSafetyResult>(
                false,
                null,
                ex.Message,
                sw.ElapsedMilliseconds);
        }
    }

    private List<string> SplitText(string text, int maxChunkSize)
    {
        var chunks = new List<string>();

        for (int i = 0; i < text.Length; i += maxChunkSize)
        {
            chunks.Add(text.Substring(
                i,
                Math.Min(maxChunkSize, text.Length - i)));
        }

        return chunks;
    }

    private SafetyCategory MapCategory(TextCategory category)
    {
        if (category == TextCategory.Hate)
            return SafetyCategory.HateSpeech;

        if (category == TextCategory.Violence)
            return SafetyCategory.Violence;

        if (category == TextCategory.Sexual)
            return SafetyCategory.SexualContent;

        if (category == TextCategory.SelfHarm)
            return SafetyCategory.SelfHarm;

        return SafetyCategory.None;
    }
}
