using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusinessCentral.OData.Client.Models;

/// <summary>
/// A generic wrapper for all OData responses. It can encapsulate a single entity,
/// a list of entities, pagination links, and batch responses.
/// </summary>
/// <typeparam name="T">The entity type of the expected data.</typeparam>
public class ODataResponse<T>
{
    /// <summary>
    /// Gets or sets for GET /entityset responses, holds the list of entities.
    /// </summary>
    [JsonPropertyName("value")]
    public List<T>? Value { get; set; }

    /// <summary>
    /// Gets or sets the URL to retrieve the next page of results in server-driven pagination.
    /// </summary>
    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; set; }

    /// <summary>
    /// Gets or sets for $batch requests, this holds the collection of individual responses.
    /// </summary>
    [JsonPropertyName("responses")]
    public List<ODataBatchResponse>? BatchResponses { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code of the response.
    /// </summary>
    [JsonIgnore]
    public HttpStatusCode StatusCode { get; set; }

    /// <summary>
    /// Gets a value indicating whether indicates whether the HTTP request was successful (2xx status code).
    /// </summary>
    [JsonIgnore]
    public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;
}

/// <summary>
/// Represents a single response within a $batch operation response.
/// </summary>
public class ODataBatchResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets or sets the body of the individual batch response. Use the DeserializeBodyAs method to
    /// get a strongly-typed object from this.
    /// </summary>
    [JsonPropertyName("body")]
    public JsonElement Body { get; set; }

    /// <summary>
    /// Deserializes the body of the batch response into a specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the body into.</typeparam>
    /// <param name="options">Optional JsonSerializerOptions.</param>
    /// <returns>The deserialized object, or null if deserialization fails.</returns>
    public T? DeserializeBodyAs<T>(JsonSerializerOptions? options = null)
    {
        try
        {
            return Body.Deserialize<T>(options ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return default;
        }
    }
}

/// <summary>
/// Represents the detailed error message structure returned by the Business Central OData API.
/// </summary>
public class ODataError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

// Ensure the IsExternalInit class is defined for compatibility with 'required' keyword
public class IsExternalInit { }

/// <summary>
/// Represents the top-level object for an OData JSON batch request.
/// </summary>
public record ODataJsonBatchRequest
{
    [JsonPropertyName("requests")]
    public required ODataJsonBatchRequestItem[] Requests { get; init; }
}

/// <summary>
/// Represents a single request within an OData JSON batch payload.
/// </summary>
public record ODataJsonBatchRequestItem
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; } = "GET";

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; init; } = new() { { "Accept", "application/json" } };
}
