using System.Diagnostics;
using Backend.Models;

namespace Backend.Agents;

/// <summary>
/// Validation Agent – stub implementation.
/// Enforces submission business rules (decoupled from content safety and plagiarism):
///   • Page count: minimum 8, maximum 25
///   • Required sections: Title, Abstract, Keywords, Authors, References
/// TODO: Replace section detection with SK Semantic Function / Native Function
/// that performs semantic search for section headers within extracted text.
/// </summary>
public class ValidationAgent : IValidationAgent
{
    private const int MinPages = 8;
    private const int MaxPages = 25;

    private readonly ILogger<ValidationAgent> _logger;

    public ValidationAgent(ILogger<ValidationAgent> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult<ValidationResult>> ValidateAsync(
        DocumentMetadata metadata,
        string text,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var issues = new List<string>();
            var missingSections = new List<string>();

            if (metadata == null)
            {
                return new StepResult<ValidationResult>(
                    false,
                    null,
                    "Metadata is null",
                    sw.ElapsedMilliseconds);
            }

            // ---------------------------
            // 1. PAGE COUNT VALIDATION
            // ---------------------------

            bool pageCountValid = true;

            if (metadata.PageCount > 0)
            {
                pageCountValid =
                    metadata.PageCount >= MinPages &&
                    metadata.PageCount <= MaxPages;

                if (!pageCountValid)
                {
                    issues.Add(
                        $"Page count {metadata.PageCount} is outside allowed range ({MinPages}-{MaxPages}).");
                }
            }


            if (!pageCountValid)
            {
                issues.Add(
                    $"Page count {metadata.PageCount} is outside allowed range ({MinPages}-{MaxPages}).");
            }

            // ---------------------------
            // 2. METADATA VALIDATION
            // ---------------------------

            if (string.IsNullOrWhiteSpace(metadata.Title))
            {
                missingSections.Add("Title");
                issues.Add("Title is missing.");
            }

            if (metadata.Authors == null || metadata.Authors.Count == 0)
            {
                missingSections.Add("Authors");
                issues.Add("Authors section missing.");
            }

            if (string.IsNullOrWhiteSpace(metadata.Abstract))
            {
                missingSections.Add("Abstract");
                issues.Add("Abstract section missing.");
            }

            bool keywordsMissing =
              metadata.Keywords == null ||
              metadata.Keywords.Count == 0;

            if (keywordsMissing && !ContainsKeywordSection(text))
            {
                missingSections.Add("Keywords");
                issues.Add("Keywords section missing.");
            }

            // ---------------------------
            // 3. TEXT SECTION DETECTION
            // ---------------------------

            if (!ContainsSection(text, "References"))
            {
                missingSections.Add("References");
                issues.Add("References section not detected.");
            }

            if (!ContainsSection(text, "Introduction"))
            {
                issues.Add("Introduction section not detected.");
            }

            // ---------------------------
            // 4. BASIC CONTENT LENGTH CHECK
            // ---------------------------

            if (text.Length < 2000)
            {
                issues.Add("Document text too short for a research paper.");
            }

            // ---------------------------
            // FINAL RESULT
            // ---------------------------

            bool isValid = issues.Count == 0;

            var result = new ValidationResult(
                IsValid: isValid,
                PageCount: metadata.PageCount,
                IsPageCountCompliant: pageCountValid,
                MissingSections: missingSections,
                ValidationIssues: issues
            );

            sw.Stop();

            _logger.LogInformation(
                "[ValidationAgent] Validation completed: Valid={Valid}, Issues={IssueCount}, Time={Time}ms",
                isValid,
                issues.Count,
                sw.ElapsedMilliseconds);

            return new StepResult<ValidationResult>(
                isValid,
                result,
                "Validation completed",
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();

            _logger.LogError(ex, "[ValidationAgent] Validation failed");

            return new StepResult<ValidationResult>(
                false,
                null,
                ex.Message,
                sw.ElapsedMilliseconds);
        }
    }

    // ------------------------------------
    // HELPER: Section Detection
    // ------------------------------------
    private bool ContainsKeywordSection(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var patterns = new[]
        {
        @"\bKeywords\b",
        @"\bKEYWORDS\b",
        @"\bIndex Terms\b",
        @"\bKey Terms\b"
    };

        foreach (var p in patterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                text,
                p,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    private bool ContainsSection(string text, string section)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var patterns = new Dictionary<string, string[]>
        {
            ["References"] = new[]
            {
            @"\bReferences\b",
            @"\bREFERENCE\b",
            @"\bBibliography\b"
        },
            ["Introduction"] = new[]
            {
            @"\bIntroduction\b",
            @"\bINTRODUCTION\b"
        }
        };

        if (!patterns.ContainsKey(section))
            return false;

        foreach (var pattern in patterns[section])
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                text,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

}
