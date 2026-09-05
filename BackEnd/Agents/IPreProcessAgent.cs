using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Pre-process Agent – validates file type, runs OCR for scanned/image PDFs,
/// and detects the primary language of the submission.
/// </summary>
public interface IPreProcessAgent
{
    Task<StepResult<PreProcessResult>> PreProcessAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}
