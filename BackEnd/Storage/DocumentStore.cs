using System.Collections.Concurrent;
using Backend.Models;

namespace Backend.Storage;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IDocumentStore"/>.
/// All state is held in four ConcurrentDictionary instances:
///   • _results      — full PipelineResult per documentId
///   • _auditLog     — list of AuditLogEntry per documentId
///   • _corrections  — list of FlaggedItem (admin corrections) per documentId
///   • _reviews      — admin approve/reject decision per documentId
///
/// Registered as a singleton in DI so all HTTP requests share the same collections.
/// TODO: Replace with EF Core + SQLite (AddDbContext, SaveChangesAsync) for persistence
///       between restarts, or Azure Cosmos DB for production deployment.
/// </summary>
public sealed class DocumentStore : IDocumentStore
{
    private readonly ConcurrentDictionary<string, PipelineResult> _results = new();
    private readonly ConcurrentDictionary<string, List<AuditLogEntry>> _auditLog = new();
    private readonly ConcurrentDictionary<string, List<FlaggedItem>> _corrections = new();
    private readonly ConcurrentDictionary<string, ReviewDecision> _reviews = new();

    // ── Pipeline results ─────────────────────────────────────────────────────

    public void SaveResult(PipelineResult result) =>
        _results[result.DocumentId] = result;

    public PipelineResult? GetResult(string documentId) =>
        _results.TryGetValue(documentId, out var r) ? r : null;

    public IReadOnlyList<PipelineResult> GetAllResults() =>
        [.. _results.Values.OrderByDescending(r =>
            r.Ingestion.Data?.ReceivedAt ?? DateTime.MinValue)];

    // ── Audit log ────────────────────────────────────────────────────────────

    public void AddAuditEntry(AuditLogEntry entry)
    {
        var list = _auditLog.GetOrAdd(entry.DocumentId, _ => []);
        lock (list) { list.Add(entry); }
    }

    public IReadOnlyList<AuditLogEntry> GetAuditLog(string? documentId = null)
    {
        if (documentId is not null)
        {
            return _auditLog.TryGetValue(documentId, out var list)
                ? [.. list.AsEnumerable().Reverse()]
                : [];
        }

        return [.. _auditLog.Values
            .SelectMany(l => l)
            .OrderByDescending(e => e.Timestamp)];
    }

    // ── HITL corrections ─────────────────────────────────────────────────────

    public void SaveCorrection(string documentId, string field, string correction)
    {
        var list = _corrections.GetOrAdd(documentId, _ => []);
        lock (list)
        {
            // Remove previous correction for the same field (admin override)
            list.RemoveAll(f => f.Field.Equals(field, StringComparison.OrdinalIgnoreCase));
            list.Add(new FlaggedItem(field, "Admin-corrected", 1.0, correction));
        }
    }

    public IReadOnlyList<FlaggedItem> GetCorrections(string documentId) =>
        _corrections.TryGetValue(documentId, out var list)
            ? [.. list]
            : [];

    // ── Admin review decisions ─────────────────────────────────────────────

    public void SaveReviewDecision(ReviewDecision decision) =>
        _reviews[decision.DocumentId] = decision;

    public ReviewDecision? GetReviewDecision(string documentId) =>
        _reviews.TryGetValue(documentId, out var r) ? r : null;
}
