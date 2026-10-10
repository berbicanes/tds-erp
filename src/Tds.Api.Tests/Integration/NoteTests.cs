using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Notes;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class NoteTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Every_role_adds_ticks_and_unticks_notes()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var op = await factory.ClientAsAsync(Roles.Operater);

        var note = await (await op.PostAsJsonAsync("/api/notes", new { text = "  Messer plin naručiti, disk 2x " }, Ct)).ReadAsync<NoteDto>(HttpStatusCode.Created);
        Assert.Equal("Messer plin naručiti, disk 2x", note.Text);

        var done = await (await op.PostAsJsonAsync($"/api/notes/{note.Id}/done", new { isDone = true, version = note.Version }, Ct)).ReadAsync<NoteDto>();
        Assert.True(done.IsDone);
        Assert.NotNull(done.DoneAt);

        var undone = await (await op.PostAsJsonAsync($"/api/notes/{note.Id}/done", new { isDone = false, version = done.Version }, Ct)).ReadAsync<NoteDto>();
        Assert.False(undone.IsDone);
        Assert.Null(undone.DoneAt);

        Assert.Equal("Upiši bilješku.", (await (await op.PostAsJsonAsync("/api/notes", new { text = " " }, Ct)).ProblemAsync(HttpStatusCode.BadRequest)).FieldError("text"));
        // No delete and no text edit exist at all.
        Assert.Contains((await op.DeleteAsync($"/api/notes/{note.Id}", Ct)).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Contains((await op.PutAsJsonAsync($"/api/notes/{note.Id}", new { text = "X" }, Ct)).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    [Fact]
    public async Task Done_notes_disappear_after_7_days_and_open_notes_come_first()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        async Task<NoteDto> Add(string t) => await (await admin.PostAsJsonAsync("/api/notes", new { text = t }, Ct)).ReadAsync<NoteDto>(HttpStatusCode.Created);
        var old = await Add("Hladnjak ispuhati");
        var recent = await Add("NH mat rezanje inox");
        await Add("NH mat rezanje aluminij");
        await admin.PostAsJsonAsync($"/api/notes/{old.Id}/done", new { isDone = true, version = old.Version }, Ct);
        await admin.PostAsJsonAsync($"/api/notes/{recent.Id}/done", new { isDone = true, version = recent.Version }, Ct);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notes.Where(n => n.Id == old.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.DoneAt, DateTimeOffset.UtcNow.AddDays(-8)), Ct);

        var list = await (await admin.GetAsync("/api/notes", Ct)).ReadAsync<List<NoteDto>>();
        Assert.Equal(["NH mat rezanje aluminij", "NH mat rezanje inox"], list.Select(n => n.Text));
        Assert.False(list[0].IsDone);
        Assert.True(list[1].IsDone);
    }

    [Fact]
    public void Visibility_rule()
    {
        var now = DateTimeOffset.UtcNow;
        var n = new Note { Text = "x" };
        Assert.True(n.IsVisible(now));
        n.SetDone(true, now.AddDays(-6.9));
        Assert.True(n.IsVisible(now));
        n.SetDone(false, now);
        n.SetDone(true, now.AddDays(-7.1));
        Assert.False(n.IsVisible(now));
    }
}
