# Local AI Agent - An Intelligent News Curator

This project provides a .NET 10 based AI agent service for fetching and summarizing the latest news based on user preferences.

NOTE: Currently very much a Proof of Concept.

## Features

- Fetches current news summaries from various freely available RSS feeds from major news outlets
- LLM access via `Microsoft.Extensions.AI` over an OpenAI-compatible endpoint (LM Studio by default)
- Modern C# 13.0 syntax and .NET 10 support
- Per-article relevancy filtering and translation, both driven by structured-output prompts

## Requirements

- Windows or Linux OS
- [LM Studio](https://lmstudio.ai/) running with an OpenAI-compliant local API
- Internet access (application will not work if blocked by Firewall)
- NodeJS 20

## Installation

- Download newest version
- Unzip the archive
- Create UserPrompt.txt file in the root directory with your custom prompt
- Ensure LMStudio is running (ideally with `gemma-3-27b-it-qat`) — you can customize `appsettings.json` to use different models
- Run the application

## License

This project is licensed under the MIT License.
