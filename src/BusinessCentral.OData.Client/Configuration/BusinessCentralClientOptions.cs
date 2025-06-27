namespace BusinessCentral.OData.Client.Configuration;

/// <summary>
/// Provides configuration options for the BusinessCentralClient.
/// This can be populated from appsettings.json or configured in code.
/// </summary>
public class BusinessCentralClientOptions
{
    /// <summary>
    /// The name of the configuration section in appsettings.json.
    /// </summary>
    public const string ConfigurationSectionName = "BusinessCentralClient";

    /// <summary>
    /// The base URL of the Business Central environment (e.g., "https://api.businesscentral.dynamics.com").
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.businesscentral.dynamics.com";

    /// <summary>
    /// The ID of the company (tenant) to interact with.
    /// </summary>
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>
    /// The OData API version to use.
    /// </summary>
    public string ApiVersion { get; set; } = "v2.0";

    /// <summary>
    //  Custom JSON serialization options. If null, default settings are used.
    /// This allows consumers to override serialization behavior for custom data types.
    /// </summary>
    public System.Text.Json.JsonSerializerOptions? JsonSerializerOptions { get; set; }
}
