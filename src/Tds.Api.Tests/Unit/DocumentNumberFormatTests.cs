using Tds.Api.Domain;

namespace Tds.Api.Tests.Unit;

public class DocumentNumberFormatTests
{
    [Theory]
    [InlineData(DocumentNumberKind.WorkOrder, 42, "RN-2026-0042")]
    [InlineData(DocumentNumberKind.ProductionEntry, 3, "DP-20261012-03")]
    [InlineData(DocumentNumberKind.QualityCheck, 118, "KK-2026-0118")]
    [InlineData(DocumentNumberKind.NonConformity, 7, "NC-2026-007")]
    public void Formats_match_DOMAIN_examples(DocumentNumberKind kind, int value, string expected) =>
        Assert.Equal(expected, DocumentNumberFormat.Format(kind, new DateOnly(2026, 10, 12), value));

    [Theory]
    [InlineData(DocumentNumberKind.ProductionEntry, 100, "DP-20261012-100")]
    [InlineData(DocumentNumberKind.NonConformity, 1000, "NC-2026-1000")]
    [InlineData(DocumentNumberKind.WorkOrder, 10000, "RN-2026-10000")]
    [InlineData(DocumentNumberKind.QualityCheck, 10000, "KK-2026-10000")]
    public void Numbers_wider_than_format_just_widen(DocumentNumberKind kind, int value, string expected) =>
        Assert.Equal(expected, DocumentNumberFormat.Format(kind, new DateOnly(2026, 10, 12), value));

    [Fact]
    public void Production_entry_period_is_the_day_others_the_year()
    {
        var date = new DateOnly(2026, 10, 12);
        Assert.Equal("20261012", DocumentNumberFormat.Period(DocumentNumberKind.ProductionEntry, date));
        Assert.Equal("2026", DocumentNumberFormat.Period(DocumentNumberKind.WorkOrder, date));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_value_is_rejected(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DocumentNumberFormat.Format(DocumentNumberKind.WorkOrder, new DateOnly(2026, 1, 1), value));
}
