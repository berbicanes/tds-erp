namespace Tds.Api.Domain;

/// <summary>DATA_MODEL §16 / DOMAIN §6. Technical counter table; no audit fields, not audited.</summary>
public class NumberSequence
{
    public required string Kind { get; init; }
    public required string Period { get; init; }
    public int LastValue { get; set; }
}

public enum DocumentNumberKind
{
    WorkOrder,
    ProductionEntry,
    QualityCheck,
    NonConformity,
}

/// <summary>Document number formats from DOMAIN §6. Pure, no I/O.</summary>
public static class DocumentNumberFormat
{
    public static string Prefix(DocumentNumberKind kind) => kind switch
    {
        DocumentNumberKind.WorkOrder => "RN",
        DocumentNumberKind.ProductionEntry => "DP",
        DocumentNumberKind.QualityCheck => "KK",
        DocumentNumberKind.NonConformity => "NC",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Counter period: per day for ProductionEntry, per year for everything else.</summary>
    public static string Period(DocumentNumberKind kind, DateOnly documentDate) => kind == DocumentNumberKind.ProductionEntry
        ? documentDate.ToString("yyyyMMdd")
        : documentDate.ToString("yyyy");

    /// <summary>Formats a number. Values wider than the format just widen (DOMAIN §6).</summary>
    public static string Format(DocumentNumberKind kind, DateOnly documentDate, int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        var digits = kind switch
        {
            DocumentNumberKind.WorkOrder => 4,
            DocumentNumberKind.ProductionEntry => 2,
            DocumentNumberKind.QualityCheck => 4,
            DocumentNumberKind.NonConformity => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        return $"{Prefix(kind)}-{Period(kind, documentDate)}-{value.ToString("D" + digits)}";
    }
}
