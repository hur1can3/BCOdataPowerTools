using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BusinessCentral.OData.Scaffold;

/// <summary>
/// Defines the contract for the OData POCO scaffolding service.
/// </summary>
public interface IPocoScaffolder
{
    /// <summary>
    /// Executes the scaffolding process based on the configured options.
    /// </summary>
    Task ScaffoldAsync();
}

public record ScaffolderOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public string CompanyId { get; init; } = string.Empty;
    public string BearerToken { get; init; } = string.Empty;
    public string OutputDirectory { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = string.Empty;
    public string? IncludeProperties { get; init; }
    public string? ExcludeProperties { get; init; }
}

/// <summary>
/// The default implementation of the IPocoScaffolder interface.
/// </summary>
public class PocoScaffolder : IPocoScaffolder
{
    private readonly ScaffolderOptions _options;
    private readonly HttpClient _httpClient;
    private readonly HashSet<string>? _includeSet;
    private readonly HashSet<string>? _excludeSet;

    public PocoScaffolder(ScaffolderOptions options)
    {
        _options = options;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.BearerToken);

        // For efficient lookups, parse the comma-separated strings into HashSets
        if (!string.IsNullOrWhiteSpace(_options.IncludeProperties))
        {
            _includeSet = new HashSet<string>(_options.IncludeProperties.Split(','), StringComparer.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(_options.ExcludeProperties))
        {
            _excludeSet = new HashSet<string>(_options.ExcludeProperties.Split(','), StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc/>
    public async Task ScaffoldAsync()
    {
        try
        {
            if (_includeSet != null && _excludeSet != null)
            {
                throw new InvalidOperationException("Cannot specify both --include-props and --exclude-props at the same time.");
            }

            Console.WriteLine("Fetching $metadata...");
            var metadataXml = await FetchMetadataAsync();
            Console.WriteLine("Successfully fetched metadata.");

            Console.WriteLine("Parsing entities...");
            var entities = ParseEntitiesFromMetadata(metadataXml);
            Console.WriteLine($"Found {entities.Count} entities.");

            if (!Directory.Exists(_options.OutputDirectory))
            {
                Directory.CreateDirectory(_options.OutputDirectory);
            }

            Console.WriteLine($"Generating POCOs in '{_options.OutputDirectory}'...");
            int filesWritten = 0;
            foreach (var entity in entities)
            {
                var classCode = GenerateClassCode(entity);

                // Only write the file if there are properties to generate
                if (!string.IsNullOrWhiteSpace(classCode))
                {
                    var filePath = Path.Combine(_options.OutputDirectory, $"{entity.Name}.cs");
                    await File.WriteAllTextAsync(filePath, classCode);
                    filesWritten++;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nScaffolding complete! Wrote {filesWritten} C# class files.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nAn error occurred: {ex.Message}");
            Console.ResetColor();
        }
    }

    private async Task<string> FetchMetadataAsync()
    {
        var metadataUrl = $"{_options.BaseUrl.TrimEnd('/')}/{_options.ApiVersion}/$metadata";
        var response = await _httpClient.GetAsync(metadataUrl);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private List<EntityInfo> ParseEntitiesFromMetadata(string xml)
    {
        var doc = XDocument.Parse(xml);
        XNamespace edm = "http://docs.oasis-open.org/odata/ns/edm";
        var entities = new List<EntityInfo>();

        var entityTypes = doc.Descendants(edm + "EntityType");
        foreach (var type in entityTypes)
        {
            var entity = new EntityInfo
            {
                Name = type.Attribute("Name")?.Value ?? string.Empty,
                Properties = new List<PropertyInfo>(),
            };

            var properties = type.Descendants(edm + "Property");
            foreach (var prop in properties)
            {
                entity.Properties.Add(new PropertyInfo
                {
                    Name = prop.Attribute("Name")?.Value ?? string.Empty,
                    Type = prop.Attribute("Type")?.Value ?? string.Empty,
                });
            }

            entities.Add(entity);
        }

        return entities;
    }

    private string? GenerateClassCode(EntityInfo entity)
    {
        var sb = new StringBuilder();
        var hasProperties = false;

        foreach (var prop in entity.Properties)
        {
            // Filtering logic
            if (_includeSet != null && !_includeSet.Contains(prop.Name))
            {
                continue;
            }

            if (_excludeSet != null && _excludeSet.Contains(prop.Name))
            {
                continue;
            }

            if (!hasProperties)
            {
                // Lazily write the header only if we have properties to generate
                sb.AppendLine("using System.Text.Json.Serialization;");
                sb.AppendLine();
                sb.AppendLine($"namespace {_options.Namespace};");
                sb.AppendLine();
                sb.AppendLine($"public class {entity.Name}");
                sb.AppendLine("{");
                hasProperties = true;
            }

            var csharpType = MapEdmTypeToCSharpType(prop.Type);
            var csharpPropName = ToPascalCase(prop.Name);

            if (csharpPropName != prop.Name)
            {
                sb.AppendLine($"    [JsonPropertyName(\"{prop.Name}\")]");
            }

            sb.AppendLine($"    public {csharpType} {csharpPropName} {{ get; set; }}");
            sb.AppendLine();
        }

        if (hasProperties)
        {
            sb.AppendLine("}");
            return sb.ToString();
        }

        // Return null if no properties were generated for this entity
        return null;
    }

    private string MapEdmTypeToCSharpType(string edmType)
    {
        var cleanType = edmType.StartsWith("Edm.") ? edmType.Substring(4) : edmType;

        return cleanType switch
        {
            "String" => "string?",
            "Guid" => "Guid?",
            "Boolean" => "bool?",
            "Byte" => "byte?",
            "SByte" => "sbyte?",
            "Int16" => "short?",
            "Int32" => "int?",
            "Int64" => "long?",
            "Single" => "float?",
            "Double" => "double?",
            "Decimal" => "decimal?",
            "Date" => "System.DateTime?",
            "DateTimeOffset" => "System.DateTimeOffset?",
            "Duration" => "System.TimeSpan?",
            "TimeOfDay" => "System.TimeSpan?",
            "Binary" => "byte[]?",
            _ => "object?", // Fallback for complex types or unknown types
        };
    }

    private string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var words = Regex.Split(input, @"[_\s-]")
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant());
        return string.Concat(words);
    }
}

internal record EntityInfo
{
    public string Name { get; init; } = string.Empty;
    public List<PropertyInfo> Properties { get; init; } = new();
}

internal record PropertyInfo
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}
