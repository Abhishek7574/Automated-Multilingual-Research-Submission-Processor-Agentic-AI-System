using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Content Safety Agent – checks for toxicity, hate speech, illicit content,
/// and flags submissions for human review when violations are detected.
/// Decoupled from the Validation Agent (business rules).
/// </summary>
public interface IContentSafetyAgent
{
    Task<StepResult<ContentSafetyResult>> CheckAsync(string text, CancellationToken ct = default);
}
