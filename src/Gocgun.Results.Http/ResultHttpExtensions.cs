using Microsoft.AspNetCore.Http;

namespace Gocgun.Results.Http;

/// <summary>Translates a <see cref="Result"/> into <see cref="TypedResults"/>. Failures are never HTTP 200.</summary>
public static class ResultHttpExtensions
{
    /// <summary>Maps <paramref name="type"/> to an HTTP status. An undefined value is 500.</summary>
    /// <param name="type">The error category.</param>
    /// <returns>The status code from ADR 0006.</returns>
    public static int GetStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.PaymentRequired => StatusCodes.Status402PaymentRequired,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Concurrency => StatusCodes.Status409Conflict,
        ErrorType.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorType.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>204 when <paramref name="result"/> succeeded. Otherwise a problem result.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.NoContent() : Problem(result);
    }

    /// <summary>200 with <see cref="Result{T}.Item"/> when <paramref name="result"/> succeeded. Otherwise a problem result.</summary>
    /// <typeparam name="T">Payload type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.Ok(result.Item) : Problem(result);
    }

    /// <summary>200 with the page when <paramref name="result"/> succeeded. Otherwise a problem result.</summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <param name="result">The page.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToHttpResult<T>(this ResultPaginated<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            return Problem(result);
        }

        return TypedResults.Ok(new PaginatedHttpBody<T>
        {
            Item = result.Item,
            Total = result.Total,
        });
    }

    /// <summary>201 with <see cref="Result{T}.Item"/> and <paramref name="location"/> when creation succeeded. Failures stay problem results.</summary>
    /// <typeparam name="T">Payload type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="location">Location of the created resource.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToCreatedHttpResult<T>(this Result<T> result, string location)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        if (!Uri.TryCreate(location, UriKind.RelativeOrAbsolute, out var createdAt))
        {
            throw new ArgumentException("Location must be a URI.", nameof(location));
        }

        return result.IsSuccess ? TypedResults.Created(createdAt, result.Item) : Problem(result);
    }

    private static ResultProblem Problem(Result result) =>
        new(result.Error ?? throw new InvalidOperationException("A failed result must carry an error."));
}
