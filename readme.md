# Local AI Agent - An Intelligent News Curator

This project provides a .NET 9-based AI agent service for fetching and summarizing the latest news based on user preferences.

## Features

- Fetches current news summaries from various freely available RSS feeds from major news outlets
- AI Agent is powered by Microsoft Semantic Kernel
- Modern C# 13.0 syntax and .NET 9 support
- Leverages RAG (Retrieval-Augmented Generation) for enhanced information retrieval. RAG is queried with vector embeddings to provide contextually relevant summaries.

## Requirements

- Windows or Linux OS
- [LM Studio](https://lmstudio.ai/) running with an OpenAI-compliant local API (currently hardcoded to use gemma-3-27b-it-abliterated model)
- Internet access (application will not work if blocked by Firewall)

## License

This project is licensed under the MIT License.
