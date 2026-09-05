using System.Diagnostics;
using Backend.Agents;
using Backend.Models;
using Backend.Storage;

namespace Backend.Pipeline;

/// <summary>
/// Orchestrates the full 11-agent document processing pipeline.
///
/// Agent order (per capstone requirements):
///   1. Ingestion Agent       – simulates email inbox via file-system read
///   2. Pre-process Agent     – file type validation, OCR, language detection
///   3. Translation Agent     – translate to English; store both copies
///   4. Extraction Agent      – extract title, authors, affiliations, abstract, keywords, figures
///   5. Validation Agent      – business rules (page count 8-25, required sections)
///   6. Content Safety Agent  – toxicity / illicit content (decoupled from Validation)
///   7. Plagiarism Detection Agent – similarity check (decoupled from Validation)
///   8. RAG Agent             – chunk, embed, upsert into vector store
///   9. Summary Agent         – ≤250-word human-readable summary
///  10. Q&A Agent             – prepare conversational Q&A with chat history
///  11. Human Feedback Agent  – HITL: flag items with confidence &lt; 25 %
///
/// The pipeline is fail-forward: a failing step is recorded but processing continues.
/// TODO: Replace sequential runner with SK Process Framework (KernelProcess / KernelProcessStep).
/// </summary>
public class DocumentPipelineOrchestrator
{
    private readonly IIngestionAgent _ingestionAgent;
    private readonly IPreProcessAgent _preProcessAgent;
    private readonly ITranslationAgent _translationAgent;
    private readonly IExtractionAgent _extractionAgent;
    private readonly IValidationAgent _validationAgent;
    private readonly IContentSafetyAgent _contentSafetyAgent;
    private readonly IPlagiarismDetectionAgent _plagiarismAgent;
    private readonly IRagAgent _ragAgent;
    private readonly ISummaryAgent _summaryAgent;
    private readonly IQnAAgent _qnAAgent;
    private readonly IHumanFeedbackAgent _humanFeedbackAgent;
    private readonly IDocumentStore _store;
    private readonly ILogger<DocumentPipelineOrchestrator> _logger;

    public DocumentPipelineOrchestrator(
        IIngestionAgent ingestionAgent,
        IPreProcessAgent preProcessAgent,
        ITranslationAgent translationAgent,
        IExtractionAgent extractionAgent,
        IValidationAgent validationAgent,
        IContentSafetyAgent contentSafetyAgent,
        IPlagiarismDetectionAgent plagiarismAgent,
        IRagAgent ragAgent,
        ISummaryAgent summaryAgent,
        IQnAAgent qnAAgent,
        IHumanFeedbackAgent humanFeedbackAgent,
        IDocumentStore store,
        ILogger<DocumentPipelineOrchestrator> logger)
    {
        _ingestionAgent = ingestionAgent;
        _preProcessAgent = preProcessAgent;
        _translationAgent = translationAgent;
        _extractionAgent = extractionAgent;
        _validationAgent = validationAgent;
        _contentSafetyAgent = contentSafetyAgent;
        _plagiarismAgent = plagiarismAgent;
        _ragAgent = ragAgent;
        _summaryAgent = summaryAgent;
        _qnAAgent = qnAAgent;
        _humanFeedbackAgent = humanFeedbackAgent;
        _store = store;
        _logger = logger;
    }

