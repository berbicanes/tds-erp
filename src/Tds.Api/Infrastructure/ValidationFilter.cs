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
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationFilter<T>>();
}
