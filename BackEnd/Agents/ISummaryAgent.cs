using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Summary Agent – produces a human-readable validation summary report
/// for reviewers in no more than 250 words, highlighting key findings,
/// major validation issues and missing sections.
/// </summary>
public interface ISummaryAgent
{
    Task<StepResult<SummarizationResult>> SummarizeAsync(string text, CancellationToken ct = default);
}
