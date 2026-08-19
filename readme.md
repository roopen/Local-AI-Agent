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

## Private server

The web application supports an invite-only, same-origin Linux deployment. The
first account must be created from a configured trusted network and becomes the
owner. All later accounts require an owner-generated, seven-day, single-use
invitation. The owner manages the shared OpenAI-compatible endpoint while each
member's news preferences, feeds, feedback, evaluations, and passkeys remain
private.

See [docs/linux-server.md](docs/linux-server.md) for the rootless Podman Quadlet,
Cloudflare Tunnel and reverse-proxy ingress, backup, upgrade, restore, and
rollback runbook.

After installation, `deploy/update-ainews.sh` performs a fast-forward Git pull,
builds `localhost/ainews:latest` through the Quadlet build unit, reloads user
systemd, restarts the application Quadlet, and verifies its health endpoint.

## License

This project is licensed under the MIT License.
