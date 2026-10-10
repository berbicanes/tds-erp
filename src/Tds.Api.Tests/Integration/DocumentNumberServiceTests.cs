using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;

namespace Tds.Api.Tests.Integration;

public class DocumentNumberServiceTests(PostgresFixture postgres)
{
    private async Task<DbContextOptions<AppDbContext>> MigratedDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(await postgres.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return options;
    }

    private static async Task<string> Next(DbContextOptions<AppDbContext> options, DocumentNumberKind kind, DateOnly date)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new AppDbContext(options);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var number = await new DocumentNumberService(db).NextAsync(kind, date, ct);
        await tx.CommitAsync(ct);
        return number;
    }

    [Fact]
    public async Task Numbers_increment_per_kind_and_year()
    {
        var options = await MigratedDatabase();
        var d = new DateOnly(2026, 10, 12);

        Assert.Equal("RN-2026-0001", await Next(options, DocumentNumberKind.WorkOrder, d));
        Assert.Equal("RN-2026-0002", await Next(options, DocumentNumberKind.WorkOrder, d));
        Assert.Equal("NC-2026-001", await Next(options, DocumentNumberKind.NonConformity, d));
        Assert.Equal("RN-2027-0001", await Next(options, DocumentNumberKind.WorkOrder, new DateOnly(2027, 1, 1)));
        Assert.Equal("RN-2026-0003", await Next(options, DocumentNumberKind.WorkOrder, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public async Task Production_entry_counter_restarts_each_day()
    {
        var options = await MigratedDatabase();

        Assert.Equal("DP-20261012-01", await Next(options, DocumentNumberKind.ProductionEntry, new DateOnly(2026, 10, 12)));
        Assert.Equal("DP-20261012-02", await Next(options, DocumentNumberKind.ProductionEntry, new DateOnly(2026, 10, 12)));
        Assert.Equal("DP-20261013-01", await Next(options, DocumentNumberKind.ProductionEntry, new DateOnly(2026, 10, 13)));
    }

    [Fact]
    public async Task Concurrent_transactions_never_get_duplicate_numbers()
    {
        var options = await MigratedDatabase();
        var d = new DateOnly(2026, 10, 12);

        var numbers = await Task.WhenAll(Enumerable.Range(0, 25)
            .Select(_ => Task.Run(() => Next(options, DocumentNumberKind.WorkOrder, d))));

        Assert.Equal(25, numbers.Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, 25).Select(i => $"RN-2026-{i:D4}").Order(),
            numbers.Order());
    }

    [Fact]
    public async Task Rolled_back_transaction_does_not_cause_duplicates()
    {
        var options = await MigratedDatabase();
        var ct = TestContext.Current.CancellationToken;
        var d = new DateOnly(2026, 10, 12);

        await using (var db = new AppDbContext(options))
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            Assert.Equal("RN-2026-0001", await new DocumentNumberService(db).NextAsync(DocumentNumberKind.WorkOrder, d, ct));
            await tx.RollbackAsync(ct);
        }

        Assert.Equal("RN-2026-0001", await Next(options, DocumentNumberKind.WorkOrder, d));
        Assert.Equal("RN-2026-0002", await Next(options, DocumentNumberKind.WorkOrder, d));
    }

    [Fact]
    public async Task Issuing_outside_transaction_is_rejected()
    {
        var options = await MigratedDatabase();
        await using var db = new AppDbContext(options);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DocumentNumberService(db).NextAsync(DocumentNumberKind.WorkOrder, new DateOnly(2026, 1, 1),
                TestContext.Current.CancellationToken));
    }
}
