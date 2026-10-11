namespace Tds.Api.Features.Import;

/// <summary>
/// Admin command line (not UI):  dotnet Tds.Api.dll import-notion &lt;folder&gt; --dry-run | --apply
/// Runs after migrations, prints the report and exits instead of starting the web server.
/// </summary>
public sealed record ImportCommand(string Folder, bool Apply)
{
    public const string Name = "import-notion";

    public const string Usage = """
        Upotreba: dotnet Tds.Api.dll import-notion <folder sa Notion CSV exportom> --dry-run | --apply
          --dry-run  provjera i izvještaj, ništa se ne upisuje (uvijek prvo ovo)
          --apply    upis u jednoj transakciji; ništa se ne upisuje ako postoji ijedna greška
        """;

    /// <summary>
    /// Null when the arguments are not this command (normal web start). <paramref name="error"/> is set when they are
    /// this command but incomplete: exactly one of --dry-run / --apply is required.
    /// </summary>
    public static ImportCommand? Parse(string[] args, out string? error)
    {
        error = null;
        if (args.Length == 0 || args[0] != Name)
            return null;
        var rest = args.Skip(1).ToList();
        var dryRun = rest.Remove("--dry-run");
        var apply = rest.Remove("--apply");
        if (dryRun == apply || rest.Count != 1 || rest[0].StartsWith("--", StringComparison.Ordinal))
        {
            error = Usage;
            return null;
        }
        return new ImportCommand(rest[0], apply);
    }

    public async Task<int> RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<NotionImporter>();
        var report = await importer.RunAsync(Folder, Apply, ct);
        Console.WriteLine(report.Render(Apply));
        return report.HasErrors ? 1 : 0;
    }
}
