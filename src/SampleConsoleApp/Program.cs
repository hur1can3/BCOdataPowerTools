using BusinessCentral.OData.Client.Configuration;
using BusinessCentral.OData.Client.Extensions;
using BusinessCentral.OData.Client.Http;
using BusinessCentral.OData.Client.Models;
using BusinessCentral.OData.Client.Querying;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

// --- POCO Classes (Example of what the scaffolder tool generates) ---

#region POCOs
public class Customer
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("balance")]
    public decimal? Balance { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("lastModifiedDateTime")]
    public DateTimeOffset? LastModifiedDateTime { get; set; }
}

public class Item
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("number")]
    public string? Number { get; set; }
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
    [JsonPropertyName("unitPrice")]
    public decimal? UnitPrice { get; set; }
}

public class SalesOrder
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("number")]
    public string? Number { get; set; }
    [JsonPropertyName("orderDate")]
    public DateTime? OrderDate { get; set; }
    [JsonPropertyName("customerNumber")]
    public string? CustomerNumber { get; set; }
    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }
    public Customer? Customer { get; set; } // Populated by $expand
    [JsonPropertyName("salesOrderLines")]
    public List<SalesOrderLine>? SalesOrderLines { get; set; } // Populated by $expand
}

public class SalesOrderLine
{
    [JsonPropertyName("documentId")]
    public Guid DocumentId { get; set; }
    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; set; }
}

// Custom DTO for receiving aggregation results from an $apply query
public class ItemSalesSummary
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("totalQuantity")]
    public decimal TotalQuantity { get; set; }
}
#endregion


// --- Main Application Entry Point ---
public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices((hostContext, services) =>
            {
                var configuration = hostContext.Configuration;

                // 1. Add the BusinessCentralClient using the extension method from the library
                services.AddBusinessCentralClient(
                    options =>
                    {
                        // Bind from your appsettings.json
                        configuration.GetSection(BusinessCentralClientOptions.ConfigurationSectionName).Bind(options);
                        
                        // Example: Overriding settings/providing custom serialization
                        options.JsonSerializerOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase // Example of custom setting
                        };
                    },
                    httpClientBuilder =>
                    {
                        // Example: Add a custom delegating handler for authentication
                        httpClientBuilder.AddHttpMessageHandler(() => 
                            new AuthenticationDelegatingHandler(configuration["Authentication:BearerToken"] ?? string.Empty));
                    }
                );
                
                // Example: Registering a custom, derived client for advanced scenarios
                services.AddScoped<CustomBusinessCentralClient>();
                
                // 3. Register our main application logic service
                services.AddHostedService<FullFeatureDemoRunner>();
            })
            .Build();

        await host.RunAsync();
    }
}


// --- Main Application Logic ---
public class FullFeatureDemoRunner : IHostedService
{
    private readonly ILogger<FullFeatureDemoRunner> _logger;
    private readonly IBusinessCentralClient _bcClient;
    private readonly CustomBusinessCentralClient _customClient;

    public FullFeatureDemoRunner(ILogger<FullFeatureDemoRunner> logger, IBusinessCentralClient bcClient, CustomBusinessCentralClient customClient)
    {
        _logger = logger;
        _bcClient = bcClient;
        _customClient = customClient;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("--- BCOdataPowerTools Full Feature Demo Starting ---");

        try
        {
            await DemoSelectAndFilterAsync();
            await DemoMultiLevelSortingAsync();
            await DemoClientPagingWithCountAsync();
            await DemoServerPagingAsync();
            await DemoExpandWithNestedSelectAsync();
            await DemoAggregationWithApplyAsync();
            await DemoBatchQueryAsync();
            await DemoCustomBuilderAndClientAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during the demo.");
        }
        
        _logger.LogInformation("\n--- Demo Finished ---");
    }

