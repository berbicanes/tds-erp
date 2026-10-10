using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Domain;

namespace Tds.Api.Tests.Support;

public static class ApiClientExtensions
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Creates a user with the role (if needed) and returns a signed-in client.</summary>
    public static async Task<HttpClient> ClientAsAsync(this TdsApiFactory factory, string role, string? userName = null)
    {
        var client = factory.CreateClient();
        userName ??= role.ToLowerInvariant();
        var password = TdsApiFactory.AdminPassword;
        if (userName != TdsApiFactory.AdminUserName)
        {
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            if (await users.FindByNameAsync(userName) is null)
            {
                var u = new AppUser { UserName = userName };
                (await users.CreateAsync(u, password)).Succeeded.ShouldBeTrue();
                await users.AddToRoleAsync(u, role);
            }
        }
        var login = await client.PostAsJsonAsync("/api/auth/login", new { userName, password }, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    public static Task<HttpClient> AdminAsync(this TdsApiFactory factory) => factory.ClientAsAsync(Roles.Admin, TdsApiFactory.AdminUserName);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        if (response.StatusCode != expected)
            Assert.Fail($"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, Ct))!;
    }

    /// <summary>Asserts a ProblemDetails response and returns it.</summary>
    public static async Task<JsonElement> ProblemAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public static string Detail(this JsonElement problem) => problem.GetProperty("detail").GetString()!;

    public static string FieldError(this JsonElement problem, string field) =>
        problem.GetProperty("errors").GetProperty(field)[0].GetString()!;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static void ShouldBeTrue(this bool value) => Assert.True(value);
}
