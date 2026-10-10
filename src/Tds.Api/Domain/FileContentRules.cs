using System.IO.Compression;
using System.Text;

namespace Tds.Api.Domain;

/// <summary>
/// DATA_MODEL §14: allowed extensions and the content check per type. Returns a Bosnian error or null when OK.
/// DXF/STEP/DWG have no reliable magic number: we check the expected text header (STEP, ASCII DXF) or the
/// AutoCAD version header (DWG, binary DXF).
/// </summary>
public static class FileContentRules
{
    public static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".dxf"] = "image/vnd.dxf",
        [".dwg"] = "image/vnd.dwg",
        [".step"] = "model/step",
        [".stp"] = "model/step",
    };

    public const string AllowedList = ".pdf, .dxf, .dwg, .step, .stp, .png, .jpg, .jpeg, .xlsx, .docx";

    public static string? CheckName(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ContentTypes.ContainsKey(ext) ? null : $"Vrsta fajla nije dozvoljena. Dozvoljeno: {AllowedList}.";
    }

    /// <summary>Checks the stored content against its extension. <paramref name="content"/> must be seekable.</summary>
    public static string? CheckContent(string fileName, Stream content)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var head = new byte[4096];
        content.Position = 0;
        var n = content.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        var bytes = head[..n];
        var bad = $"Sadržaj fajla ne odgovara vrsti {ext}. Fajl je oštećen ili je pogrešno imenovan.";

        bool StartsWith(ReadOnlySpan<byte> sig) => bytes.AsSpan().StartsWith(sig);

        switch (ext)
        {
            case ".pdf":
                return StartsWith("%PDF-"u8) ? null : bad;
            case ".png":
                return StartsWith(Png) ? null : bad;
            case ".jpg" or ".jpeg":
                return StartsWith(Jpeg) ? null : bad;
            case ".xlsx" or ".docx":
                if (!StartsWith("PK\x03\x04"u8))
                    return bad;
                var required = ext == ".xlsx" ? "xl/workbook.xml" : "word/document.xml";
                try
                {
                    content.Position = 0;
                    using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
                    return zip.GetEntry(required) is null ? bad : null;
                }
                catch (InvalidDataException)
                {
                    return bad;
                }
            case ".dwg":
                return StartsWith("AC10"u8) ? null : bad;
            case ".step" or ".stp":
                return TextStart(bytes).StartsWith("ISO-10303-21;", StringComparison.Ordinal) ? null : bad;
            case ".dxf":
                if (StartsWith("AutoCAD Binary DXF"u8))
                    return null;
                return !bytes.AsSpan().Contains((byte)0) && Encoding.Latin1.GetString(bytes).Contains("SECTION", StringComparison.Ordinal) ? null : bad;
            default:
                return $"Vrsta fajla nije dozvoljena. Dozvoljeno: {AllowedList}.";
        }
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>Text after an optional UTF-8 BOM and leading whitespace.</summary>
    private static string TextStart(byte[] bytes)
    {
        var span = bytes.AsSpan();
        if (span.StartsWith(Utf8Bom))
            span = span[3..];
        return Encoding.Latin1.GetString(span).TrimStart();
    }
}
