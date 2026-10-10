namespace Tds.Api.Infrastructure;

/// <summary>Write responses that carry non-blocking warnings (e.g. "lot bez certifikata", DOMAIN §7.2).</summary>
public sealed record WithWarnings<T>(T Data, IReadOnlyList<string> Warnings);

public static class ApiResults
{
    public static IResult NotFound(string detail) =>
        TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status404NotFound, title: "Nije pronađeno");

    public static IResult Conflict(string detail) =>
        TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status409Conflict, title: "Pravilo nije ispunjeno");

    public static IResult Validation(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] },
            detail: message, title: "Neispravni podaci");
}
