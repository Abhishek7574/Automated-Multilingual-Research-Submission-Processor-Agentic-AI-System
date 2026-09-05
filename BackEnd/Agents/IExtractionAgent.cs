using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Extraction Agent – extracts structured fields from a submission
/// (title, authors, affiliations, abstract, keywords, figures, page count).
/// </summary>
public interface IExtractionAgent
{
   Task<StepResult<DocumentMetadata>> ExtractAsync(
        string text,
        string fileName,int pageCount,
        CancellationToken ct = default);
}
