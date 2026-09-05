using Backend.Agents;
using Backend.Models;
using Backend.Pipeline;
using Backend.Storage;

namespace Backend.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents");

        // ── POST /api/documents/process ───────────────────────────────────────
        // Accepts a multipart/form-data upload with a single PDF file.
        // Runs the full 7-step pipeline and returns the aggregated result.
        group.MapPost("/process", async (
            IFormFile file,
            DocumentPipelineOrchestrator pipeline,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "No file uploaded." });

            var ext = Path.GetExtension(file.FileName).ToLower();

            if (ext != ".pdf" && ext != ".docx" && ext != ".doc")
            {
                return Results.BadRequest(new { error = "Only PDF, DOCX and DOC files are supported." });
            }


            var documentId = Guid.NewGuid().ToString("N")[..12];

            await using var stream = file.OpenReadStream();
            var result = await pipeline.RunAsync(documentId, file.FileName, stream, ct);

            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .WithName("ProcessDocument")
        .WithSummary("Upload a PDF and run the full AI processing pipeline");

        // ── GET /api/documents ─────────────────────────────────────────────────
        // Returns a summary list of all processed documents (newest first).
        group.MapGet("/", (IDocumentStore store) =>
        {
            var summaries = store.GetAllResults().Select(r => new
            {
                r.DocumentId,
                r.FileName,
                r.OverallSuccess,
                r.TotalElapsedMs,
                RequiresReview = r.HumanFeedback.Data?.RequiresHumanReview ?? false,
                IsResolved = r.HumanFeedback.Data?.IsResolved ?? false,
                OverallConfidence = r.HumanFeedback.Data?.OverallConfidence ?? 1.0,
                FlaggedItems = r.HumanFeedback.Data?.FlaggedItems ?? [],
                ProcessedAt = r.Ingestion.Data?.ReceivedAt ?? DateTime.UtcNow,
                ReviewDecision = store.GetReviewDecision(r.DocumentId),
            });
            return Results.Ok(summaries);
        })
        .WithName("ListDocuments")
        .WithSummary("List all processed documents with their pipeline status");

        // ── POST /api/documents/{documentId}/review ───────────────────────────
        // Admin submits an Approve or Reject decision for a flagged document.
        group.MapPost("/{documentId}/review", (
            string documentId,
            ReviewRequest body,
            IDocumentStore store) =>
        {
            if (store.GetResult(documentId) is null)
                return Results.NotFound(new { error = $"Document '{documentId}' not found." });

            if (!body.Approved && string.IsNullOrWhiteSpace(body.RejectionReason))
                return Results.BadRequest(new { error = "RejectionReason is required when rejecting." });

            var decision = new ReviewDecision(
                DocumentId: documentId,
                Approved: body.Approved,
                RejectionReason: body.RejectionReason,
                ReviewedBy: "admin",
                DecidedAt: DateTime.UtcNow);

            store.SaveReviewDecision(decision);

            store.AddAuditEntry(new AuditLogEntry(
                Id: Guid.NewGuid().ToString("N")[..8],
                DocumentId: documentId,
                Action: body.Approved ? "Document approved" : "Document rejected",
                Actor: "admin",
                Details: body.Approved ? null : body.RejectionReason,
                Timestamp: DateTime.UtcNow));

            return Results.Ok(decision);
        })
        .WithName("ReviewDocument")
        .WithSummary("Admin approves or rejects a flagged document");


        // ── GET /api/documents/{documentId} ───────────────────────────────────
        // Returns the full pipeline result for a single document.
        group.MapGet("/{documentId}", (string documentId, IDocumentStore store) =>
        {
            var result = store.GetResult(documentId);
            return result is null
                ? Results.NotFound(new { error = $"Document '{documentId}' not found." })
                : Results.Ok(result);
        })
        .WithName("GetDocument")
        .WithSummary("Get full pipeline result for a document");

        // ── GET /api/documents/{documentId}/audit ─────────────────────────────
        // Returns the audit log for a specific document.
        group.MapGet("/{documentId}/audit", (string documentId, IDocumentStore store) =>
            Results.Ok(store.GetAuditLog(documentId)))
        .WithName("GetDocumentAuditLog")
        .WithSummary("Get audit log entries for a document");

        // ── GET /api/documents/audit ──────────────────────────────────────────
        // Returns the full audit log across all documents (newest first).
        group.MapGet("/audit", (IDocumentStore store) =>
            Results.Ok(store.GetAuditLog()))
        .WithName("GetAllAuditLog")
        .WithSummary("Get the full audit log across all documents");

        // ── GET /api/documents/pipeline-steps ─────────────────────────────────
        // Returns metadata about each pipeline step — useful for frontend display.
        group.MapGet("/pipeline-steps", () => Results.Ok(new[]
        {
            new { id =  1, name = "Ingestion Agent",            icon = "📥", description = "Simulates email inbox monitoring by reading submissions from the file-system watch folder." },
            new { id =  2, name = "Pre-process Agent",          icon = "🔄", description = "Validates file type, runs OCR on scanned documents, and detects the primary language." },
            new { id =  3, name = "Translation Agent",          icon = "🌍", description = "Translates non-English submissions to English; stores original and translated text." },
            new { id =  4, name = "Extraction Agent",           icon = "🧠", description = "Extracts title, authors, affiliations, abstract, keywords and figures." },
            new { id =  5, name = "Validation Agent",           icon = "✔️",  description = "Enforces business rules: page count 8-25, required sections present (title, abstract, keywords, authors, references)." },
            new { id =  6, name = "Content Safety Agent",       icon = "🛡️", description = "Scans for toxicity, hate speech and illicit content; flags for human review when violations detected." },
            new { id =  7, name = "Plagiarism Detection Agent", icon = "🔍", description = "Cross-references against academic databases for similarity; flags if > 25 %." },
            new { id =  8, name = "RAG Agent",                  icon = "📚", description = "Generates embeddings and maintains the vector store for retrieval, augmentation and generation." },
            new { id =  9, name = "Summary Agent",              icon = "✨",  description = "Produces a ≤250-word summary highlighting key findings, validation issues and missing sections." },
            new { id = 10, name = "Q&A Agent",                  icon = "💬", description = "Enables multilingual conversational Q&A on the document with full chat history." },
            new { id = 11, name = "Human Feedback Agent",       icon = "👤", description = "Presents flagged items to admin for HITL review; accepts corrections when confidence < 25 %." },
        }))
        .WithName("GetPipelineSteps")
        .WithSummary("List all pipeline steps with metadata");

        // ── POST /api/documents/{documentId}/ask ──────────────────────────────
        // Ask a question against an already-processed document.
        group.MapPost("/{documentId}/ask", async (
     string documentId,
     QnARequest body,
     IQnAAgent qnaAgent,
     CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Question))
                return Results.BadRequest(new { error = "Question cannot be empty." });

            var response = await qnaAgent.AskAsync(
                new QnARequest(documentId, body.Question, body.SessionId),
                ct);

            return Results.Ok(response);
        })
        .WithName("AskQuestion")
        .WithSummary("Ask a natural language question against a processed document");

        // ── POST /api/documents/{documentId}/correct ──────────────────────────
        // Admin submits a correction for a flagged HITL item.
        group.MapPost("/{documentId}/correct", async (
            string documentId,
            HitlCorrectionRequest body,
            IHumanFeedbackAgent humanFeedbackAgent,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Field) || string.IsNullOrWhiteSpace(body.Correction))
                return Results.BadRequest(new { error = "Field and Correction are required." });

            await humanFeedbackAgent.ApplyCorrectionAsync(documentId, body.Field, body.Correction, ct);

            return Results.Ok(new { documentId, body.Field, body.Correction, appliedAt = DateTime.UtcNow });
        })
        .WithName("SubmitHitlCorrection")
        .WithTags("Documents")
        .WithSummary("Admin submits a Human-In-The-Loop correction for a flagged item");

        return app;
    }
}
