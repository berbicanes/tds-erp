using Tds.Api.Features.Import;

namespace Tds.Api.Tests.UnitTests;

/// <summary>NOTION_SOURCE.md "Import": formats; anything else is reported, not guessed.</summary>
public class NotionCsvTests
{
    [Fact]
    public void Reads_quoted_fields_with_commas_quotes_and_line_breaks_and_skips_the_bom()
    {
        var table = NotionCsv.Read("x.csv", "﻿A,B,C\r\n1,\"a, b\",\"say \"\"hi\"\"\nnext line\"\r\n\r\n2,,\n");

        Assert.Equal(["A", "B", "C"], table.Headers);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Rows[0].Line);
        Assert.Equal("a, b", table.Rows[0].Get("B"));
        Assert.Equal("say \"hi\"\nnext line", table.Rows[0].Get("C"));
        Assert.Equal("", table.Rows[1].Get("B"));
        Assert.Equal("", table.Rows[1].Get("Missing column"));
    }

    [Theory]
    [InlineData("2026-10-12")]
    [InlineData("October 12, 2026")]
    [InlineData("12.10.2026")]
    [InlineData("12.10.2026.")]
    public void Supported_date_formats(string text)
    {
        Assert.True(NotionCsv.TryDate(text, out var d));
        Assert.Equal(new DateOnly(2026, 10, 12), d);
    }

    [Theory]
    [InlineData("12/10/2026")]
    [InlineData("October 12, 2026 9:00 AM")]
    [InlineData("October 12, 2026 → October 14, 2026")]
    [InlineData("sutra")]
    public void Other_date_formats_are_rejected(string text) => Assert.False(NotionCsv.TryDate(text, out _));

    [Fact]
    public void Empty_values_are_unknown_not_zero()
    {
        Assert.True(NotionCsv.TryDate("", out var d));
        Assert.Null(d);
        Assert.True(NotionCsv.TryDecimal("", out var n));
        Assert.Null(n);
    }

    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData("1250", 1250)]
    [InlineData("-3", -3)]
    public void Numbers_with_a_decimal_point(string text, double expected)
    {
        Assert.True(NotionCsv.TryDecimal(text, out var n));
        Assert.Equal((decimal)expected, n);
    }

    [Theory]
    [InlineData("1,5")]
    [InlineData("1,234")]
    [InlineData("1.234,5")]
    [InlineData("12 kg")]
    public void Comma_thousands_separator_and_units_are_rejected(string text) => Assert.False(NotionCsv.TryDecimal(text, out _));

    [Fact]
    public void Whole_numbers()
    {
        Assert.True(NotionCsv.TryInt("200", out var a));
        Assert.Equal(200, a);
        Assert.True(NotionCsv.TryInt("12.0", out var b));
        Assert.Equal(12, b);
        Assert.False(NotionCsv.TryInt("12.5", out _));
    }

    [Fact]
    public void Relations_keep_commas_inside_names()
    {
        Assert.Equal(["Čelik Trade d.o.o.", "Alu, Metal"],
            NotionCsv.Names("Čelik Trade d.o.o. (https://www.notion.so/Celik-111), Alu, Metal (https://www.notion.so/Alu-222)"));
        Assert.Equal(["H-1", "H-2"], NotionCsv.Names("H-1, H-2"));
        Assert.Empty(NotionCsv.Names(""));
    }

    [Theory]
    [InlineData("import-notion /data")]
    [InlineData("import-notion /data --dry-run --apply")]
    [InlineData("import-notion --dry-run")]
    [InlineData("import-notion /data /other --apply")]
    public void Command_requires_a_folder_and_exactly_one_of_dry_run_or_apply(string commandLine)
    {
        Assert.Null(ImportCommand.Parse(commandLine.Split(' '), out var error));
        Assert.Equal(ImportCommand.Usage, error);
    }

    [Fact]
    public void Command_parsing()
    {
        Assert.Equal(new ImportCommand("/data", Apply: false), ImportCommand.Parse(["import-notion", "--dry-run", "/data"], out _));
        Assert.Equal(new ImportCommand("/data", Apply: true), ImportCommand.Parse(["import-notion", "/data", "--apply"], out _));
        Assert.Null(ImportCommand.Parse(["--urls", "http://+:8080"], out var error));
        Assert.Null(error);
    }
}
