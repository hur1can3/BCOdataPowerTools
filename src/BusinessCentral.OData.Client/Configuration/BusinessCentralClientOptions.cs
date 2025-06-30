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
    /// Gets or sets the base URL of the Business Central environment (e.g., "https://api.businesscentral.dynamics.com").
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.businesscentral.dynamics.com";

    /// <summary>
    /// Gets or sets the ID of the company (tenant) to interact with.
    /// </summary>
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OData API version to use.
    /// </summary>
    public string ApiVersion { get; set; } = "v2.0";

    /// <summary>
    /// Gets or sets custom JSON serialization options. If null, default settings are used.
    /// This allows consumers to override serialization behavior for custom data types.
    /// </summary>
    public System.Text.Json.JsonSerializerOptions? JsonSerializerOptions { get; set; }
}
