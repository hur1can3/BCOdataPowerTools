using System;
using System.Net.Http;

using BusinessCentral.OData.Client.Configuration;
using BusinessCentral.OData.Client.Http;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace BusinessCentral.OData.Client.Extensions;

/// <summary>
/// Provides extension methods for setting up the BusinessCentralClient in an IServiceCollection.
/// </summary>
public static class BusinessCentralClientServiceCollectionExtensions
{
    /// <summary>
    /// Adds the IBusinessCentralClient to the service collection with logging,
    /// configuration, and resilient HTTP policies.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="configureOptions">An action to configure the client options.</param>
    /// <param name="configureBuilder">An optional action to further configure the IHttpClientBuilder, e.g., to add custom delegating handlers for authentication.</param>
    /// <returns>The IHttpClientBuilder for further customization.</returns>
    public static IHttpClientBuilder AddBusinessCentralClient(
        this IServiceCollection services,
        Action<BusinessCentralClientOptions> configureOptions,
        Action<IHttpClientBuilder>? configureBuilder = null)
    {
        services.Configure(configureOptions);

        var options = new BusinessCentralClientOptions();
        configureOptions(options);

        // Register the client with its interface. This allows consumers to inject IBusinessCentralClient.
        var httpClientBuilder = services.AddHttpClient<IBusinessCentralClient, BusinessCentralClient>((serviceProvider, client) =>
        {
            if (string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                throw new InvalidOperationException("BaseUrl cannot be empty. Please configure it in BusinessCentralClientOptions.");
            }

            client.BaseAddress = new Uri(options.BaseUrl);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Allow consumer to add their own handlers (e.g., for authentication)
        configureBuilder?.Invoke(httpClientBuilder);

        return httpClientBuilder;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Handles 5xx, 408, and HttpRequestException
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
    }
}
