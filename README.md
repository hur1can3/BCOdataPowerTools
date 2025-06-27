# BCOdataPowerTools

Welcome to BCOdataPowerTools, a suite of .NET libraries designed to make interacting with the Microsoft Dynamics 365 Business Central OData API simple, modern, and efficient.

This repository contains two main projects:

1. **BusinessCentral.OData.Client**: A modern, resilient, and extensible .NET client library for querying the Business Central OData API using a fluent, strongly-typed interface.

2. **BusinessCentral.OData.Scaffold**: A .NET Global Tool for automatically generating C# POCO classes from your Business Central OData metadata, ready to be used with the client library.

## Core Philosophy

- **Modern .NET**: Built for .NET 8 and .NET Standard 2.0, embracing dependency injection, `IHttpClientFactory`, and structured logging.

- **Resilient by Default**: Automatically handles transient network faults with built-in retry and circuit-breaker policies via Polly.

- **Developer-First Experience**: Focused on providing a clean, intuitive, and extensible API to accelerate development.

## 1. BC.OData.Client Library

A production-ready client library for any .NET application connecting to Business Central.

### Key Features

- **Fluent Query Builder**: Construct complex OData queries (`$filter`, `$select`, `$expand`, `$orderby`, `$apply`) in a strongly-typed, refactor-safe way.

- **Automatic Pagination**: A simple `GetAllPagesAsync` method transparently handles server-driven pagination via `@odata.nextLink`.

- **Efficient Batching**: Supports `application/json` `$batch` requests to execute multiple operations in a single network call.

- **Extensible**: Easily add custom authentication handlers or override client behavior.

[**➡️ Go to the Client Library README for full documentation and examples.**](https://gemini.google.com/app/src/BusinessCentral.OData.Client/README.md "null")

## 2. BC.OData.Scaffold Tool

A command-line companion tool that automates the creation of C# POCO classes, saving you hours of manual work.

### Key Features

- **Automatic Code Generation**: Connects to your BC environment and generates C# classes from the OData `$metadata`.

- **Attribute Generation**: Automatically adds `[JsonPropertyName]` attributes to handle OData field naming conventions.

- **Property Filtering**: Use `--include-props` and `--exclude-props` to generate lean POCOs with only the fields you need.

- **.NET Global Tool**: Easy to install and run from any command line.

[**➡️ Go to the Scaffolding Tool README for installation and usage instructions.**](https://gemini.google.com/app/src/BusinessCentral.OData.Scaffold/README.md "null")

## License

This project is licensed under the [MIT License](https://gemini.google.com/app/LICENSE "null").