    public async Task<PipelineResult> RunAsync(
        string documentId,
        string fileName,
        Stream fileStream,
        CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();
        _logger.LogInformation("=== Pipeline START  |  Document: '{DocumentId}'  |  File: '{FileName}' ===",
            documentId, fileName);

        // Buffer the upload stream once so multiple agents can each get a fresh
        // MemoryStream over the same bytes without consuming the original.
        byte[] fileBytes;
        using (var buffer = new MemoryStream())
        {
            await fileStream.CopyToAsync(buffer, ct);
            fileBytes = buffer.ToArray();
        }

        // ── Step 1: Ingestion Agent ────────────────────────────────────
        // Simulates reading the submission from the email inbox (file system watch folder).
        // When called via HTTP upload the file path is recorded as the temp stream source.
        var ingestionResult = await SafeRunAsync(
            "Ingestion Agent",
            () => _ingestionAgent.IngestAsync(fileName, ct));

        // ── Step 2: Pre-process Agent ──────────────────────────────────
        // Validates file type, runs OCR on scanned documents, detects language.
        var preProcessResult = await SafeRunAsync(
            "Pre-process Agent",
            () => _preProcessAgent.PreProcessAsync(new MemoryStream(fileBytes), fileName, ct));

        // Resolved text: use OCR/extracted text from pre-process if available
        var rawText = preProcessResult.Data?.ExtractedText
            ?? $"{Path.GetFileNameWithoutExtension(fileName)} extracted content";
        var langCode = preProcessResult.Data?.LanguageCode ?? "en";

        // ── Step 3: Translation Agent ──────────────────────────────────
        // Translates to English when source language is not English.
        // Both original and English versions are stored in TranslationResult.
        var translationResult = await SafeRunAsync(
            "Translation Agent",
            () => _translationAgent.TranslateAsync(rawText, langCode, ct));

        // Use translated text for all downstream agents
        var workingText = translationResult.Data?.TranslatedText ?? rawText;

        // ── Step 4: Extraction Agent ───────────────────────────────────
        // Extracts title, authors, affiliations, abstract, keywords, figures.
        var extractionResult = await SafeRunAsync(
            "Extraction Agent",
            () => _extractionAgent.ExtractAsync(workingText, fileName, preProcessResult.Data?.pageCount ?? 0,ct));

        var metadata = extractionResult.Data;

        // Build text corpus for downstream agents (title + abstract + keywords)
        var textCorpus = metadata is not null
            ? $"{metadata.Title} {metadata.Abstract} {string.Join(' ', metadata.Keywords)} {workingText}"
            : workingText;

        // ── Step 5: Validation Agent ───────────────────────────────────
        // Enforces business rules: page count 8-25, required sections present.
        // Decoupled from ContentSafety and Plagiarism.
        var validationResult = await SafeRunAsync(
            "Validation Agent",
            () => _validationAgent.ValidateAsync(
                metadata ?? new DocumentMetadata("Unknown", [], [], "", [], [], 0, "Unknown"),
                workingText, ct));

        // ── Step 6: Content Safety Agent ──────────────────────────────
        // Checks for toxicity, hate speech, illicit content.
        // Flags document for human review if violations found.
        var safetyResult = await SafeRunAsync(
            "Content Safety Agent",
            () => _contentSafetyAgent.CheckAsync(textCorpus, ct));

        // ── Step 7: Plagiarism Detection Agent ─────────────────────────
        // Cross-references against academic databases.
        var plagiarismResult = await SafeRunAsync(
            "Plagiarism Detection Agent",
            () => _plagiarismAgent.DetectAsync(textCorpus, ct));

        // ── Step 8: RAG Agent ──────────────────────────────────────────
        // Generates embeddings and upserts chunks into the vector store.
        var ragResult = await SafeRunAsync(
            "RAG Agent",
            () => _ragAgent.IndexAsync(documentId, textCorpus, ct));

        // ── Step 9: Summary Agent ──────────────────────────────────────
        // Generates ≤250-word structured summary with key findings,
        // validation issues and missing sections.
        var summaryResult = await SafeRunAsync(
            "Summary Agent",
            () => _summaryAgent.SummarizeAsync(textCorpus, ct));

        // ── Step 10: Q&A Agent ─────────────────────────────────────────
        // Prepares conversational Q&A backed by the RAG index.
        var indexId = ragResult.Data?.IndexId ?? $"idx-{documentId}";
        var qnaResult = await SafeRunAsync(
            "Q&A Agent",
            () => _qnAAgent.PrepareAsync(documentId, indexId, ct));

        // ── Step 11: Human Feedback Agent ─────────────────────────────
        // Evaluates overall confidence; flags for admin HITL if any step
        // confidence < 25 % or if safety / plagiarism / validation issues exist.
        var stepSummary = new PipelineStepSummary(
            ContentSafetyPassed: safetyResult.Data?.IsSafe ?? true,
            PlagiarismSimilarityPercent: plagiarismResult.Data?.SimilarityPercent ?? 0,
            ValidationIssues: validationResult.Data?.ValidationIssues ?? [],
            ExtractionConfidence: extractionResult.Success ? 0.90 : 0.10   // TODO: real confidence from SK
        );
        var humanFeedbackResult = await SafeRunAsync(
            "Human Feedback Agent",
            () => _humanFeedbackAgent.EvaluateAsync(documentId, stepSummary, ct));

        totalSw.Stop();

        var overallSuccess =
            ingestionResult.Success &&
            preProcessResult.Success &&
            translationResult.Success &&
            extractionResult.Success &&
            validationResult.Success &&
            safetyResult.Success &&
            plagiarismResult.Success &&
            ragResult.Success &&
            summaryResult.Success &&
            qnaResult.Success &&
            humanFeedbackResult.Success;

        _logger.LogInformation(
            "=== Pipeline {Status}  |  '{DocumentId}'  |  {Ms}ms total ===",
            overallSuccess ? "COMPLETE" : "COMPLETE WITH ERRORS",
            documentId, totalSw.ElapsedMilliseconds);

        var pipelineResult = new PipelineResult(
            DocumentId: documentId,
            FileName: fileName,
            OverallSuccess: overallSuccess,
            Ingestion: ingestionResult,
            PreProcess: preProcessResult,
            Translation: translationResult,
            Extraction: extractionResult,
            Validation: validationResult,
            ContentSafety: safetyResult,
            Plagiarism: plagiarismResult,
            RagIndex: ragResult,
            Summarization: summaryResult,
            QnA: qnaResult,
            HumanFeedback: humanFeedbackResult,
            TotalElapsedMs: totalSw.ElapsedMilliseconds
        );

        // Persist result so admin endpoints can query it later
        _store.SaveResult(pipelineResult);

        // Append per-step audit entries
        var steps = new (string Name, bool Success, string? Error)[]
        {
            ("Ingestion Agent",            ingestionResult.Success,       ingestionResult.Error),
            ("Pre-process Agent",          preProcessResult.Success,      preProcessResult.Error),
            ("Translation Agent",          translationResult.Success,     translationResult.Error),
            ("Extraction Agent",           extractionResult.Success,      extractionResult.Error),
            ("Validation Agent",           validationResult.Success,      validationResult.Error),
            ("Content Safety Agent",       safetyResult.Success,          safetyResult.Error),
            ("Plagiarism Detection Agent", plagiarismResult.Success,      plagiarismResult.Error),
            ("RAG Agent",                  ragResult.Success,             ragResult.Error),
            ("Summary Agent",              summaryResult.Success,         summaryResult.Error),
            ("Q&A Agent",                  qnaResult.Success,             qnaResult.Error),
            ("Human Feedback Agent",       humanFeedbackResult.Success,   humanFeedbackResult.Error),
        };

        foreach (var (name, success, error) in steps)
        {
            _store.AddAuditEntry(new AuditLogEntry(
                Id: Guid.NewGuid().ToString("N")[..8],
                DocumentId: documentId,
                Action: $"{name} {(success ? "completed" : "failed")}",
                Actor: "system",
                Details: success ? null : error,
                Timestamp: DateTime.UtcNow));
        }

        _store.AddAuditEntry(new AuditLogEntry(
            Id: Guid.NewGuid().ToString("N")[..8],
            DocumentId: documentId,
            Action: overallSuccess ? "Pipeline completed" : "Pipeline completed with errors",
            Actor: "system",
            Details: $"{totalSw.ElapsedMilliseconds}ms total",
            Timestamp: DateTime.UtcNow));

        return pipelineResult;
    }

    /// <summary>Wraps a pipeline step – exceptions are captured rather than thrown.</summary>
    private async Task<StepResult<T>> SafeRunAsync<T>(string agentName, Func<Task<StepResult<T>>> step)
    {
        try
        {
            _logger.LogInformation("[{Agent}] Starting", agentName);
            var result = await step();
            _logger.LogInformation("[{Agent}] {Status} in {Ms}ms",
                agentName, result.Success ? "OK" : "FAILED", result.ElapsedMs);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Agent}] Exception: {Message}", agentName, ex.Message);
            return new StepResult<T>(false, default, Error: ex.Message);
        }
    }
}
