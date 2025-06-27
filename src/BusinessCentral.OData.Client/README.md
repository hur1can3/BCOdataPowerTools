```
# BusinessCentral.OData.Client Library

A modern, resilient, and extensible .NET client for interacting with the Microsoft Dynamics 365 Business Central OData API.

This library is built for .NET Standard 2.0 and .NET 8+, following best practices including dependency injection, `IHttpClientFactory` integration, resilience with Polly, and structured logging.

## Features

-   **Fluent Query Builder**: Construct complex OData queries (`$filter`, `$select`, `$expand`, `$orderby`, `$apply`, `$count`) in a strongly-typed, refactor-safe way.
-   **Resilient by Default**: Automatically handles transient network faults with built-in retry and circuit-breaker policies via Polly.
-   **Modern & Extensible**: Designed for dependency injection and easy extension with custom `DelegatingHandler`s for authentication or by inheriting from the client/builder classes.
-   **Automatic Pagination**: A simple `GetAllPagesAsync` method transparently handles server-driven pagination via `@odata.nextLink`.
-   **Efficient Batching**: Supports `application/json` `$batch` requests to execute multiple operations in a single network call.
-   **Structured Error Handling**: Provides a custom `ODataException` with detailed error information from the API.

## Installation

Install the package from NuGet:

```powershell
Install-Package BusinessCentral.OData.Client
```

## Getting Started: Configuration

In your application's startup file (`Program.cs` or `Startup.cs`), register the client using the provided extension method.

```
// In Program.cs
using BusinessCentral.OData.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBusinessCentralClient(options =>
{
    // Bind from your appsettings.json
    builder.Configuration.GetSection("BusinessCentralClient").Bind(options);

    // Or configure manually
    // options.BaseUrl = "[https://api.businesscentral.dynamics.com](https://api.businesscentral.dynamics.com)";
    // options.CompanyId = "YOUR_COMPANY_ID_GUID";
});
```

**appsettings.json:**

```
{
  "BusinessCentralClient": {
    "BaseUrl": "[https://api.businesscentral.dynamics.com](https://api.businesscentral.dynamics.com)",
    "CompanyId": "YOUR_COMPANY_ID_GUID"
  }
}
```

## Usage Examples

Inject `IBusinessCentralClient` into your services or controllers.

### Basic Filtering and Selecting

```
var query = new ODataQueryBuilder<Customer>()
    .Filter(c => c.Balance > 1000 && c.Country == "US")
    .OrderByDescending(c => c.Balance)
    .Select(c => new { c.Number, c.DisplayName, c.Balance })
    .Top(10);

var response = await client.GetEntitiesAsync(query);
// response.Value will contain a list of up to 10 customers
```

### Expanding Related Entities (with Nested Select)

```
// Get the latest sales order, expand its customer (only name), 
// and its lines (only certain fields, where quantity > 5).
var query = new ODataQueryBuilder<SalesOrder>()
    .OrderByDescending(so => so.OrderDate)
    .Expand(so => so.Customer!, c => c.Select(cust => new { cust.DisplayName })) 
    .Expand(so => so.SalesOrderLines!, linesQuery =>
    {
        linesQuery.Select(line => new { line.LineNumber, line.Description })
                  .Filter(line => line.Quantity > 5);
    })
    .Top(1);

var response = await client.GetEntitiesAsync(query);
var latestOrder = response.Value?.FirstOrDefault();
```

### Aggregation with `$apply`

```
// Group all sales order lines by description and sum the total quantity.
var query = new ODataQueryBuilder<SalesOrderLine>()
    .Apply(agg => agg
        .GroupBy(sol => sol.Description!)
        .Aggregate(sol => sol.Quantity!, "sum", "totalQuantity") 
    );

// The response type will be a custom DTO.
var response = await client.GetEntitiesAsync(query);
var summaries = response.Value?.Cast<JsonElement>().Select(e => e.Deserialize<ItemSalesSummary>()).ToList();
```

### Automatic Pagination

```
// This method handles all @odata.nextLink pages automatically.
// Use with caution on large datasets as it can consume significant resources.
var allItems = await client.GetAllPagesAsync(new ODataQueryBuilder<Item>());
```

## Advanced Customization

### Custom Authentication Handler

The recommended way to handle authentication is with a `DelegatingHandler`.

1. **Create the handler:**
   
   ```
   public class MyAuthHandler : DelegatingHandler
   {
      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
          var token = "YOUR_ACQUIRED_TOKEN"; // Logic to acquire your bearer token
          request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
          return await base.SendAsync(request, cancellationToken);
      }
   }
   ```

2. **Register it during setup:**
   
   ```
   // In Program.cs
   services.AddTransient<MyAuthHandler>();
   
   services.AddBusinessCentralClient(
      options => { /* ... */ },
      builder => 
      {
          // Add your custom handler to the HttpClient pipeline
          builder.AddHttpMessageHandler<MyAuthHandler>();
      }
   );
   ```
