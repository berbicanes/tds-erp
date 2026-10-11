using System.Net;
using System.Net.Http.Json;
using Tds.Api.Domain;
using Tds.Api.Features.Employees;
using Tds.Api.Features.Users;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class EmployeeAndUserTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Admin_creates_lists_searches_and_updates_employees()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();

        var marko = await (await admin.PostAsJsonAsync("/api/employees",
            new { fullName = " Marko Marić ", position = "Operater lasera", isActive = true, version = 0 }, Ct))
            .ReadAsync<EmployeeDto>(HttpStatusCode.Created);
        await admin.PostAsJsonAsync("/api/employees", new { fullName = "Amra Hodžić", position = "Kontrolor", isActive = true }, Ct);
        await admin.PostAsJsonAsync("/api/employees", new { fullName = "Stari Radnik", isActive = false }, Ct);

        Assert.Equal("Marko Marić", marko.FullName);

        var all = await (await admin.GetAsync("/api/employees?sort=fullName", Ct)).ReadAsync<PagedResult<EmployeeDto>>();
        Assert.Equal(["Amra Hodžić", "Marko Marić", "Stari Radnik"], all.Items.Select(x => x.FullName));

        var search = await (await admin.GetAsync("/api/employees?q=lasera", Ct)).ReadAsync<PagedResult<EmployeeDto>>();
        Assert.Equal("Marko Marić", Assert.Single(search.Items).FullName);

        var active = await (await admin.GetAsync("/api/employees?isActive=true&sort=-fullName", Ct)).ReadAsync<PagedResult<EmployeeDto>>();
        Assert.Equal(["Marko Marić", "Amra Hodžić"], active.Items.Select(x => x.FullName));

        var updated = await (await admin.PutAsJsonAsync($"/api/employees/{marko.Id}",
            new { fullName = "Marko Marić", position = "Operater prese", isActive = false, version = marko.Version }, Ct))
            .ReadAsync<EmployeeDto>();
        Assert.Equal("Operater prese", updated.Position);
        Assert.False(updated.IsActive);

        // Pickers offer only active employees.
        var options = await admin.GetFromJsonAsync<List<EmployeeOption>>("/api/employees/options", Ct);
        Assert.Equal(["Amra Hodžić"], options!.Select(o => o.FullName));
    }

    [Fact]
    public async Task Stale_version_returns_409_with_concurrency_code()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var e = await (await admin.PostAsJsonAsync("/api/employees", new { fullName = "Marko", isActive = true }, Ct))
            .ReadAsync<EmployeeDto>(HttpStatusCode.Created);

        await admin.PutAsJsonAsync($"/api/employees/{e.Id}", new { fullName = "Marko A", isActive = true, version = e.Version }, Ct);
        var stale = await admin.PutAsJsonAsync($"/api/employees/{e.Id}", new { fullName = "Marko B", isActive = true, version = e.Version }, Ct);

        var problem = await stale.ProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal("concurrency", problem.GetProperty("code").GetString());
        Assert.Equal(Problems.ConcurrencyMessage, problem.Detail());
    }

    [Fact]
    public async Task Validation_errors_are_per_field_in_bosnian()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/employees", new { fullName = " ", isActive = true }, Ct);

        Assert.Equal("Unesi ime i prezime.", (await response.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("fullName"));
    }

    [Theory]
    [InlineData(Roles.Kontrolor)]
    [InlineData(Roles.Operater)]
    public async Task Only_admin_manages_employees_and_users(string role)
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var client = await factory.ClientAsAsync(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/employees", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/employees", new { fullName = "X", isActive = true }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users", Ct)).StatusCode);
        // Pickers are open to everyone signed in.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees/options", Ct)).StatusCode);
    }

    [Fact]
    public async Task Admin_creates_user_links_employee_changes_role_and_resets_password()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();

        var user = await (await admin.PostAsJsonAsync("/api/users",
            new { userName = "amra", role = "Operater", password = "lozinka-amra-1" }, Ct)).ReadAsync<UserDto>(HttpStatusCode.Created);
        Assert.Equal("Operater", user.Role);
        Assert.True(user.IsActive);

        var emp = await (await admin.PostAsJsonAsync("/api/employees", new { fullName = "Amra Hodžić", isActive = true, userId = user.Id }, Ct))
            .ReadAsync<EmployeeDto>(HttpStatusCode.Created);
        Assert.Equal("amra", emp.UserName);

        // Same login cannot be linked to a second employee.
        var second = await admin.PostAsJsonAsync("/api/employees", new { fullName = "Neko Drugi", isActive = true, userId = user.Id }, Ct);
        Assert.Equal("Korisnički nalog je već vezan za zaposlenika Amra Hodžić.",
            (await second.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("userId"));

        var promoted = await (await admin.PutAsJsonAsync($"/api/users/{user.Id}", new { role = "Kontrolor", isActive = true }, Ct)).ReadAsync<UserDto>();
        Assert.Equal("Kontrolor", promoted.Role);
        Assert.Equal("Amra Hodžić", promoted.EmployeeName);

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PostAsJsonAsync($"/api/users/{user.Id}/password", new { password = "nova-lozinka-2" }, Ct)).StatusCode);
        var amra = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,
            (await amra.PostAsJsonAsync("/api/auth/login", new { userName = "amra", password = "nova-lozinka-2" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Weak_password_and_duplicate_username_are_rejected()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();

        var weak = await admin.PostAsJsonAsync("/api/users", new { userName = "x1", role = "Operater", password = "kratka1" }, Ct);
        Assert.Contains("najmanje 10 znakova", (await weak.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("password"));

        var noDigit = await admin.PostAsJsonAsync("/api/users", new { userName = "x2", role = "Operater", password = "samoslovaaaa" }, Ct);
        Assert.Contains("cifru", (await noDigit.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("password"));

        var dup = await admin.PostAsJsonAsync("/api/users", new { userName = "admin", role = "Operater", password = "lozinka-123" }, Ct);
        Assert.Equal("Korisničko ime admin je zauzeto.", (await dup.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("userName"));

        var badName = await admin.PostAsJsonAsync("/api/users", new { userName = "Šef", role = "Operater", password = "lozinka-123" }, Ct);
        Assert.Contains("bez dijakritika", (await badName.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("userName"));
    }

    [Fact]
    public async Task Deactivated_user_cannot_log_in_and_admin_cannot_deactivate_self()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var user = await (await admin.PostAsJsonAsync("/api/users",
            new { userName = "bivsi", role = "Operater", password = "lozinka-123" }, Ct)).ReadAsync<UserDto>(HttpStatusCode.Created);

        var off = await (await admin.PutAsJsonAsync($"/api/users/{user.Id}", new { role = "Operater", isActive = false }, Ct)).ReadAsync<UserDto>();
        Assert.False(off.IsActive);

        // PREGLED N4 / DOMAIN §8: the correct password of a deactivated account gets the same answer as a wrong one,
        // so its password cannot be confirmed by guessing.
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = "bivsi", password = "lozinka-123" }, Ct);
        Assert.Equal(Features.Auth.AuthEndpoints.WrongCredentials, (await login.ProblemAsync(HttpStatusCode.Unauthorized)).Detail());
        var wrong = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = "bivsi", password = "pogresna-12" }, Ct);
        Assert.Equal(Features.Auth.AuthEndpoints.WrongCredentials, (await wrong.ProblemAsync(HttpStatusCode.Unauthorized)).Detail());

        var me = await admin.GetFromJsonAsync<Features.Auth.MeResponse>("/api/me", Ct);
        var self = await admin.PutAsJsonAsync($"/api/users/{me!.Id}", new { role = "Admin", isActive = false }, Ct);
        await self.ProblemAsync(HttpStatusCode.Conflict);
        var demote = await admin.PutAsJsonAsync($"/api/users/{me.Id}", new { role = "Kontrolor", isActive = true }, Ct);
        await demote.ProblemAsync(HttpStatusCode.Conflict);
    }
}
