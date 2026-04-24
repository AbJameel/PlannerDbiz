using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PlannerApi.Services;

public static class UploadedDocumentTextExtractor
{
    public static string ExtractText(string fileName, byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

        return extension switch
        {
            ".docx" => ExtractDocxText(bytes),
            ".txt" or ".eml" or ".msg" => DecodeText(bytes),
            _ => DecodeText(bytes)
        };
    }

    private static string ExtractDocxText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var parts = new[]
        {
            "word/document.xml",
            "word/header1.xml",
            "word/header2.xml",
            "word/header3.xml",
            "word/footer1.xml",
            "word/footer2.xml",
            "word/footer3.xml"
        };

        var chunks = new List<string>();

        foreach (var part in parts)
        {
            var entry = archive.GetEntry(part);
            if (entry is null)
                continue;

            using var entryStream = entry.Open();
            var text = ExtractWordXmlText(entryStream);
            if (!string.IsNullOrWhiteSpace(text))
                chunks.Add(text.Trim());
        }

        if (chunks.Count == 0)
            return string.Empty;

        return NormalizeWhitespace(string.Join(Environment.NewLine + Environment.NewLine, chunks));
    }

    private static string ExtractWordXmlText(Stream xmlStream)
    {
        var doc = XDocument.Load(xmlStream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var sb = new StringBuilder();

        foreach (var node in doc.DescendantNodes())
        {
            if (node is not XElement el)
                continue;

            if (el.Name == w + "t")
            {
                sb.Append(el.Value);
            }
            else if (el.Name == w + "tab")
            {
                sb.Append('\t');
            }
            else if (el.Name == w + "br" || el.Name == w + "cr")
            {
                sb.AppendLine();
            }
            else if (el.Name == w + "p")
            {
                sb.AppendLine();
            }
            else if (el.Name == w + "tr")
            {
                sb.AppendLine();
            }
            else if (el.Name == w + "tc")
            {
                sb.Append('\t');
            }
        }

        return NormalizeWhitespace(sb.ToString());
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return NormalizeWhitespace(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));

        try
        {
            return NormalizeWhitespace(new UTF8Encoding(false, true).GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return NormalizeWhitespace(Encoding.Default.GetString(bytes));
        }
    }

    private static string NormalizeWhitespace(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = Regex.Replace(normalized, "[\t ]+\n", "\n");
        normalized = Regex.Replace(normalized, "\n{3,}", "\n\n");
        normalized = Regex.Replace(normalized, "[ \t]{2,}", " ");
        return normalized.Trim();
    }
}
