using Tds.Api.Infrastructure;

namespace Tds.Api.Tests.UnitTests;

public class PagingTests
{
    private sealed record Row(string Name, int Qty);

    private static readonly Row[] Rows = [new("B", 2), new("A", 3), new("C", 1)];

    private static readonly SortMap<Row> Sort = new SortMap<Row>()
        .Add("name", r => r.Name)
        .Add("qty", r => r.Qty)
        .Default("name");

    [Theory]
    [InlineData("name", "A,B,C")]
    [InlineData("-name", "C,B,A")]
    [InlineData("qty", "C,B,A")]
    [InlineData("-QTY", "A,B,C")]
    [InlineData(null, "A,B,C")]
    [InlineData("nepoznato", "A,B,C")]
    [InlineData("-name; DROP TABLE x", "A,B,C")]
    public void Sort_uses_whitelisted_keys_only(string? sort, string expected) =>
        Assert.Equal(expected, string.Join(",", Sort.Apply(Rows.AsQueryable(), sort).Select(r => r.Name)));

    [Theory]
    [InlineData(null, null, 1, 50)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-3, 1000, 1, 200)]
    [InlineData(4, 25, 4, 25)]
    public void Page_and_size_are_clamped(int? page, int? size, int expectedPage, int expectedSize)
    {
        var q = new ListQuery(page, size, null, null);
        Assert.Equal(expectedPage, q.SafePage);
        Assert.Equal(expectedSize, q.SafePageSize);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  lim ", "lim")]
    public void Search_is_trimmed_or_null(string? q, string? expected) =>
        Assert.Equal(expected, new ListQuery(null, null, null, q).Search);

    [Fact]
    public void Like_escapes_wildcards() =>
        Assert.Equal(@"%50\%\_x%", PagingExtensions.Like("50%_x"));
}
