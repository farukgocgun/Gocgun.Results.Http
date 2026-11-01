using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gocgun.Results.Http;

/// <summary>RFC 9457 problem written through <see cref="TypedResults"/>.</summary>
internal sealed class ResultProblem(Error error) : IResult
{
    private const string AboutBlank = "about:blank";

    private static readonly HashSet<string> ReservedMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "type", "title", "status", "detail", "instance", "errors", "code", "traceId",
    };

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var status = ResultHttpExtensions.GetStatusCode(error.Type);
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = error.Code,
            ["traceId"] = httpContext.TraceIdentifier,
        };
        CopyMetadata(error.Metadata, extensions);

        var message = httpContext.RequestServices.GetService<IProblemMessageResolver>()?.Resolve(error.Message) ?? error.Message;
        var type = ResolveType(httpContext, error.Code);
        var title = ToTitle(error.Code);
        var instance = httpContext.Request.Path.Value;
        IResult problem = error.Type == ErrorType.Validation
            ? TypedResults.ValidationProblem(
                CopyDetails(error.Details),
                detail: message,
                instance: instance,
                title: title,
                type: type,
                extensions: extensions)
            : TypedResults.Problem(
                detail: message,
                instance: instance,
                statusCode: status,
                title: title,
                type: type,
                extensions: extensions);

        if (status == StatusCodes.Status429TooManyRequests && TryRetryAfter(error.Metadata, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = retryAfter;
        }

        return problem.ExecuteAsync(httpContext);
    }

    private static string ResolveType(HttpContext httpContext, string code)
    {
        var baseUri = httpContext.RequestServices?.GetService<IOptions<ResultHttpOptions>>()?.Value.ErrorTypeBase;
        if (baseUri is null)
        {
            return AboutBlank;
        }

        var prefix = baseUri.OriginalString;
        if (!prefix.EndsWith('/'))
        {
            prefix += "/";
        }

        return prefix + ToKebab(code);
    }

    private static Dictionary<string, string[]> CopyDetails(IReadOnlyDictionary<string, string[]>? details) =>
        details is null
            ? new Dictionary<string, string[]>(StringComparer.Ordinal)
            : new Dictionary<string, string[]>(details, StringComparer.Ordinal);

    private static void CopyMetadata(IReadOnlyDictionary<string, object?>? metadata, Dictionary<string, object?> extensions)
    {
        if (metadata is null)
        {
            return;
        }

        foreach (var (key, value) in metadata)
        {
            // Reserved RFC 9457 members and our own members must not be duplicated or overridden by caller metadata.
            if (ReservedMembers.Contains(key))
            {
                continue;
            }

            extensions[key] = value;
        }
    }

    private static bool TryRetryAfter(IReadOnlyDictionary<string, object?>? metadata, out string retryAfter)
    {
        retryAfter = string.Empty;
        if (metadata is null || !metadata.TryGetValue("retryAfterSeconds", out var value) || value is null)
        {
            return false;
        }

        switch (value)
        {
            case int seconds when seconds >= 0:
                retryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                return true;
            case long seconds when seconds >= 0:
                retryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                return true;
            case string text when int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0:
                retryAfter = parsed.ToString(CultureInfo.InvariantCulture);
                return true;
            default:
                return false;
        }
    }

    private static string ToKebab(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return "error";
        }

        var builder = new StringBuilder(code.Length + 8);
        foreach (var character in code)
        {
            if (character is '.' or '_' or ' ' or '-')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }

                continue;
            }

            if (char.IsUpper(character) && builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string ToTitle(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return "Error";
        }

        var builder = new StringBuilder(code.Length + 8);
        for (var index = 0; index < code.Length; index++)
        {
            var character = code[index];
            if (character is '.' or '_' or '-')
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                continue;
            }

            if (char.IsUpper(character) && builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }

            builder.Append(builder.Length == 0 ? char.ToUpperInvariant(character) : char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
