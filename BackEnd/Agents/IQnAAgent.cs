using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Q&A Agent – handles conversational queries about submissions.
/// Supports multilingual input and maintains chat history per session.
/// </summary>
public interface IQnAAgent
{
    Task<StepResult<QnAReadyResult>> PrepareAsync(string documentId, string indexId, CancellationToken ct = default);
    Task<QnAResponse> AskAsync(QnARequest request, CancellationToken ct = default);
}
