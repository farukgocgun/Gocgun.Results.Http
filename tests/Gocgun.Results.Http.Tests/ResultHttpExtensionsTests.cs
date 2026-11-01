using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Gocgun.Results.Http.Tests;

public sealed class ResultHttpExtensionsTests
{
    [Fact]
    public async Task SuccessWithoutBody_Returns204()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Success().ToHttpResult());

        Assert.Equal(StatusCodes.Status204NoContent, executed.Status);
        Assert.Equal(string.Empty, executed.Body);
    }

    [Fact]
    public async Task SuccessWithBody_Returns200()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Success("hello").ToHttpResult());

        Assert.Equal(StatusCodes.Status200OK, executed.Status);
        Assert.Equal("\"hello\"", executed.Body);
        Assert.Contains("application/json", executed.ContentType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessPaginated_Returns200AndOmitsTotal()
    {
        IReadOnlyList<string> rows = ["a"];
        var executed = await HttpResultExecutor.ExecuteAsync(ResultPaginated<string>.Success(rows).ToHttpResult());

        Assert.Equal(StatusCodes.Status200OK, executed.Status);
        using var document = JsonDocument.Parse(executed.Body);
        Assert.Equal("a", document.RootElement.GetProperty("item")[0].GetString());
        Assert.False(document.RootElement.TryGetProperty("nextCursor", out _));
        Assert.False(document.RootElement.TryGetProperty("hasMore", out _));
        Assert.False(document.RootElement.TryGetProperty("total", out _));
    }

    [Fact]
    public async Task Created_Returns201()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Success("created-body").ToCreatedHttpResult("/api/social-accounts/1"));

        Assert.Equal(StatusCodes.Status201Created, executed.Status);
        Assert.Equal("/api/social-accounts/1", executed.Location);
        Assert.Equal("\"created-body\"", executed.Body);
    }

    [Fact]
    public async Task NullItem_Returns200()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Success<string>(null!).ToHttpResult());

        Assert.Equal(StatusCodes.Status200OK, executed.Status);
        Assert.DoesNotContain("problem+json", executed.ContentType ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("code", executed.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validation_Returns400()
    {
        var details = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["name"] = ["Required"],
        };
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(Error.Validation("Validation", "Name is required.", details)).ToHttpResult(), Catalog);

        AssertProblem(executed, StatusCodes.Status400BadRequest, "Validation");
        using var document = JsonDocument.Parse(executed.Body);
        Assert.Equal("Required", document.RootElement.GetProperty("errors").GetProperty("name")[0].GetString());
        Assert.Equal("https://errors.example.test/validation", document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Unauthorized_Returns401()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(Error.Unauthorized("InvalidCredentials", "Sign in again.")).ToHttpResult());

        AssertProblem(executed, StatusCodes.Status401Unauthorized, "InvalidCredentials");
    }

    [Fact]
    public async Task PaymentRequired_Returns402()
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["limit"] = "SocialAccounts",
            ["max"] = 1,
        };
        var executed = await HttpResultExecutor.ExecuteAsync(
            Result.Failure(Error.PaymentRequired("PlanLimitExceeded", "Plan limit exceeded.", metadata)).ToHttpResult(),
            Catalog);

        AssertProblem(executed, StatusCodes.Status402PaymentRequired, "PlanLimitExceeded");
        using var document = JsonDocument.Parse(executed.Body);
        Assert.Equal("https://errors.example.test/plan-limit-exceeded", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("Plan limit exceeded", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("SocialAccounts", document.RootElement.GetProperty("limit").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("max").GetInt32());
        Assert.Equal("/api/social-accounts", document.RootElement.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task Forbidden_Returns403()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(Error.Forbidden("EmailNotConfirmed", "Confirm email.")).ToHttpResult());

        AssertProblem(executed, StatusCodes.Status403Forbidden, "EmailNotConfirmed");
    }

    [Fact]
    public async Task NotFound_Returns404()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(Error.NotFound("Posts.NotFound", "Post was not found.")).ToHttpResult(), Catalog);

        AssertProblem(executed, StatusCodes.Status404NotFound, "Posts.NotFound");
        using var document = JsonDocument.Parse(executed.Body);
        Assert.Equal("https://errors.example.test/posts-not-found", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("Posts not found", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("Post was not found.", document.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Conflict_Returns409()
    {
        var error = Error.Conflict("SlotOccupied", "Slot is taken.");
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(error).ToHttpResult());
        var created = await HttpResultExecutor.ExecuteAsync(Result.Failure<string>(error).ToCreatedHttpResult("/api/items/1"));

        AssertProblem(executed, StatusCodes.Status409Conflict, "SlotOccupied");
        AssertProblem(created, StatusCodes.Status409Conflict, "SlotOccupied");
        Assert.NotEqual(StatusCodes.Status201Created, created.Status);
    }

    [Fact]
    public async Task Concurrency_Returns409()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(Error.Concurrency("ConcurrencyConflict", "Reload and retry.")).ToHttpResult());

        AssertProblem(executed, StatusCodes.Status409Conflict, "ConcurrencyConflict");
    }

    [Fact]
    public async Task UnsupportedMediaType_Returns415()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(
            Result.Failure(Error.UnsupportedMediaType("UnsupportedMedia", "That file type is not accepted.")).ToHttpResult());

        AssertProblem(executed, StatusCodes.Status415UnsupportedMediaType, "UnsupportedMedia");
    }

    [Fact]
    public async Task BusinessRule_Returns422()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(Error.BusinessRule("ConditionNotSupported", "Condition is not supported.")).ToHttpResult());

        AssertProblem(executed, StatusCodes.Status422UnprocessableEntity, "ConditionNotSupported");
    }

    [Fact]
    public async Task ServiceUnavailable_Returns503()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(Error.ServiceUnavailable("Ai.ProviderNotConfigured", "No provider.")).ToHttpResult());

        AssertProblem(executed, StatusCodes.Status503ServiceUnavailable, "Ai.ProviderNotConfigured");
    }

    [Fact]
    public async Task RateLimited_Returns429()
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["retryAfterSeconds"] = 30,
        };
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(Error.RateLimited("RateLimited", "Slow down.", metadata)).ToHttpResult());

        AssertProblem(executed, StatusCodes.Status429TooManyRequests, "RateLimited");
        Assert.Equal("30", executed.RetryAfter);
        using var document = JsonDocument.Parse(executed.Body);
        Assert.Equal(30, document.RootElement.GetProperty("retryAfterSeconds").GetInt32());
    }

    [Fact]
    public void UnknownErrorType_MapsTo500AndCannotBeBuilt()
    {
        Assert.Equal(StatusCodes.Status500InternalServerError, ResultHttpExtensions.GetStatusCode((ErrorType)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => Error.NotFound("Posts.NotFound", "Missing") with { Type = (ErrorType)999 });
    }

    [Fact]
    public void EmptyCodeViaWith_Throws()
    {
        Assert.Throws<ArgumentException>(() => Error.NotFound("Posts.NotFound", "Missing") with { Code = string.Empty });
    }

    [Fact]
    public async Task ErrorTypeBase_IsAboutBlankWhenNotConfigured()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(Error.NotFound("Posts.NotFound", "Missing")).ToHttpResult());

        using var document = JsonDocument.Parse(executed.Body);
        Assert.Equal("about:blank", document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ErrorTypeBase_UsesConfiguredBase()
    {
        var executed = await HttpResultExecutor.ExecuteAsync(
            Result.Failure(Error.NotFound("Posts.NotFound", "Missing")).ToHttpResult(),
            services => services.AddGocgunResultsHttp(options => options.ErrorTypeBase = new Uri("https://errors.example.test/catalog")));

        using var document = JsonDocument.Parse(executed.Body);
        Assert.Equal("https://errors.example.test/catalog/posts-not-found", document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Metadata_CannotOverrideProblemMembers()
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "trial_expired",
            ["Type"] = "x",
            ["errors"] = "x",
            ["detail"] = "x",
            ["limit"] = 5,
        };
        var executed = await HttpResultExecutor.ExecuteAsync(Result.Failure(Error.PaymentRequired("PlanLimit", "Upgrade.", metadata)).ToHttpResult());

        using var document = JsonDocument.Parse(executed.Body);
        Assert.Equal(StatusCodes.Status402PaymentRequired, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Upgrade.", document.RootElement.GetProperty("detail").GetString());
        Assert.False(document.RootElement.TryGetProperty("errors", out _));
        Assert.Equal(5, document.RootElement.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task Failure_IsNever200AndKeepsCode()
    {
        Error[] errors =
        [
            Error.Validation("Validation", "Bad", new Dictionary<string, string[]>(StringComparer.Ordinal) { ["name"] = ["Required"] }),
            Error.Unauthorized("InvalidCredentials", "No"),
            Error.PaymentRequired("PlanLimitExceeded", "Limit", new Dictionary<string, object?>(StringComparer.Ordinal) { ["max"] = 1 }),
            Error.Forbidden("EmailNotConfirmed", "No"),
            Error.NotFound("Posts.NotFound", "Missing"),
            Error.Conflict("SlotOccupied", "Taken"),
            Error.Concurrency("ConcurrencyConflict", "Stale"),
            Error.UnsupportedMediaType("UnsupportedMedia", "No"),
            Error.BusinessRule("ConditionNotSupported", "No"),
            Error.RateLimited("RateLimited", "Slow", new Dictionary<string, object?>(StringComparer.Ordinal) { ["retryAfterSeconds"] = 1 }),
        ];

        foreach (var error in errors)
        {
            await AssertKeepsCode(Result.Failure(error).ToHttpResult(), error.Code);
            await AssertKeepsCode(Result.Failure<string>(error).ToHttpResult(), error.Code);
            await AssertKeepsCode(ResultPaginated<string>.Failure(error).ToHttpResult(), error.Code);
            await AssertKeepsCode(Result.Failure<string>(error).ToCreatedHttpResult("/api/items/1"), error.Code);
        }
    }

    [Fact]
    public void DefinedErrorTypes_DoNotMapTo200Or500()
    {
        foreach (var type in Enum.GetValues<ErrorType>())
        {
            var status = ResultHttpExtensions.GetStatusCode(type);
            Assert.NotEqual(StatusCodes.Status200OK, status);
            Assert.NotEqual(StatusCodes.Status500InternalServerError, status);
        }
    }

    [Fact]
    public void ResultsAssembly_DoesNotReferenceAspNetCore()
    {
        var referenced = typeof(Result).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(referenced, assembly => assembly.Name is not null && assembly.Name.Contains("AspNetCore", StringComparison.Ordinal));
    }

    private static async Task AssertKeepsCode(IResult result, string code)
    {
        var executed = await HttpResultExecutor.ExecuteAsync(result);
        AssertProblem(executed, executed.Status, code);
        Assert.NotEqual(StatusCodes.Status200OK, executed.Status);
    }

    private static void Catalog(IServiceCollection services) =>
        services.AddGocgunResultsHttp(options => options.ErrorTypeBase = new Uri("https://errors.example.test/"));

    private static void AssertProblem(ExecutedHttpResult executed, int status, string code)
    {
        Assert.Equal(status, executed.Status);
        Assert.NotEqual(StatusCodes.Status200OK, executed.Status);
        Assert.Contains("application/problem+json", executed.ContentType, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(executed.Body);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(status, document.RootElement.GetProperty("status").GetInt32());
    }
}

internal static class HttpResultExecutor
{
    internal static async Task<ExecutedHttpResult> ExecuteAsync(IResult result, Action<IServiceCollection>? configure = null)
    {
        var context = new DefaultHttpContext();
        var services = new ServiceCollection().AddLogging();
        configure?.Invoke(services);
        context.RequestServices = services.BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/social-accounts";
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return new ExecutedHttpResult(
            context.Response.StatusCode,
            body,
            context.Response.ContentType,
            context.Response.Headers.RetryAfter.ToString(),
            context.Response.Headers.Location.ToString());
    }
}

internal sealed record ExecutedHttpResult(int Status, string Body, string? ContentType, string RetryAfter, string Location);
