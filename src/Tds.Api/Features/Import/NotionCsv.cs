using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Tds.Api.Features.Import;

/// <summary>One data row of a Notion CSV export. <see cref="Line"/> is the row number as a spreadsheet shows it (header = 1).</summary>
public sealed class CsvRow(int line, IReadOnlyDictionary<string, string> values)
{
    public int Line { get; } = line;

    /// <summary>Trimmed value; "" when the column is missing or empty.</summary>
    public string Get(string column) => values.TryGetValue(column, out var v) ? v.Trim() : "";
}

public sealed record CsvTable(string FileName, IReadOnlyList<string> Headers, IReadOnlyList<CsvRow> Rows);

/// <summary>
/// Reading Notion CSV exports (NOTION_SOURCE.md "Import"). Every parser says whether the text is valid;
/// nothing is guessed: an unsupported format is an error for the report, never a default.
/// </summary>
public static partial class NotionCsv
{
    /// <summary>RFC 4180: comma separated, "" escapes a quote, quoted fields may contain commas and line breaks.</summary>
    public static CsvTable Read(string fileName, string text)
    {
        var records = Parse(text.Normalize(NormalizationForm.FormC));
        if (records.Count == 0)
            return new CsvTable(fileName, [], []);
        var headers = records[0].Select(h => h.Trim()).ToList();
        var rows = new List<CsvRow>();
        for (var r = 1; r < records.Count; r++)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var c = 0; c < headers.Count; c++)
                values.TryAdd(headers[c], c < records[r].Count ? records[r][c] : "");
            rows.Add(new CsvRow(r + 1, values));
        }
        return new CsvTable(fileName, headers, rows);
    }

    public static List<List<string>> Parse(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;

        void EndRecord()
        {
            record.Add(field.ToString());
            field.Clear();
            // A blank line (one empty field) is not a record.
            if (record.Count > 1 || record[0].Length > 0)
                records.Add(record);
            record = [];
        }

        for (; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') inQuotes = false;
                else field.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == ',') { record.Add(field.ToString()); field.Clear(); }
            else if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRecord();
            }
            else field.Append(ch);
        }
        if (field.Length > 0 || record.Count > 0)
            EndRecord();
        return records;
    }

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "MMMM d, yyyy", "d.M.yyyy", "d.M.yyyy."];

    /// <summary>Empty → (true, null). Supported: 2026-10-12, October 12, 2026, 12.10.2026(.).</summary>
    public static bool TryDate(string text, out DateOnly? value)
    {
        value = null;
        if (text.Length == 0)
            return true;
        if (!DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return false;
        value = d;
        return true;
    }

    /// <summary>Empty → (true, null). Only digits with an optional decimal point: a comma is ambiguous (1,234).</summary>
    public static bool TryDecimal(string text, out decimal? value)
    {
        value = null;
        if (text.Length == 0)
            return true;
        if (!DecimalPattern().IsMatch(text))
            return false;
        value = decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Empty → (true, null). Whole number; "12.0" is accepted, "12.5" is not.</summary>
    public static bool TryInt(string text, out int? value)
    {
        value = null;
        if (!TryDecimal(text, out var d))
            return false;
        if (d is null)
            return true;
        if (d != decimal.Truncate(d.Value) || d > int.MaxValue || d < int.MinValue)
            return false;
        value = (int)d.Value;
        return true;
    }

    /// <summary>
    /// Relation (or multi-value text): "Name (https://www.notion.so/…), Other (https://…)". Without Notion links the
    /// value is split on commas.
    /// </summary>
    public static List<string> Names(string text)
    {
        if (text.Length == 0)
            return [];
        if (text.Contains("notion.so/", StringComparison.OrdinalIgnoreCase))
            return NotionLink().Matches(text).Select(m => m.Groups["name"].Value.Trim()).Where(n => n.Length > 0).ToList();
        return text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>Notion checkbox export: "Yes" / "No" (also "true"/"false").</summary>
    public static bool IsChecked(string text) =>
        text.Equals("Yes", StringComparison.OrdinalIgnoreCase) || text.Equals("true", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex DecimalPattern();

    [GeneratedRegex(@"\s*(?<name>.+?)\s*\(https?://(?:www\.)?notion\.so/[^)]*\)\s*(?:,|$)", RegexOptions.IgnoreCase)]
    private static partial Regex NotionLink();
}
