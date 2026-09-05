using Azure;
using Azure.AI.OpenAI;
using Backend.Models;
using OpenAI.Chat;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Backend.Agents;

/// <summary>
/// Translation Agent – stub implementation.
/// TODO: Replace with Azure AI Translator SDK call to translate non-English
/// document text into English. Store both original and translated versions
/// in the document context so downstream agents can reference either language.
/// </summary>
public class TranslationAgent : ITranslationAgent
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<TranslationAgent> _logger;

    public TranslationAgent(IConfiguration config, ILogger<TranslationAgent> logger)
    {
        _logger = logger;

        var endpoint = new Uri(config["AzureOpenAI:Endpoint"]);
        var key = config["AzureOpenAI:ApiKey"];
        var deployment = config["AzureOpenAI:ChatDeployment"];

        var client = new AzureOpenAIClient(endpoint, new AzureKeyCredential(key));
        _chatClient = client.GetChatClient(deployment);
    }

    public async Task<StepResult<TranslationResult>> TranslateAsync(
        string text, string sourceLanguageCode, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Check if translation is even needed
        var isEnglish = string.Equals(sourceLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        if (isEnglish || string.IsNullOrWhiteSpace(text))
        {
            var skipResult = new TranslationResult(text, text, sourceLanguageCode, "English", false);
            return new StepResult<TranslationResult>(true, skipResult, "Skipped", sw.ElapsedMilliseconds);
        }

        try
        {
            // 2. Modified AI Prompt to explicitly ask for the Language Name
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    $"You are a professional translator. Translate the text from {sourceLanguageCode} to English. " +
                    "Also, identify the full English name of the source language (e.g., 'Spanish', 'Japanese'). " +
                    "Return strictly in this format:\nName: [Language Name]\nTranslation: [Text]"),
                new UserChatMessage(text)
            };

            var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);
            var responseBody = response.Value.Content[0].Text.Trim();

            // 3. Extract the Name and the Translation using Regex
            var languageName = Regex.Match(responseBody, @"Name:\s*(.*)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            var translatedText = Regex.Match(responseBody, @"Translation:\s*(.*)", RegexOptions.Singleline | RegexOptions.IgnoreCase).Groups[1].Value.Trim();

            // Fallback if AI fails format
            if (string.IsNullOrEmpty(languageName)) languageName = sourceLanguageCode;
            if (string.IsNullOrEmpty(translatedText)) translatedText = responseBody;

            // 4. Construct TranslationResult (Matching your 5-parameter positional constructor)
            // Order based on your code: (Original, Translated, Code, Name, WasTranslated)
            var result = new TranslationResult(
                text,
                translatedText,
                sourceLanguageCode,
                languageName,
                true
            );

            sw.Stop();

            // 5. Construct StepResult (Success, Data, Message, ElapsedMs)
            return new StepResult<TranslationResult>(true, result, "Success", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[TranslationAgent] Translation failed");
            return new StepResult<TranslationResult>(false, null, ex.Message, sw.ElapsedMilliseconds);
        }
    }
}
