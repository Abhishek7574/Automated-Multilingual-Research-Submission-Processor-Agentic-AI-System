using Backend.Models;

namespace Backend.Storage;

/// <summary>
/// Shared in-memory store for pipeline results, audit log entries and HITL corrections.
/// Registered as a singleton so all requests share the same state for the lifetime of the process.
/// TODO: Upgrade to EF Core + SQLite (or Azure Cosmos DB) for durable persistence.
/// </summary>
public interface IDocumentStore
{
    // ── Pipeline results ─────────────────────────────────────────────────────

    /// <summary>Persists the full pipeline result for a document.</summary>
    void SaveResult(PipelineResult result);

    /// <summary>Returns the pipeline result for a given document, or null if not found.</summary>
    PipelineResult? GetResult(string documentId);

    /// <summary>Returns all stored pipeline results, newest first.</summary>
    IReadOnlyList<PipelineResult> GetAllResults();

    // ── Audit log ────────────────────────────────────────────────────────────

    /// <summary>Appends an audit entry (scoped to a document or global).</summary>
    void AddAuditEntry(AuditLogEntry entry);

    /// <summary>
    /// Returns audit entries.
    /// If <paramref name="documentId"/> is provided, only entries for that document are returned.
    /// If null, all entries across all documents are returned (newest first).
    /// </summary>
    IReadOnlyList<AuditLogEntry> GetAuditLog(string? documentId = null);

    // ── HITL corrections ─────────────────────────────────────────────────────

    /// <summary>Persists an admin correction for a flagged field on a document.</summary>
    void SaveCorrection(string documentId, string field, string correction);

    /// <summary>Returns all corrections submitted for a document.</summary>
    IReadOnlyList<FlaggedItem> GetCorrections(string documentId);
    // ── Admin review decisions ────────────────────────────────────────────

    /// <summary>Persists an admin approve/reject decision for a flagged document.</summary>
    void SaveReviewDecision(ReviewDecision decision);

    /// <summary>Returns the admin review decision for a document, or null if not yet reviewed.</summary>
    ReviewDecision? GetReviewDecision(string documentId);
}
