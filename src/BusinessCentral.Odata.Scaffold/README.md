```
# BusinessCentral.OData.Scaffold Tool

A .NET Global Tool to scaffold C# POCO classes from the Microsoft Dynamics 365 Business Central OData `$metadata` endpoint. This tool is a companion to the `BusinessCentral.OData.Client` library.

## What It Does

This tool connects to your Business Central environment, reads the OData service metadata, and automatically generates C# class files (`.cs`) for each entity. The generated classes include `[JsonPropertyName]` attributes, making them immediately usable with `System.Text.Json` and the `BusinessCentral.OData.Client` library.

This saves you from manually creating dozens or hundreds of POCO classes.

## Installation

Install the tool globally from NuGet. Make sure you have the .NET 8 SDK or later installed.

```powershell
dotnet tool install --global BusinessCentral.OData.Scaffold
```

To update the tool to the latest version:

```
dotnet tool update --global BusinessCentral.OData.Scaffold
```

## Usage

Run the tool from your command line. You must provide the API URL, your company ID, and a valid bearer token.

```
dotnet-bc-scaffold --url <your-api-url> --company-id <your-company-guid> --token <your-bearer-token> [options]
```

### Options

| **Option**        | **Short** | **Description**                                                                                         | **Required** | **Default**        |
| ----------------- | --------- | ------------------------------------------------------------------------------------------------------- | ------------ | ------------------ |
| `--url`           |           | The base URL of the Business Central OData API (e.g., `https://api.businesscentral.dynamics.com`).      | **Yes**      |                    |
| `--company-id`    |           | The ID (GUID) of the company/tenant.                                                                    | **Yes**      |                    |
| `--token`         |           | The OAuth 2.0 Bearer token for authentication.                                                          | **Yes**      |                    |
| `--output`        | `-o`      | The output directory for the generated C# files.                                                        | No           | `./GeneratedPocos` |
| `--namespace`     | `-n`      | The namespace for the generated C# classes.                                                             | No           | `MyProject.Pocos`  |
| `--api-version`   |           | The OData API version to use.                                                                           | No           | `v2.0`             |
| `--include-props` |           | A comma-separated list of properties to include. If specified, only these properties will be generated. | No           |                    |
| `--exclude-props` |           | A comma-separated list of properties to exclude from generation.                                        | No           |                    |

**Note:** `--include-props` and `--exclude-props` cannot be used at the same time.

### Example: Basic Scaffolding

This command generates POCOs for all entities and all their properties.

```
dotnet-bc-scaffold ^
  --url "[https://api.businesscentral.dynamics.com](https://api.businesscentral.dynamics.com)" ^
  --company-id "a1b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d6" ^
  --token "eyJ0eXAiOiJKV1QiLCJhbGciOi..." ^
  --output "./src/MyWebApp/Data/Pocos" ^
  --namespace "MyWebApp.Data.Pocos"
```

### Example: Scaffolding with Property Filtering

This command will generate POCOs for all entities, but each class will **only contain** the `id`, `number`, and `displayName` properties (if they exist on that entity). This is useful for creating lean DTOs.

```
dotnet-bc-scaffold ^
  --url "[https://api.businesscentral.dynamics.com](https://api.businesscentral.dynamics.com)" ^
  --company-id "a1b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d6" ^
  --token "eyJ0eXAiOiJKV1QiLCJhbGciOi..." ^
  --include-props "id,number,displayName"
```

This command will generate POCOs for all entities, but **will not include** the `etag` or `lastDateTimeModified` properties.

```
dotnet-bc-scaffold ^
  --url "[https://api.businesscentral.dynamics.com](https://api.businesscentral.dynamics.com)" ^
  --company-id "a1b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d6" ^
  --token "eyJ0eXAiOiJKV1QiLCJhbGciOi..." ^
  --exclude-props "@odata.etag,lastDateTimeModified"
```
