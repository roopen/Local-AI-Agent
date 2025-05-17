# Local AI Agent - News Service

This project provides a .NET 9-based service for fetching and summarizing the latest news from Finnish YLE using RSS feeds. It leverages modern C# features and integrates with Microsoft Semantic Kernel for AI-powered operations.

## Features

- Fetches current news summaries from YLE RSS feeds
- Uses `IHttpClientFactory` for efficient HTTP requests
- Parses RSS feeds with `SyndicationFeed`
- Designed for integration with AI agents and Semantic Kernel
- Modern C# 13.0 syntax and .NET 9 support

## Getting Started

1. **Clone the repository**
2. **Build the project**

   Open the solution in Visual Studio and build the project.

3. **Configure YLE News Settings**

   Ensure `YleNewsSettings` is configured with the correct RSS feed URLs and client name.

4. **Run the Service**

   Use the `NewsService` class to fetch news summaries:
## Project Structure

- `News/NewsService.cs` - Main service for fetching and parsing YLE news feeds
- `YleNewsSettings` - Static class for managing YLE RSS feed URLs (not shown here)

## Requirements

- .NET 9 SDK
- Visual Studio 2022 or later
- Internet access for fetching RSS feeds

## License

This project is licensed under the MIT License.
