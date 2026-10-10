using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Auth;
using Tds.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
// Field keys in validation errors match JSON camelCase ("userName", not "UserName").
ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
    member is null ? null : System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(member.Name);
builder.Services.AddHttpContextAccessor();

// Behind Caddy (production): trust X-Forwarded-* from the reverse proxy. The API port is not published.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Auth cookie keys survive container restarts (otherwise every deploy signs everyone out).
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection().SetApplicationName("tds-erp").PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<DocumentNumberService>();
builder.Services.AddSingleton<IFileStorage>(sp => new LocalFileStorage(
    builder.Configuration[$"{FileStorageOptions.Section}:Root"] ?? "",
    sp.GetRequiredService<TimeProvider>()));

builder.Services.AddDbContext<AppDbContext>((sp, options) => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention()
    .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

// Auth: cookie only, same origin, no JWT (CLAUDE.md "Auth").
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "tds_auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    // API: 401/403 instead of redirects.
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Problems.WriteAsync(ctx.HttpContext, 401, "Neovlašten pristup", "Nisi prijavljen.");
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Problems.WriteAsync(ctx.HttpContext, 403, "Zabranjeno", "Nemaš ovlasti za ovu akciju.");
    };
});
builder.Services.AddAuthorization();
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();
builder.Services.AddScoped<IdentitySeeder>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    // Single-server app: migrations are applied on startup.
    await db.Database.MigrateAsync();
    logger.LogInformation("Database connection OK, migrations applied");
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

app.Run();

public partial class Program;
