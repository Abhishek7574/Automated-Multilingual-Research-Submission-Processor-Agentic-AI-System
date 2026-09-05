using Azure;
using Azure.AI.OpenAI;
using Backend.Models;
using OpenAI.Chat;
using System.Diagnostics;
using System.Text.Json;

namespace Backend.Agents;

/// <summary>
/// Extraction Agent – stub implementation.
/// TODO: Replace with Azure Document Intelligence + SK Native Functions
/// to extract title, authors, affiliations, abstract, keywords and figures.
/// </summary>
public class ExtractionAgent : IExtractionAgent
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<ExtractionAgent> _logger;

    public ExtractionAgent(IConfiguration config, ILogger<ExtractionAgent> logger)
    {
        _logger = logger;

        var endpoint = new Uri(config["AzureOpenAI:Endpoint"]);
        var key = config["AzureOpenAI:ApiKey"];
        var deployment = config["AzureOpenAI:ChatDeployment"];

        var client = new AzureOpenAIClient(endpoint, new AzureKeyCredential(key));
        _chatClient = client.GetChatClient(deployment);
    }

    public async Task<StepResult<DocumentMetadata>> ExtractAsync(
        string text,
        string fileName,int pageCount,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new StepResult<DocumentMetadata>(
                    false, null, "Empty document text");
            }

            var prompt = $$"""
                        You are an academic document parser.

                        Extract the following metadata from the research paper text:

                        - title
                        - authors
                        - affiliations
                        - abstract
                        - keywords
                        - figures

                        Return STRICT JSON in this format:

                        {
                        "title": "",
                        "authors": [],
                        "affiliations": [],
                        "abstract": "",
                        "keywords": [],
                        "figures": []
                        }

                        Text:
                        {{text}}
                        """;

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You extract structured metadata from research papers."),
                new UserChatMessage(prompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);

            var json = response.Value.Content[0].Text.Trim();

            var parsed = JsonDocument.Parse(json);

            var root = parsed.RootElement;
            List<string> ParseArray(JsonElement element)
            {
                var list = new List<string>();

                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        list.Add(item.GetString() ?? "");
                    }
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        // Take first property value if object
                        var prop = item.EnumerateObject().FirstOrDefault();
                        if (prop.Value.ValueKind == JsonValueKind.String)
                            list.Add(prop.Value.GetString() ?? "");
                    }
                }

                return list;
            }

            var metadata = new DocumentMetadata(
    Title: root.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
    Authors: root.TryGetProperty("authors", out var authors) ? ParseArray(authors) : new(),
    Affiliations: root.TryGetProperty("affiliations", out var aff) ? ParseArray(aff) : new(),
    Abstract: root.TryGetProperty("abstract", out var abs) ? abs.GetString() ?? "" : "",
    Keywords: root.TryGetProperty("keywords", out var kw) ? ParseArray(kw) : new(),
    Figures: root.TryGetProperty("figures", out var fig) ? ParseArray(fig) : new(),
    PageCount: pageCount,
    Format: Path.GetExtension(fileName).TrimStart('.').ToUpper()
);

            sw.Stop();

            return new StepResult<DocumentMetadata>(
                true,
                metadata,
                "Success",
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[ExtractionAgent] AI extraction failed");

            return new StepResult<DocumentMetadata>(
                false,
                null,
                ex.Message,
                sw.ElapsedMilliseconds);
        }
    }
}
