using System.Net;

using BusinessCentral.OData.Client.Models;

namespace BusinessCentral.OData.Client.Exceptions;

/// <summary>
/// A custom exception for handling OData API errors. It includes the HTTP status code
/// and the detailed error response from the API.
/// </summary>
public class ODataException : Exception
{
    /// <summary>
    /// Gets the HTTP status code returned by the API.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets the detailed error information from the API, if available.
    /// </summary>
    public ODataError? ApiError { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="apiError">The detailed API error.</param>
    /// <param name="innerException">The inner exception.</param>
    public ODataException(string message, HttpStatusCode statusCode, ODataError? apiError, Exception? innerException = null)
        : base(apiError?.Message ?? message, innerException)
    {
        StatusCode = statusCode;
        ApiError = apiError;
    }
}
