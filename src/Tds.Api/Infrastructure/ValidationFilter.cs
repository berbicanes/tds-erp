using FluentValidation;

namespace Tds.Api.Infrastructure;

/// <summary>Runs the FluentValidation validator for <typeparamref name="T"/>; 400 with per-field errors.</summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
            return TypedResults.Problem(detail: "Zahtjev je prazan ili neispravan.", statusCode: StatusCodes.Status400BadRequest);

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (!result.IsValid)
            return TypedResults.ValidationProblem(result.ToDictionary(),
                detail: "Neka polja nisu ispravno popunjena.", title: "Neispravni podaci");

        return await next(context);
    }
}

public static class ValidationExtensions
{
    /// <summary>Message for a missing concurrency token (every edit sends back the version it read).</summary>
    public const string VersionMissing = "Nedostaje verzija zapisa. Osvježi stranicu i pokušaj ponovo.";

    /// <summary>
    /// Required field (CLAUDE.md "API"): request DTOs use nullable types so an omitted field is reported here,
    /// never bound to 0, 0001-01-01, false or an empty Guid.
    /// </summary>
    /// <summary>
    /// Version of a shared create/update DTO: required on update (PUT), meaningless on create.
    /// </summary>
    public static void VersionOnUpdate<T>(this AbstractValidator<T> validator, IHttpContextAccessor http, System.Linq.Expressions.Expression<Func<T, uint?>> version) =>
        validator.RuleFor(version).Required(VersionMissing).When(_ => HttpMethods.IsPut(http.HttpContext?.Request.Method ?? ""));

    public static IRuleBuilderOptions<T, TProperty?> Required<T, TProperty>(this IRuleBuilder<T, TProperty?> rule, string message)
        where TProperty : struct =>
        rule.NotNull().WithMessage(message);

    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationFilter<T>>();
}
