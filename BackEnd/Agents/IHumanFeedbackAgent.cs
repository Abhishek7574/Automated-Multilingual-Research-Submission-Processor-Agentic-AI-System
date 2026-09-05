using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Human Feedback Agent – presents flagged items to the admin for review,
/// accepts corrections, and stores them to improve future extraction/validation.
/// Any pipeline step result with confidence &lt; 25 % triggers an automatic flag.
/// </summary>
public interface IHumanFeedbackAgent
{
    /// <summary>
    /// Evaluates pipeline results and determines whether human review is needed.
    /// </summary>
    Task<StepResult<HumanFeedbackResult>> EvaluateAsync(
        string documentId, PipelineStepSummary summary, CancellationToken ct = default);

    /// <summary>
    /// Accepts an admin correction for a previously flagged item and persists it.
    /// </summary>
    Task ApplyCorrectionAsync(
        string documentId, string field, string correction, CancellationToken ct = default);
}
