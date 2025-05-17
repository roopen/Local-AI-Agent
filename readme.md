# Local AI Agent - An Intelligent News Curator

This project provides a .NET 9-based AI agent service for fetching and summarizing the latest news based on user preferences. It leverages modern C# features and integrates with Microsoft Semantic Kernel for AI-powered operations.

## Features

- Fetches current news summaries from YLE and Fox News RSS feeds
- Designed for integration with AI agents and Semantic Kernel
- Modern C# 13.0 syntax and .NET 9 support

## Getting Started

1. **Clone the repository**
2. **Build the project**

   Open the solution in Visual Studio and build the project.

3. **Configure News Source Settings**

   Ensure `YleNewsSettings` and other news source settings are configured with the correct RSS feed URLs and client names.

4. **Run LM Studio**

   Start [LM Studio](https://lmstudio.ai/) and ensure it is running with an OpenAI-compliant local API endpoint. The service requires this for AI-powered summarization.

5. **Run the Service**

   Use the `NewsService` class to fetch news summaries from all configured sources:

## Requirements

- .NET 9 SDK
- Visual Studio 2022 or later
- [LM Studio](https://lmstudio.ai/) running with an OpenAI-compliant local API (currently hardcoded to use gemma-3-27b-it-abliterated model)
- Internet access for fetching RSS feeds

## License

This project is licensed under the MIT License.
