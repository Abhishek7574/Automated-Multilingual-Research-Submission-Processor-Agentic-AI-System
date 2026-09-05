using Azure.AI.TextAnalytics;
using Backend.Models;
using Microsoft.SemanticKernel;
using System.Diagnostics;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace Backend.Agents;

/// <summary>
/// Pre-process Agent – validates file type and invokes the SK OcrPlugin.
/// TODO: Add Azure AI Language for language detection.
/// </summary>
public class PreProcessAgent : IPreProcessAgent
{
    private readonly ILogger<PreProcessAgent> _logger;
    private readonly Kernel _kernel;
    private readonly TextAnalyticsClient _textClient;

    public PreProcessAgent(ILogger<PreProcessAgent> logger, Kernel kernel, TextAnalyticsClient textClient)
    {
        _logger = logger;
        _kernel = kernel;
        _textClient = textClient;
    }

    private string ExtractDocxText(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        using var doc = WordprocessingDocument.Open(stream, false);

        var body = doc.MainDocumentPart?.Document.Body;

        if (body == null)
            return "";

        StringBuilder sb = new();

        foreach (var text in body.Descendants<Text>())
        {
            sb.Append(text.Text);
            sb.Append(" ");
        }

        return sb.ToString();
    }

    public async Task<StepResult<PreProcessResult>> PreProcessAsync(
        Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[PreProcessAgent] Processing '{FileName}'", fileName);


        // 1. Extract bytes for OCR
        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await fileStream.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
        }

        string extractedText = "";

        var ext1 = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();

        if (ext1 == "DOCX")
        {
            extractedText = ExtractDocxText(fileBytes);
        }
        else
        {
            // Use OCR for PDF or images
            var ocrResult = await _kernel.InvokeAsync<string>(
                "OcrPlugin",
                "ExtractTextFromFile",
                new KernelArguments
                {
                    ["fileBytes"] = fileBytes,
                    ["fileName"] = fileName
                },
                ct);

            extractedText = ocrResult ?? string.Empty;
        }


        // 2. Invoke SK OcrPlugin (Uses Tesseract)
        //var ocrResult = await _kernel.InvokeAsync<string>(
        //    "OcrPlugin", "ExtractTextFromFile",
        //    new KernelArguments { ["fileBytes"] = fileBytes, ["fileName"] = fileName },
        //    ct);

        //var extractedText = ocrResult ?? string.Empty;

        // 3. Azure AI Language Detection
        string languageName = "Unknown";
        string languageCode = "und";
        double confidence = 0.0;

        if (!string.IsNullOrWhiteSpace(extractedText))
        {
            try
            {
                // TRUNCATE TEXT: Azure has a 5,120 character limit for language detection
                // We take the first 3000 characters which is more than enough for detection
                string textToAnalyze = extractedText.Length > 3000
                    ? extractedText.Substring(0, 3000)
                    : extractedText;

                DetectedLanguage detected = await _textClient.DetectLanguageAsync(textToAnalyze, cancellationToken: ct);

                languageName = detected.Name;
                languageCode = detected.Iso6391Name;
                confidence = detected.ConfidenceScore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PreProcessAgent] Language detection failed.");
                // Fallback defaults are already set above
            }
        }

        var ext = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
        var isValidType = ext is "PDF" or "DOCX" or "DOC";


        int pageCount = 0;

        try
        {
            if (ext == "PDF")
            {
                using var pdf = PdfDocument.Open(new MemoryStream(fileBytes));
                pageCount = pdf.NumberOfPages;
            }
            else if (ext == "DOCX")
            {
                using var doc = WordprocessingDocument.Open(new MemoryStream(fileBytes), false);

                if (doc.ExtendedFilePropertiesPart?.Properties?.Pages?.Text != null)
                {
                    pageCount = int.Parse(doc.ExtendedFilePropertiesPart.Properties.Pages.Text);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PreProcessAgent] Could not determine page count");
        }

        var result = new PreProcessResult(
            IsValidFileType: isValidType,
            DetectedFileType: ext,
            OcrApplied: !string.IsNullOrEmpty(extractedText),
            ExtractedText: extractedText,
            PrimaryLanguage: languageName,
            LanguageCode: languageCode,
            LanguageConfidence: confidence,
            pageCount: pageCount
        );

        sw.Stop();
        _logger.LogInformation(
            "[PreProcessAgent] Completed: Language={Lang} ({Conf}), {Ms}ms",
            result.PrimaryLanguage, result.LanguageConfidence, sw.ElapsedMilliseconds);

        return new StepResult<PreProcessResult>(true, result, ElapsedMs: sw.ElapsedMilliseconds);
    }
}
