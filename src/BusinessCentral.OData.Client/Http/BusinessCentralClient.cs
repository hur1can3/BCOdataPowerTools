using BusinessCentral.OData.Client.Configuration;
using BusinessCentral.OData.Client.Exceptions;
using BusinessCentral.OData.Client.Models;
using BusinessCentral.OData.Client.Querying;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusinessCentral.OData.Client.Http;

/// <summary>
/// Defines the contract for a client that interacts with the Business Central OData API.
/// </summary>
public interface IBusinessCentralClient
{
    /// <summary>
    /// Retrieves a paged list of entities based on the query builder.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="queryBuilder">The fluent query builder instance.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>An ODataResponse containing the list of entities and pagination information.</returns>
    Task<ODataResponse<T>> GetEntitiesAsync<T>(ODataQueryBuilder<T> queryBuilder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Automatically handles server-driven pagination to retrieve all entities from all pages.
    /// Warning: This can result in a large number of HTTP requests and consume significant memory.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="queryBuilder">The fluent query builder instance.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>A complete list of all entities matching the query.</returns>
    Task<List<T>> GetAllPagesAsync<T>(ODataQueryBuilder<T> queryBuilder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends multiple queries as a single, efficient JSON-formatted $batch request.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <param name="builders">An array of query builders, one for each request in the batch.</param>
    /// <returns>An ODataResponse containing a list of individual batch responses.</returns>
    Task<ODataResponse<object>> SendJsonBatchAsync(CancellationToken cancellationToken = default, params ODataQueryBuilder[] builders);
}


/// <summary>
/// The default implementation for IBusinessCentralClient.
/// </summary>
public class BusinessCentralClient : IBusinessCentralClient
{
    private readonly HttpClient _httpClient;
    private readonly BusinessCentralClientOptions _options;
    private readonly ILogger<BusinessCentralClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessCentralClient"/> class.
    /// This is typically done via dependency injection.
    /// </summary>
    public BusinessCentralClient(
        HttpClient httpClient,
        IOptions<BusinessCentralClientOptions> options,
        ILogger<BusinessCentralClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Use custom serializer options if provided, otherwise create default ones.
        _jsonOptions = _options.JsonSerializerOptions ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc/>
    public virtual async Task<ODataResponse<T>> GetEntitiesAsync<T>(ODataQueryBuilder<T> queryBuilder, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildRequestUri(queryBuilder);
        _logger.LogDebug("Sending GET request to {RequestUri}", requestUri);

        var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        return await ProcessResponseAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual async Task<List<T>> GetAllPagesAsync<T>(ODataQueryBuilder<T> queryBuilder, CancellationToken cancellationToken = default)
    {
        var allEntities = new List<T>();
        _logger.LogInformation("Starting to fetch all pages for entity {EntityName}.", typeof(T).Name);

        // This removes any client-side paging to ensure server-driven paging works correctly.
        queryBuilder.Top(0).Skip(0);

        var response = await GetEntitiesAsync(queryBuilder, cancellationToken).ConfigureAwait(false);
        
        while(response != null && response.IsSuccessStatusCode)
        {
            if (response.Value != null)
            {
                allEntities.AddRange(response.Value);
                _logger.LogDebug("Fetched {Count} entities from a page. Total so far: {Total}", response.Value.Count, allEntities.Count);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Operation was cancelled during pagination.");
                break;
            }

            if (!string.IsNullOrEmpty(response.NextLink))
            {
                 _logger.LogDebug("Following nextLink for pagination.");
                 var nextResponse = await _httpClient.GetAsync(response.NextLink, cancellationToken).ConfigureAwait(false);
                 response = await ProcessResponseAsync<T>(nextResponse, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation("Finished pagination. No more nextLinks found.");
                break; // No more pages
            }
        }

        _logger.LogInformation("Successfully fetched a total of {TotalCount} entities for {EntityName} across all pages.", allEntities.Count, typeof(T).Name);
        return allEntities;
    }
    
    /// <inheritdoc/>
    public virtual async Task<ODataResponse<object>> SendJsonBatchAsync(CancellationToken cancellationToken = default, params ODataQueryBuilder[] builders)
    {
        _logger.LogInformation("Constructing JSON batch request with {RequestCount} operations.", builders.Length);

        var requestItems = builders.Select((builder, index) =>
        {
            var entityName = builder.GetEntityTypeName();
            // The URL for JSON batching must be relative to the API version root.
            var relativeUrl = $"/companies({_options.CompanyId})/{entityName}?{builder.ToQueryString()}";
            return new ODataJsonBatchRequestItem
            {
                Id = $"{index + 1}",
                Url = relativeUrl
            };
        }).ToArray();

        var batchRequest = new ODataJsonBatchRequest { Requests = requestItems };
        var requestUri = $"{_options.ApiVersion}/$batch";
        
        _logger.LogDebug("Sending JSON batch POST request to {BatchEndpoint}", requestUri);
        var response = await _httpClient.PostAsJsonAsync(requestUri, batchRequest, _jsonOptions, cancellationToken).ConfigureAwait(false);

        return await ProcessResponseAsync<object>(response, cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Builds the request URI for a standard entity query. Can be overridden in a derived class for custom URL construction.
    /// </summary>
    protected virtual string BuildRequestUri(ODataQueryBuilder builder)
    {
        var entityName = builder.GetEntityTypeName();
        var queryString = builder.ToQueryString();
        // The final URL is relative to the BaseAddress configured in HttpClient.
        return $"{_options.ApiVersion}/companies({_options.CompanyId})/{entityName}?{queryString}";
    }

    /// <summary>
    /// Processes the HttpResponseMessage, handling success and error cases. Can be overridden for custom response handling.
    /// </summary>
    protected virtual async Task<ODataResponse<T>> ProcessResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var odataResponse = await JsonSerializer.DeserializeAsync<ODataResponse<T>>(responseStream, _jsonOptions, cancellationToken).ConfigureAwait(false);

            if (odataResponse == null) return new ODataResponse<T> { StatusCode = response.StatusCode };
            
            odataResponse.StatusCode = response.StatusCode;
            return odataResponse;
        }

        string errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var requestUri = response.RequestMessage?.RequestUri;
        _logger.LogError("API request to {RequestUri} failed with status {StatusCode}. Response: {ErrorContent}", 
            requestUri, response.StatusCode, errorContent);

        ODataError? apiError = null;
        try
        {
            var errorDoc = JsonDocument.Parse(errorContent);
            if (errorDoc.RootElement.TryGetProperty("error", out var errorElement))
            {
                 apiError = errorElement.Deserialize<ODataError>(_jsonOptions);
            }
        }
        catch (JsonException ex) 
        { 
            _logger.LogError(ex, "Failed to parse API error response JSON for request {RequestUri}.", requestUri);
        }
        
        throw new ODataException(
            $"API request failed with status code {response.StatusCode}",
            response.StatusCode,
            apiError
        );
    }
}
