using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Validation Agent – enforces submission business rules:
///   • Page count must be between 8 and 25 (inclusive)
///   • Required sections: Title, Abstract, Keywords, Authors, References
/// This agent is decoupled from ContentSafetyAgent (toxicity) and
/// PlagiarismDetectionAgent (similarity), which run as separate pipeline steps.
/// </summary>
public interface IValidationAgent
{
    Task<StepResult<ValidationResult>> ValidateAsync(
        DocumentMetadata metadata, string text, CancellationToken ct = default);
}
