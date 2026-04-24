using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace Dbiz.MailboxWorker.Services;

public sealed class AttachmentTextExtractor : IAttachmentTextExtractor
{
    public Task<(string Text, string Status)> ExtractTextAsync(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(filePath))
        {
            return Task.FromResult((string.Empty, "file-not-found"));
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            return extension switch
            {
                ".txt" or ".csv" or ".log" or ".json" or ".xml" => Task.FromResult((File.ReadAllText(filePath), "ok")),
                ".html" or ".htm" => Task.FromResult((HtmlToPlainText(File.ReadAllText(filePath)), "ok")),
                ".docx" => Task.FromResult((ReadDocx(filePath), "ok")),
                ".pdf" => Task.FromResult((ReadPdf(filePath), "ok")),
                _ => Task.FromResult((string.Empty, $"unsupported-extension:{extension}"))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult((string.Empty, $"extract-failed:{ex.GetType().Name}"));
        }
    }

    private static string ReadDocx(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document?.Body;
        return body?.InnerText ?? string.Empty;
    }

    private static string ReadPdf(string filePath)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var noScripts = Regex.Replace(html, "<(script|style)[^>]*>.*?</\\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var withLineBreaks = Regex.Replace(noScripts, "<(br|/p|/div|li|/tr|/h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        var noTags = Regex.Replace(withLineBreaks, "<[^>]+>", " ");

        return System.Net.WebUtility.HtmlDecode(noTags)
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Aggregate(new StringBuilder(), (sb, line) => sb.AppendLine(line), sb => sb.ToString())
            .Trim();
    }
}
