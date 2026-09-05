using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// RAG Agent – generates embeddings and maintains the vector store for retrieval,
/// augmentation and generation queries.
/// </summary>
public interface IRagAgent
{
    Task<StepResult<RagIndexResult>> IndexAsync(string documentId, string text, CancellationToken ct = default);
}
