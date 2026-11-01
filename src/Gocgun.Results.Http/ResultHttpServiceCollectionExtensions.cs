using Microsoft.Extensions.DependencyInjection;

namespace Gocgun.Results.Http;

/// <summary>Registers <see cref="ResultHttpOptions"/>.</summary>
public static class ResultHttpServiceCollectionExtensions
{
    /// <summary>Configures how failed results are written as RFC 9457 problems.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Options callback.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddGocgunResultsHttp(this IServiceCollection services, Action<ResultHttpOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<ResultHttpOptions>().Configure(configure);
        return services;
    }
}
