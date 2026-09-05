using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Ingestion Agent – simulates continuous email inbox monitoring by reading
/// research paper submissions from a designated watch folder on the file system.
/// In production this would connect to a live mailbox (Exchange / IMAP / Graph API).
/// </summary>
public interface IIngestionAgent
{
    /// <summary>
    /// Scans the inbox folder and returns the next pending document for processing.
    /// </summary>
    Task<StepResult<IngestionResult>> IngestAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Lists all pending documents in the watch folder.
    /// </summary>
    Task<IReadOnlyList<IngestionResult>> ListPendingAsync(CancellationToken ct = default);
}
