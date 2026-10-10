using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Tds.Api.Domain;

namespace Tds.Api.Infrastructure;

/// <summary>RFC 9457 errors with Bosnian text in <c>detail</c> (CLAUDE.md "API").</summary>
public static class Problems
{
    public static IResult Unauthorized(string detail) =>
        TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status401Unauthorized, title: "Neovlašten pristup");

    public static Task WriteAsync(HttpContext http, int status, string title, string detail) =>
        http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        }).AsTask();
}

/// <summary>Maps <see cref="DomainException"/> to 409 and hides details of unexpected errors.</summary>
public sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is DomainException domain)
        {
            http.Response.StatusCode = StatusCodes.Status409Conflict;
            await Problems.WriteAsync(http, StatusCodes.Status409Conflict, "Pravilo nije ispunjeno", domain.Message);
            return true;
        }

        logger.LogError(exception, "Unhandled exception");
        http.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Problems.WriteAsync(http, StatusCodes.Status500InternalServerError, "Greška na serveru",
            "Došlo je do neočekivane greške. Pokušaj ponovo, a ako se ponovi, javi administratoru.");
        return true;
    }
}
