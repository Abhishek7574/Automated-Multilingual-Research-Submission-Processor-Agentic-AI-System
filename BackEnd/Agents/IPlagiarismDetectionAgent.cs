using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Plagiarism Detection Agent – cross-references the submission against academic
/// databases. Decoupled from the Validation Agent (business rules).
/// </summary>
public interface IPlagiarismDetectionAgent
{
    Task<StepResult<PlagiarismResult>> DetectAsync(string text, CancellationToken ct = default);
}
