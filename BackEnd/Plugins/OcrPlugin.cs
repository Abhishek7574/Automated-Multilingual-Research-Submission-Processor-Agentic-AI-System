using DocumentFormat.OpenXml.Packaging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;
using Tesseract;
using UglyToad.PdfPig;

namespace Backend.Plugins;

/// <summary>
/// Semantic Kernel plugin that extracts plain text from uploaded files.
/// - PDFs: uses PdfPig to extract the text layer directly (works for all
///         standard research-paper PDFs; scanned-image PDFs yield empty text).
/// - Images: uses Tesseract OCR (PNG, JPEG, TIFF, BMP, WebP …).
/// </summary>
[Description(
    "Extracts plain text from PDF or image files. " +
    "PDF files are handled via PdfPig text extraction; " +
    "image files are processed with Tesseract OCR.")]
public sealed class OcrPlugin
{
    private static readonly HashSet<string> ImageExtensions =
       new(StringComparer.OrdinalIgnoreCase)
       { "png", "jpg", "jpeg", "tif", "tiff", "bmp", "gif", "webp" };

    private readonly ILogger<OcrPlugin> _logger;
    private readonly string _tessdataPath;
    private readonly string _language;

    public OcrPlugin(ILogger<OcrPlugin> logger, IConfiguration config)
    {
        _logger = logger;
        _tessdataPath = config["Tesseract:TessdataPath"] ?? "tessdata";
        _language = config["Tesseract:Language"] ?? "eng";
    }

    [KernelFunction]
    [Description("Extract text from file bytes")]
    public string ExtractTextFromFile(byte[] fileBytes, string fileName)
    {
        try
        {
            var ext = Path.GetExtension(fileName).TrimStart('.').ToLower();

            return ext switch
            {
                "pdf" => ExtractPdf(fileBytes),
                "docx" => ExtractDocx(fileBytes),
                "doc" => ExtractDoc(fileBytes),
                _ when ImageExtensions.Contains(ext) => ExtractImage(fileBytes),
                _ => ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Text extraction failed");
            return "";
        }
    }

    // ---------------- PDF ----------------
    private string ExtractPdf(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        using var pdf = PdfDocument.Open(stream);

        var sb = new StringBuilder();

        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);

        return sb.ToString();
    }

    // ---------------- DOCX ----------------
    private string ExtractDocx(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        using var doc = WordprocessingDocument.Open(stream, false);

        var body = doc.MainDocumentPart?.Document.Body;

        return body?.InnerText ?? "";
    }

    // ---------------- DOC ----------------
    // DOC is old format — simple fallback
    private string ExtractDoc(byte[] fileBytes)
    {
        return Encoding.UTF8.GetString(fileBytes);
    }

    // ---------------- IMAGE OCR ----------------
    private string ExtractImage(byte[] fileBytes)
    {
        using var engine = new TesseractEngine(_tessdataPath, _language, EngineMode.Default);

        using var img = Pix.LoadFromMemory(fileBytes);
        using var page = engine.Process(img);

        return page.GetText();
    }
}
