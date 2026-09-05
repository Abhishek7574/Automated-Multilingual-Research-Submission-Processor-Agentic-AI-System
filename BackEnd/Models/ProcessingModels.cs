namespace Backend.Models;

// ── Upload / Pipeline request ─────────────────────────────────────────────

public record DocumentUploadResponse(
    string DocumentId,
    string FileName,
    long FileSizeBytes,
    string Message
);

// ── Shared pipeline result wrapper ───────────────────────────────────────

public record StepResult<T>(
    bool Success,
    T? Data,
    string? Error = null,
    long ElapsedMs = 0
);

// ── 1. Ingestion Agent ────────────────────────────────────────────────────
// Simulates reading a submission from a file-system watch folder (email inbox).

public record IngestionResult(
    string DocumentId,
    string FilePath,
    string FileName,
    long FileSizeBytes,
    string FileType,
    string SimulatedSender,
    string SimulatedSubject,
    DateTime ReceivedAt
);

// ── 2. Pre-process Agent ──────────────────────────────────────────────────
// File type validation + OCR for scanned/image docs + language detection.

public record PreProcessResult(
    bool IsValidFileType,
    string DetectedFileType,
    bool OcrApplied,
    string ExtractedText,
    string PrimaryLanguage,
    string LanguageCode,
    double LanguageConfidence,
    int pageCount
);

// ── 3. Translation Agent ──────────────────────────────────────────────────
// Translates to English; stores both original and translated text.

public record TranslationResult(
    string OriginalText,
    string TranslatedText,
    string SourceLanguage,
    string SourceLanguageName,
    bool WasTranslated
);

// ── 4. Extraction Agent ───────────────────────────────────────────────────
// Extracts structured fields: title, authors, affiliations, abstract,
// keywords, figures and page count.

public record DocumentMetadata(
    string Title,
    List<string> Authors,
    List<string> Affiliations,
    string Abstract,
    List<string> Keywords,
    List<string> Figures,
    int PageCount,
    string Format
);

// ── 5. Validation Agent ───────────────────────────────────────────────────
// Business rules: page count 8–25, required sections present.
// Decoupled from ContentSafetyAgent and PlagiarismDetectionAgent.

public record ValidationResult(
    bool IsValid,
    int PageCount,
    bool IsPageCountCompliant,
    List<string> MissingSections,
    List<string> ValidationIssues
);

// ── 6. Content Safety Agent ───────────────────────────────────────────────
// Toxicity, hate speech, illicit content check. Flags for HITL.

public enum SafetyCategory { None, HateSpeech, Violence, SexualContent, SelfHarm }

public record ContentSafetyResult(
    bool IsSafe,
    List<SafetyFlag> Flags,
    string OverallRating
);

public record SafetyFlag(SafetyCategory Category, double Severity, string Detail);

// ── 7. Plagiarism Detection Agent ─────────────────────────────────────────
// Cross-references against academic databases. Decoupled from Validation.

public record PlagiarismResult(
    double SimilarityPercent,
    bool PlagiarismDetected,
    List<PlagiarismMatch> Matches
);

public record PlagiarismMatch(
    string Source,
    double Similarity,
    string MatchedText
);

// ── 8. RAG Agent ──────────────────────────────────────────────────────────
// Generates embeddings and maintains the vector store.

public record RagIndexResult(
    string IndexId,
    int ChunksIndexed,
    int TotalTokens,
    string VectorStore
);

// ── 9. Summary Agent ──────────────────────────────────────────────────────
// ≤250-word human-readable report: key findings, validation issues, missing sections.

public record SummarizationResult(
    string Summary,
    List<string> KeyFindings,
    List<string> MissingSections,
    List<string> ValidationIssues,
    List<string> Topics,
    string Methodology
);

// ── 10. Q&A Agent ─────────────────────────────────────────────────────────
// Conversational queries with multilingual support and chat history.

public record QnAReadyResult(
    bool IsReady,
    string IndexId,
    string Endpoint
);

public record QnARequest(string DocumentId, string Question, string? SessionId = null);

public record QnAResponse(
    string Question,
    string Answer,
    List<string> Sources,
    double Confidence,
    string? SessionId = null
);

// ── 11. Human Feedback Agent ──────────────────────────────────────────────
// HITL: flags items where confidence < 25%; accepts admin corrections.

public record HumanFeedbackResult(
    bool RequiresHumanReview,
    double OverallConfidence,
    List<FlaggedItem> FlaggedItems,
    bool IsResolved
);

public record FlaggedItem(
    string Field,
    string AgentResult,
    double Confidence,
    string? HumanCorrection
);

/// <summary>
/// Lightweight summary of key pipeline outputs passed to HumanFeedbackAgent.
/// </summary>
public record PipelineStepSummary(
    bool ContentSafetyPassed,
    double PlagiarismSimilarityPercent,
    List<string> ValidationIssues,
    double ExtractionConfidence
);

// ── Admin / HITL request models ───────────────────────────────────────────────

public record HitlCorrectionRequest(string Field, string Correction);

// ── Admin review decision ─────────────────────────────────────────────────────
// Recorded when an admin explicitly approves or rejects a flagged document.

public record ReviewRequest(bool Approved, string? RejectionReason = null);

public record ReviewDecision(
    string DocumentId,
    bool Approved,
    string? RejectionReason,
    string ReviewedBy,
    DateTime DecidedAt
);


public record AuditLogEntry(
    string Id,
    string DocumentId,
    string Action,
    string Actor,           // "system" | "admin" | "user"
    string? Details,
    DateTime Timestamp
);

// ── Full Pipeline Result ──────────────────────────────────────────────────

public record PipelineResult(
    string DocumentId,
    string FileName,
    bool OverallSuccess,
    // 11 agent steps in order
    StepResult<IngestionResult> Ingestion,
    StepResult<PreProcessResult> PreProcess,
    StepResult<TranslationResult> Translation,
    StepResult<DocumentMetadata> Extraction,
    StepResult<ValidationResult> Validation,
    StepResult<ContentSafetyResult> ContentSafety,
    StepResult<PlagiarismResult> Plagiarism,
    StepResult<RagIndexResult> RagIndex,
    StepResult<SummarizationResult> Summarization,
    StepResult<QnAReadyResult> QnA,
    StepResult<HumanFeedbackResult> HumanFeedback,
    long TotalElapsedMs
);
