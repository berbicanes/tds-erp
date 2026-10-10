using System.IO.Compression;
using System.Text;
using Tds.Api.Domain;

namespace Tds.Api.Tests.Unit;

/// <summary>DATA_MODEL §14 content checks.</summary>
public class FileContentRulesTests
{
    private static MemoryStream Bytes(params byte[] b) => new(b);
    private static MemoryStream Text(string s) => new(Encoding.UTF8.GetBytes(s));

    private static MemoryStream Zip(string entry)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            using (var w = new StreamWriter(zip.CreateEntry(entry).Open()))
                w.Write("<x/>");
        return ms;
    }

    public static TheoryData<string, Func<Stream>> Valid => new()
    {
        { "crtez.pdf", () => Text("%PDF-1.7\n...") },
        { "slika.PNG", () => Bytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0) },
        { "foto.jpg", () => Bytes(0xFF, 0xD8, 0xFF, 0xE0, 0, 0) },
        { "foto.jpeg", () => Bytes(0xFF, 0xD8, 0xFF, 0xE1, 0, 0) },
        { "mjerenja.xlsx", () => Zip("xl/workbook.xml") },
        { "izvjestaj.docx", () => Zip("word/document.xml") },
        { "dio.dwg", () => Text("AC1032\0\0\0") },
        { "dio.step", () => Text("ISO-10303-21;\nHEADER;") },
        { "dio.stp", () => new MemoryStream([0xEF, 0xBB, 0xBF, .. Encoding.ASCII.GetBytes("\r\n  ISO-10303-21;\n")]) },
        { "kontura.dxf", () => Text("  0\nSECTION\n  2\nHEADER\n") },
        { "kontura_bin.dxf", () => Text("AutoCAD Binary DXF\r\n\u001a\0") },
    };

    public static TheoryData<string, Func<Stream>> Invalid => new()
    {
        { "virus.pdf", () => Bytes(0x4D, 0x5A, 0x90, 0x00) },                 // EXE renamed to .pdf
        { "slika.png", () => Bytes(0xFF, 0xD8, 0xFF, 0xE0) },                 // JPEG named .png
        { "foto.jpg", () => Text("%PDF-1.4") },
        { "mjerenja.xlsx", () => Zip("word/document.xml") },                  // DOCX renamed to .xlsx
        { "izvjestaj.docx", () => Text("PK\u0003\u0004 not really a zip") },  // broken zip
        { "dio.dwg", () => Text("%PDF-1.4") },
        { "dio.step", () => Text("solid cube\nfacet normal") },               // STL renamed
        { "kontura.dxf", () => Bytes(0x00, 0x01, 0x02, 0x53, 0x45) },         // binary garbage
        { "kontura.dxf", () => Text("just some notes without sections") },
    };

    [Theory]
    [MemberData(nameof(Valid))]
    public void Valid_content_passes(string name, Func<Stream> content) =>
        Assert.Null(FileContentRules.CheckContent(name, content()));

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Content_not_matching_extension_is_rejected(string name, Func<Stream> content) =>
        Assert.StartsWith("Sadržaj fajla ne odgovara vrsti", FileContentRules.CheckContent(name, content()));

    [Theory]
    [InlineData("program.exe")]
    [InlineData("skripta.js")]
    [InlineData("bez_ekstenzije")]
    [InlineData("crtez.pdf.exe")]
    public void Not_allowed_extensions_are_rejected(string name) =>
        Assert.Equal("Vrsta fajla nije dozvoljena. Dozvoljeno: .pdf, .dxf, .dwg, .step, .stp, .png, .jpg, .jpeg, .xlsx, .docx.",
            FileContentRules.CheckName(name));

    [Theory]
    [InlineData("CRTEZ.PDF")]
    [InlineData("a.Step")]
    public void Extensions_are_case_insensitive(string name) => Assert.Null(FileContentRules.CheckName(name));
}
