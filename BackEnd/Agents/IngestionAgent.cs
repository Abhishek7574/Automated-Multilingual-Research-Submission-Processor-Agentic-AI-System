using System.Diagnostics;
using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Ingestion Agent – stub implementation.
/// Simulates email inbox monitoring by watching a local folder on the file system.
/// TODO: Replace file-system polling with Microsoft Graph API / IMAP integration
/// to monitor a real research-submission mailbox.
/// </summary>
public class IngestionAgent : IIngestionAgent
{
    private readonly ILogger<IngestionAgent> _logger;
    private readonly IConfiguration _config;

    // Default watch folder – configurable via appsettings.json (Ingestion:WatchFolder)
    private string WatchFolder =>
        _config["Ingestion:WatchFolder"] ?? Path.Combine(Path.GetTempPath(), "amrsp-inbox");

    public IngestionAgent(ILogger<IngestionAgent> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task<StepResult<IngestionResult>> IngestAsync(
        string filePath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[IngestionAgent] Ingesting file '{FilePath}'", filePath);

        await Task.Delay(100, ct); // TODO: Graph API / IMAP fetch

        var fileInfo = new FileInfo(filePath);
        var result = new IngestionResult(
            DocumentId: Guid.NewGuid().ToString("N")[..12],
            FilePath: filePath,
            FileName: fileInfo.Name,
            FileSizeBytes: fileInfo.Exists ? fileInfo.Length : 0,
            FileType: fileInfo.Extension.TrimStart('.').ToUpperInvariant(),
            SimulatedSender: "researcher@university.edu",   // TODO: parse from email headers
            SimulatedSubject: $"Research Submission: {fileInfo.Name}",
            ReceivedAt: DateTime.UtcNow
        );

        sw.Stop();
        _logger.LogInformation(
            "[IngestionAgent] Ingested '{FileName}' ({Size} bytes) from '{Sender}' in {Ms}ms",
            result.FileName, result.FileSizeBytes, result.SimulatedSender, sw.ElapsedMilliseconds);

        return new StepResult<IngestionResult>(true, result, ElapsedMs: sw.ElapsedMilliseconds);
    }

    public async Task<IReadOnlyList<IngestionResult>> ListPendingAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[IngestionAgent] Scanning watch folder '{Folder}'", WatchFolder);

        await Task.Delay(50, ct); // TODO: poll inbox or filesystem

        if (!Directory.Exists(WatchFolder))
        {
            _logger.LogWarning("[IngestionAgent] Watch folder does not exist: {Folder}", WatchFolder);
            return [];
        }

        var files = Directory.GetFiles(WatchFolder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".doc", StringComparison.OrdinalIgnoreCase))
            .Select(f =>
            {
                var fi = new FileInfo(f);
                return new IngestionResult(
                    DocumentId: Guid.NewGuid().ToString("N")[..12],
                    FilePath: f,
                    FileName: fi.Name,
                    FileSizeBytes: fi.Length,
                    FileType: fi.Extension.TrimStart('.').ToUpperInvariant(),
                    SimulatedSender: "researcher@university.edu",
                    SimulatedSubject: $"Research Submission: {fi.Name}",
                    ReceivedAt: fi.CreationTimeUtc
                );
            })
            .ToList();

        _logger.LogInformation("[IngestionAgent] Found {Count} pending document(s)", files.Count);
        return files;
    }
}