    #region Demo Methods
    private async Task DemoSelectAndFilterAsync()
    {
        _logger.LogInformation("\n[1. DEMO: $filter, $select, and $orderby]");
        _logger.LogInformation("  - GOAL: Get top 5 customers from US or GB with a balance, showing only their Number, Name, and Balance.");
        
        var query = new ODataQueryBuilder<Customer>()
            .Filter(c => c.Balance > 0 && (c.Country == "US" || c.Country == "GB"))
            .OrderByDescending(c => c.Balance)
            .Select(c => new { c.Number, c.DisplayName, c.Balance })
            .Top(5);

        var response = await _bcClient.GetEntitiesAsync(query);
        response.Value?.ForEach(c => _logger.LogInformation("    - Customer: {Number}, Name: {Name}, Balance: {Balance:C}", c.Number, c.DisplayName, c.Balance));
    }

    private async Task DemoMultiLevelSortingAsync()
    {
        _logger.LogInformation("\n[2. DEMO: Multi-level Sorting with $orderby, ThenBy, and ThenByDescending]");
        _logger.LogInformation("  - GOAL: Get top 10 customers, sorted first by Country (ascending) and then by Balance (descending).");
        
        var query = new ODataQueryBuilder<Customer>()
            .OrderBy(c => c.Country!)
            .ThenByDescending(c => c.Balance!)
            .Top(10);
        
        var response = await _bcClient.GetEntitiesAsync(query);
        response.Value?.ForEach(c => _logger.LogInformation("    - Customer: Country={Country}, Name={Name}, Balance={Balance:C}", c.Country, c.DisplayName, c.Balance));
    }
    
    private async Task DemoClientPagingWithCountAsync()
    {
        _logger.LogInformation("\n[3. DEMO: Client Paging with $top, $skip, and $count]");
        _logger.LogInformation("  - GOAL: Get items 5 and 6 from the item list and also request the total count of all items.");

        var query = new ODataQueryBuilder<Item>()
            .OrderBy(i => i.Number)
            .Top(2)
            .Skip(4)
            .Count(true); // OData Query: $top=2&$skip=4&$count=true

        var response = await _bcClient.GetEntitiesAsync(query);
        _logger.LogInformation("  - NOTE: The total count is returned in the response metadata. The client library would need a property to expose '@odata.count'.");
        response.Value?.ForEach(i => _logger.LogInformation("    - Item: {Number}, Name: {Name}", i.Number, i.DisplayName));
    }

    private async Task DemoServerPagingAsync()
    {
        _logger.LogInformation("\n[4. DEMO: Server-Side Pagination with GetAllPagesAsync]");
        _logger.LogInformation("  - GOAL: Use GetAllPagesAsync to automatically follow all @odata.nextLink pages and retrieve all items.");
        var query = new ODataQueryBuilder<Item>();
        var allItems = await _bcClient.GetAllPagesAsync(query);
        _logger.LogInformation("  - Total items found across all pages: {Count}", allItems.Count);
    }

    private async Task DemoExpandWithNestedSelectAsync()
    {
        _logger.LogInformation("\n[5. DEMO: $expand with nested $select and $filter]");
        _logger.LogInformation("  - GOAL: Get the latest sales order, expand its customer (only name), and its lines (only certain fields, where quantity > 5).");
        
        var query = new ODataQueryBuilder<SalesOrder>()
            .OrderByDescending(so => so.OrderDate)
            .Expand(so => so.Customer!, c => c.Select(cust => new { cust.DisplayName })) // $expand=Customer($select=displayName)
            .Expand(so => so.SalesOrderLines!, linesQuery =>  // $expand=salesOrderLines($select=...&$filter=...)
            {
                linesQuery.Select(line => new { line.LineNumber, line.Description, line.Quantity })
                          .Filter(line => line.Quantity > 5);
            })
            .Top(1);
        
        var response = await _bcClient.GetEntitiesAsync(query);
        var latestOrder = response.Value?.FirstOrDefault();

        if (latestOrder != null)
        {
            _logger.LogInformation("  - Found Order #{OrderNumber} for Customer: {CustomerName}", latestOrder.Number, latestOrder.Customer?.DisplayName ?? "N/A");
            latestOrder.SalesOrderLines?.ForEach(line => 
            {
                _logger.LogInformation("    - Line {LineNum}: {Desc}, Quantity: {Qty}", line.LineNumber, line.Description, line.Quantity);
            });
        }
    }

