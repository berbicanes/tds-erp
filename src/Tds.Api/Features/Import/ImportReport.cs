using System.Text;

namespace Tds.Api.Features.Import;

public enum FindingKind { Greska, Duplikat, Upozorenje }

/// <param name="Source">Notion database (file) the finding belongs to, e.g. "Dobavljači".</param>
/// <param name="Line">Row number in the CSV (header = 1); null for file-level findings.</param>
public sealed record Finding(FindingKind Kind, string Source, int? Line, string Message)
{
    public override string ToString() => Line is null ? $"[{Source}] {Message}" : $"[{Source}, red {Line}] {Message}";
}

/// <summary>What an import found and did (NOTION_SOURCE.md "Import": vrste nalaza).</summary>
public sealed class ImportReport
{
    private readonly List<Finding> _findings = [];
    private readonly Dictionary<string, int> _created = [];

    public IReadOnlyList<Finding> Findings => _findings;
    public IReadOnlyDictionary<string, int> Created => _created;
    public List<string> Files { get; } = [];
    public bool HasErrors => _findings.Any(f => f.Kind == FindingKind.Greska);

    /// <summary>True only when the data was written and committed (--apply without errors).</summary>
    public bool Applied { get; set; }

    public void Error(string source, int? line, string message) => _findings.Add(new(FindingKind.Greska, source, line, message));
    public void Duplicate(string source, int? line, string message) => _findings.Add(new(FindingKind.Duplikat, source, line, message));
    public void Warning(string source, int? line, string message) => _findings.Add(new(FindingKind.Upozorenje, source, line, message));

    public void Count(string what, int n = 1) => _created[what] = _created.GetValueOrDefault(what) + n;

    public string Render(bool apply)
    {
        var sb = new StringBuilder();
        sb.AppendLine(apply ? "Notion import (--apply)" : "Notion import — PROBA (--dry-run): ništa nije upisano");
        foreach (var f in Files)
            sb.AppendLine("  " + f);
        sb.AppendLine();

        sb.AppendLine(Applied ? "Upisano:" : HasErrors ? "Bilo bi upisano kad se isprave greške:" : apply ? "Nije upisano:" : "Bilo bi upisano:");
        if (_created.Count == 0)
            sb.AppendLine("  ništa");
        foreach (var (what, n) in _created)
            sb.AppendLine($"  {what}: {n}");

        Section(sb, FindingKind.Greska, "GREŠKE — import se ne može izvršiti dok se ne isprave");
        Section(sb, FindingKind.Duplikat, "DUPLIKATI — preskočeno");
        Section(sb, FindingKind.Upozorenje, "UPOZORENJA — uvozi se bez tog podatka");

        sb.AppendLine();
        sb.AppendLine(HasErrors
            ? $"Rezultat: broj grešaka: {_findings.Count(f => f.Kind == FindingKind.Greska)}. Ništa nije upisano."
            : Applied ? "Rezultat: upisano, transakcija potvrđena."
            : "Rezultat: bez grešaka. Za upis pokreni ponovo sa --apply.");
        return sb.ToString();
    }

    private void Section(StringBuilder sb, FindingKind kind, string title)
    {
        var items = _findings.Where(f => f.Kind == kind).ToList();
        if (items.Count == 0)
            return;
        sb.AppendLine();
        sb.AppendLine($"{title} ({items.Count}):");
        foreach (var f in items)
            sb.AppendLine("  " + f);
    }
}
