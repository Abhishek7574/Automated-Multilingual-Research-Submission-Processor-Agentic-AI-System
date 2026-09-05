using System.Diagnostics;
using Backend.Models;
using Backend.Storage;

namespace Backend.Agents;

/// <summary>
/// Human Feedback Agent – stub implementation.
/// Evaluates overall pipeline confidence. If any step confidence is below 25 %,
/// or if content safety / plagiarism / validation flags are raised, the document
/// is flagged for admin review. Admins can then supply corrections which are
/// stored and used to improve future extractions.
/// TODO: Persist corrections to a durable store (e.g. Azure Table Storage / Cosmos DB)
/// and feed them back into SK Memory for continuous learning.
/// </summary>
public class HumanFeedbackAgent : IHumanFeedbackAgent
{
    private const double HitlConfidenceThreshold = 0.25;

    private readonly IDocumentStore _store;
    private readonly ILogger<HumanFeedbackAgent> _logger;

    public HumanFeedbackAgent(
        IDocumentStore store,
        ILogger<HumanFeedbackAgent> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task<StepResult<HumanFeedbackResult>> EvaluateAsync(
        string documentId,
        PipelineStepSummary summary,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "[HumanFeedbackAgent] Evaluating pipeline confidence for {DocumentId}",
            documentId);

        var flaggedItems = new List<FlaggedItem>();

        // ───────── Content Safety ─────────
        if (!summary.ContentSafetyPassed)
        {
            flaggedItems.Add(new FlaggedItem(
                "ContentSafety",
                "Content safety violation detected",
                0.50,
                null));
        }

        // ───────── Plagiarism ─────────
        if (summary.PlagiarismSimilarityPercent > 25)
        {
            var confidence = 1.0 - (summary.PlagiarismSimilarityPercent / 100.0);

            flaggedItems.Add(new FlaggedItem(
                "Plagiarism",
                $"Similarity {summary.PlagiarismSimilarityPercent:F1}% exceeds threshold",
                confidence,
                null));
        }

        // ───────── Validation ─────────
        foreach (var issue in summary.ValidationIssues)
        {
            flaggedItems.Add(new FlaggedItem(
                "Validation",
                issue,
                0.0,
                null));
        }

        // ───────── Extraction Confidence ─────────
        if (summary.ExtractionConfidence < HitlConfidenceThreshold)
        {
            flaggedItems.Add(new FlaggedItem(
                "Extraction",
                $"Extraction confidence {summary.ExtractionConfidence:P0} below threshold",
                summary.ExtractionConfidence,
                null));
        }

        // ───────── Overall Confidence ─────────
        double overallConfidence = flaggedItems.Count == 0
            ? 1.0
            : flaggedItems.Min(f => f.Confidence);

        bool requiresReview = flaggedItems.Count > 0;

        var result = new HumanFeedbackResult(
            RequiresHumanReview: requiresReview,
            OverallConfidence: overallConfidence,
            FlaggedItems: flaggedItems,
            IsResolved: false
        );

        sw.Stop();

        _logger.LogInformation(
            "[HumanFeedbackAgent] ReviewRequired={Review}, Flags={Flags}, Confidence={Confidence:P0}, {Ms}ms",
            requiresReview,
            flaggedItems.Count,
            overallConfidence,
            sw.ElapsedMilliseconds);

        return Task.FromResult(
            new StepResult<HumanFeedbackResult>(
                true,
                result,
                ElapsedMs: sw.ElapsedMilliseconds));
    }

    public Task ApplyCorrectionAsync(
        string documentId,
        string field,
        string correction,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[HumanFeedbackAgent] Applying admin correction for {DocumentId} field {Field}",
            documentId,
            field);

        _store.SaveCorrection(documentId, field, correction);

        _store.AddAuditEntry(new AuditLogEntry(
            Id: Guid.NewGuid().ToString("N")[..8],
            DocumentId: documentId,
            Action: $"HITL correction applied: {field}",
            Actor: "admin",
            Details: correction,
            Timestamp: DateTime.UtcNow));

        return Task.CompletedTask;
    }
}
