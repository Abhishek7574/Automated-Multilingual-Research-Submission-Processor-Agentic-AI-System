using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Translation Agent – translates extracted document text into English.
/// Stores both the original text and the English translation in memory
/// so subsequent agents can reference either version.
/// </summary>
public interface ITranslationAgent
{
    Task<StepResult<TranslationResult>> TranslateAsync(
        string text, string sourceLanguageCode, CancellationToken ct = default);
}
