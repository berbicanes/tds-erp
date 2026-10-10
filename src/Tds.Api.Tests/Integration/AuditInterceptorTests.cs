using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;

namespace Tds.Api.Tests.Integration;

public class AuditInterceptorTests(PostgresFixture postgres)
{
    private sealed class TestThing : VoidableEntity
    {
        public string Name { get; set; } = "";
        public decimal Quantity { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions options) : AppDbContext(options)
    {
        public DbSet<TestThing> Things => Set<TestThing>();
    }

    private sealed class FakeUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
    }

    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeUser _user = new() { UserId = Guid.CreateVersion7() };

    private async Task<Func<TestDbContext>> CreateContextFactory()
    {
        var cs = await postgres.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(cs)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditInterceptor(new SystemClock(_time), _user))
            .Options;
        await using (var db = new TestDbContext(options))
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return () => new TestDbContext(options);
    }

    [Fact]
    public async Task Create_sets_audit_fields_and_writes_create_log()
    {
        var newContext = await CreateContextFactory();
        var ct = TestContext.Current.CancellationToken;
        var thing = new TestThing { Name = "Lim", Quantity = 1.5m };

        await using (var db = newContext())
        {
            db.Things.Add(thing);
            await db.SaveChangesAsync(ct);
        }

        await using var check = newContext();
        var saved = await check.Things.SingleAsync(ct);
        Assert.Equal(_time.Now, saved.CreatedAt);
        Assert.Equal(_user.UserId, saved.CreatedBy);
        Assert.Equal(_time.Now, saved.UpdatedAt);
        Assert.Equal(_user.UserId, saved.UpdatedBy);

        var log = await check.AuditLogs.SingleAsync(ct);
        Assert.Equal(AuditAction.Create, log.Action);
        Assert.Equal(nameof(TestThing), log.EntityType);
        Assert.Equal(thing.Id, log.EntityId);
        Assert.Equal(_user.UserId, log.UserId);
        var changes = JsonDocument.Parse(log.ChangesJson).RootElement;
        Assert.Equal("Lim", changes.GetProperty("Name")[1].GetString());
        Assert.Equal(JsonValueKind.Null, changes.GetProperty("Name")[0].ValueKind);
        Assert.False(changes.TryGetProperty("CreatedAt", out _));
        Assert.False(changes.TryGetProperty("Id", out _));
    }

    [Fact]
    public async Task Update_logs_only_changed_fields_with_old_and_new_value()
    {
        var newContext = await CreateContextFactory();
        var ct = TestContext.Current.CancellationToken;
        var thing = new TestThing { Name = "Lim", Quantity = 1.5m };
        await using (var db = newContext())
        {
            db.Things.Add(thing);
            await db.SaveChangesAsync(ct);
        }

        _time.Now = _time.Now.AddHours(1);
        var editor = Guid.CreateVersion7();
        _user.UserId = editor;
        await using (var db = newContext())
        {
            var t = await db.Things.SingleAsync(ct);
            t.Name = "Lim inox";
            await db.SaveChangesAsync(ct);
        }

        await using var check = newContext();
        var saved = await check.Things.SingleAsync(ct);
        Assert.Equal(_time.Now, saved.UpdatedAt);
        Assert.Equal(editor, saved.UpdatedBy);
        Assert.NotEqual(editor, saved.CreatedBy);

        var log = await check.AuditLogs.SingleAsync(l => l.Action == AuditAction.Update, ct);
        var changes = JsonDocument.Parse(log.ChangesJson).RootElement;
        Assert.Equal(["Name"], changes.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Lim", changes.GetProperty("Name")[0].GetString());
        Assert.Equal("Lim inox", changes.GetProperty("Name")[1].GetString());
    }

    [Fact]
    public async Task Save_without_changes_writes_no_update_log()
    {
        var newContext = await CreateContextFactory();
        var ct = TestContext.Current.CancellationToken;
        await using (var db = newContext())
        {
            db.Things.Add(new TestThing { Name = "Lim" });
            await db.SaveChangesAsync(ct);
        }

        await using (var db = newContext())
        {
            var t = await db.Things.SingleAsync(ct);
            t.Name = "Lim"; // same value
            await db.SaveChangesAsync(ct);
        }

        await using var check = newContext();
        Assert.Equal(1, await check.AuditLogs.CountAsync(ct));
    }

    [Fact]
    public async Task Void_sets_voided_fields_and_writes_void_log()
    {
        var newContext = await CreateContextFactory();
        var ct = TestContext.Current.CancellationToken;
        await using (var db = newContext())
        {
            db.Things.Add(new TestThing { Name = "Lim" });
            await db.SaveChangesAsync(ct);
        }

        _time.Now = _time.Now.AddHours(2);
        await using (var db = newContext())
        {
            var t = await db.Things.SingleAsync(ct);
            t.Void("Pogrešan unos");
            await db.SaveChangesAsync(ct);
        }

        await using var check = newContext();
        var saved = await check.Things.SingleAsync(ct);
        Assert.True(saved.IsVoided);
        Assert.Equal("Pogrešan unos", saved.VoidedReason);
        Assert.Equal(_time.Now, saved.VoidedAt);
        Assert.Equal(_user.UserId, saved.VoidedBy);
        var log = await check.AuditLogs.SingleAsync(l => l.Action == AuditAction.Void, ct);
        Assert.Contains("Pogrešan unos", log.ChangesJson);
    }

    [Fact]
    public async Task System_action_has_null_user()
    {
        var newContext = await CreateContextFactory();
        var ct = TestContext.Current.CancellationToken;
        _user.UserId = null;
        await using (var db = newContext())
        {
            db.Things.Add(new TestThing { Name = "Seed" });
            await db.SaveChangesAsync(ct);
        }

        await using var check = newContext();
        Assert.Null((await check.Things.SingleAsync(ct)).CreatedBy);
        Assert.Null((await check.AuditLogs.SingleAsync(ct)).UserId);
    }

    [Fact]
    public async Task Hard_delete_is_rejected()
    {
        var newContext = await CreateContextFactory();
        var ct = TestContext.Current.CancellationToken;
        await using (var db = newContext())
        {
            db.Things.Add(new TestThing { Name = "Lim" });
            await db.SaveChangesAsync(ct);
        }

        await using (var db = newContext())
        {
            db.Things.Remove(await db.Things.SingleAsync(ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
        }

        await using var check = newContext();
        Assert.Equal(1, await check.Things.CountAsync(ct));
    }
}
