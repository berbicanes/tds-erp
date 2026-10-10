using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tds.Api.Features.Auth;
using Tds.Api.Tests.Support;

namespace Tds.Api.Tests.Integration;

public class AuthEndpointsTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> Login(HttpClient client, string userName, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { userName, password }, Ct);

    private static async Task<JsonElement> Problem(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    [Fact]
    public async Task Me_without_login_is_401_problem_not_redirect()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Nisi prijavljen.", (await Problem(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_sets_strict_secure_httponly_cookie_and_me_returns_user_with_roles()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();

        var login = await Login(client, TdsApiFactory.AdminUserName, TdsApiFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("tds_auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", Ct);
        Assert.Equal(TdsApiFactory.AdminUserName, me!.UserName);
        Assert.Equal(["Admin"], me.Roles);
    }

    [Fact]
    public async Task Wrong_password_is_401_with_bosnian_detail()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();

        var response = await Login(client, TdsApiFactory.AdminUserName, "pogresna-lozinka");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Pogrešno korisničko ime ili lozinka.", (await Problem(response)).GetProperty("detail").GetString());
        Assert.False(response.Headers.Contains("Set-Cookie") &&
                     response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("tds_auth=")));
    }

    [Fact]
    public async Task Unknown_user_gets_same_message_as_wrong_password()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();

        var response = await Login(client, "nepostojeci", "bilo-sta-1234");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Pogrešno korisničko ime ili lozinka.", (await Problem(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Empty_fields_are_400_with_errors_per_field()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();

        var response = await Login(client, "", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Problem(response)).GetProperty("errors");
        Assert.Equal("Unesi korisničko ime.", errors.GetProperty("userName")[0].GetString());
        Assert.Equal("Unesi lozinku.", errors.GetProperty("password")[0].GetString());
    }

    [Fact]
    public async Task Sixth_attempt_after_five_failures_reports_lockout_even_with_correct_password()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
            await Login(client, TdsApiFactory.AdminUserName, "pogresna-lozinka");
        var response = await Login(client, TdsApiFactory.AdminUserName, TdsApiFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("zaključan", (await Problem(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();
        await Login(client, TdsApiFactory.AdminUserName, TdsApiFactory.AdminPassword);

        var logout = await client.PostAsync("/api/auth/logout", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Logout_is_post_only()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/logout", Ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
