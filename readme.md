# Local AI Agent - An Intelligent News Curator

This project provides a .NET 9-based AI agent service for fetching and summarizing the latest news based on user preferences.

## Features

- Fetches current news summaries from various freely available RSS feeds from major news outlets
- AI Agent is powered by Microsoft Semantic Kernel
- Modern C# 13.0 syntax and .NET 9 support
- Leverages RAG (Retrieval-Augmented Generation) for enhanced information retrieval. RAG is queried with vector embeddings to provide contextually relevant summaries.

## Workflow

+-------------------+
| NewsClientSettings|
| (e.g. JapanTimes) |
+-------------------+
          |
          v
+-------------------+
|  AddHttpClient    |
|  (Configure       |
|   HttpClient)     |
+-------------------+
          |
          v
+-------------------+
|  NewsService      |
|  - LoadAllNews()  |
|  - GetNews()      |
+-------------------+
          |
          v
+-------------------+
|  Fetch RSS/Atom   |
|  Feed via HTTP    |
+-------------------+
          |
          v
+-------------------+
|  SyndicationFeed  |
|  (Parse XML Feed) |
+-------------------+
          |
          v
+-------------------+
|  Filter news by   |
|  keyword          |
+-------------------+
          |
          v
+-------------------+
|  Filter news by   |
|  vectors          |
+-------------------+
          |
          v
+-------------------+
|  Save to Vector   |
|  Database         |
+-------------------+
          |
          v
+-------------------+
|  AI Chat Service  |
|  (Loads NewsItems |
|   from Vector DB) |
+-------------------+

## Requirements

- Windows or Linux OS
- [LM Studio](https://lmstudio.ai/) running with an OpenAI-compliant local API (currently hardcoded to use gemma-3-27b-it-abliterated model)
- Internet access (application will not work if blocked by Firewall)

## Installation

- Download newest version
- Unzip the archive
- Create UserPrompt.txt file in the root directory with your custom prompt
- Ensure LMStudio is running (with gemma-3-27b-it-abliterated model and text-embedding-granite-embedding-278m-multilingual) [You can customize appsettings.json to use different models]
- Run the application

## License

This project is licensed under the MIT License.