    private async Task DemoAggregationWithApplyAsync()
    {
        _logger.LogInformation("\n[6. DEMO: Aggregation with $apply, groupby, and sum]");
        _logger.LogInformation("  - GOAL: Group all sales order lines by description and sum the total quantity for each, effectively creating a sales summary report.");

        // OData Query: $apply=groupby((description), aggregate(quantity with sum as totalQuantity))
        var query = new ODataQueryBuilder<SalesOrderLine>()
            .Apply(agg => agg
                .GroupBy(sol => sol.Description!)
                .Aggregate(sol => sol.Quantity!, "sum", "totalQuantity") 
            );

        var response = await _bcClient.GetEntitiesAsync(query);
        // The response type is our custom DTO
        var summaries = response.Value?.Cast<JsonElement>().Select(e => e.Deserialize<ItemSalesSummary>(_jsonOptions)).ToList();
        
        summaries?.Take(5).ToList().ForEach(summary => 
            _logger.LogInformation("    - Item: '{ItemDesc}', Total Quantity Sold: {Total}", summary.Description, summary.TotalQuantity)
        );
    }

    private async Task DemoBatchQueryAsync()
    {
        _logger.LogInformation("\n[7. DEMO: $batch Query]");
        _logger.LogInformation("  - GOAL: Get the top customer by balance and the most expensive item in a single, efficient HTTP request.");
        var customerQuery = new ODataQueryBuilder<Customer>().OrderByDescending(c => c.Balance).Top(1);
        var itemQuery = new ODataQueryBuilder<Item>().OrderByDescending(i => i.UnitPrice).Top(1);

        var batchResponse = await _bcClient.SendJsonBatchAsync(default, customerQuery, itemQuery);
        // ... (rest of batch logic) ...
    }

    private async Task DemoCustomBuilderAndClientAsync()
    {
        _logger.LogInformation("\n[8. DEMO: Extensibility - Using a custom query builder and derived client]");
        _logger.LogInformation("  - GOAL: Show how developers can create shortcut methods on a custom builder and override client behavior.");
        
        // This builder has a custom .WithHighValue() method that encapsulates filter logic
        var customQuery = new CustomSalesOrderQueryBuilder()
            .WithHighValue(10000) // Our custom method
            .OrderByDescending(so => so.OrderDate)
            .Top(1);

        // This client adds a custom header to every request (check logs to see the URI)
        var response = await _customClient.GetEntitiesAsync(customQuery);
        
        var order = response.Value?.FirstOrDefault();
        if(order != null)
        {
            _logger.LogInformation("    - Found high-value order using custom builder: #{Number} with Amount {Amount:C}", order.Number, order.Amount);
        }
    }
    #endregion

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


// --- Extensibility Examples ---
#region Extensibility
// Example of a custom builder with a shortcut method
public class CustomSalesOrderQueryBuilder : ODataQueryBuilder<SalesOrder>
{
    public CustomSalesOrderQueryBuilder WithHighValue(decimal threshold)
    {
        this.Filter(so => so.Amount > threshold); 
        return this;
    }
}

// Example of a custom client that overrides behavior to add a tracking parameter
public class CustomBusinessCentralClient : BusinessCentralClient
{
    private readonly ILogger<CustomBusinessCentralClient> _logger;
    public CustomBusinessCentralClient(HttpClient httpClient, IOptions<BusinessCentralClientOptions> options, ILogger<CustomBusinessCentralClient> logger) 
        : base(httpClient, options, logger) 
    {
        _logger = logger;
    }

    protected override string BuildRequestUri(ODataQueryBuilder builder)
    {
        // Add a custom query parameter to every request built by this client
        var baseUri = base.BuildRequestUri(builder);
        var finalUri = $"{baseUri}&source=CustomClient";
        _logger.LogInformation("  - [Custom Client]: Modified request URI to: {Uri}", finalUri);
        return finalUri;
    }
}

// Example of a custom delegating handler for authentication
public class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly string _token;
    public AuthenticationDelegatingHandler(string token) { _token = token; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            throw new InvalidOperationException("BearerToken is not configured.");
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return base.SendAsync(request, cancellationToken);
    }
}
#endregion
