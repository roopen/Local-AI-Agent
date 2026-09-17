# Full-article reader

The **Read in app** button opens a native modal dialog. The separate **Read the article at [source]** link opens the publisher in a new tab. The existing SignalR news stream stays mounted and keeps receiving articles. The reader uses a separate authenticated HTTP request, cancellation token, conversation, and captured user-selected model runtime. Closing/retrying/switching the reader never calls the news stream's stop method or model activation methods.

## Runtime and deployment

The modal requests `application/x-ndjson` from `POST /api/News/ReadArticle` to receive flushed progress events followed by one result or error. Ordinary JSON callers keep the existing response contract. Phases reflect actual work: waiting for a browser slot, opening the publisher, extracting content, checking language, and translating with completed/total batch counts. Cached results and same-language articles skip unnecessary work. Keep proxy buffering disabled for this response (`X-Accel-Buffering: no`); progress uses the reader's own cancellable HTTP request, never the news hub. The loading animation respects reduced-motion preferences.

The reader requires .NET 10 and the accompanying Playwright MCP service. The sidecar pins `@playwright/mcp` to **0.0.80** and its Playwright/browser dependency versions in `deploy/article-reader/package-lock.json`. The .NET client uses `ModelContextProtocol` **2.2.0**. No browser profile, publisher cookies, or account login is imported.

Build the two images from the repository root:

```sh
podman build -t localhost/ainews-article-egress:1 -f deploy/article-reader/Proxy.Containerfile .
podman build -t localhost/ainews-article-mcp:0.0.80 -f deploy/article-reader/Mcp.Containerfile .
```

Install `article-reader.network`, `article-egress.network`, `article-egress.container`, and `article-mcp.container` alongside the existing units from `deploy/quadlet`. Install the updated `ainews.container`, which joins the reader and DNS-enabled egress networks in addition to its normal network. The standard `deploy/update-ainews.sh` updater now builds and installs these services too. For manual installation, reload systemd and start the browser services before restarting AI News:

```sh
sudo cp deploy/quadlet/article-reader.network deploy/quadlet/article-egress.network deploy/quadlet/article-egress.container deploy/quadlet/article-mcp.container deploy/quadlet/ainews.container /etc/containers/systemd/
sudo systemctl daemon-reload
sudo systemctl start article-egress.service article-mcp.service
sudo systemctl restart ainews.service
```

Use the same Podman user/context as the existing server when building and installing images. The private API setting is `ArticleReader__McpEndpoint=http://10.203.0.3:8931/mcp`. Do not publish the MCP endpoint through the reverse proxy or Cloudflare tunnel. If the sidecar is unavailable, the feed still works and the modal offers retry and the source link.

### Static addresses and upgrading an existing server

The internal `article-reader-static` network uses `10.203.0.0/24`: gateway `.1`, proxy `.2`, MCP `.3`, and API `.4`. Automatic allocation uses `.128/25`, outside the service addresses. Both API-to-MCP and browser-to-proxy traffic use IP literals. Compose uses the same proxy and MCP addresses because it shares the browser image configuration. Before deployment, check `ip route` and `sudo podman network inspect --all` for conflicts with this subnet; if necessary, change the subnet and addresses together in the Quadlets, Compose, MCP configuration, and API environment.

Run the current repository updater after synchronizing these changes:

```sh
sudo bash deploy/update-ainews.sh "$PWD"
```

It rebuilds the browser image (which contains the proxy URL and MCP allowed-host list), migrates the old bundled MCP endpoint in `/etc/ainews/ainews.env`, and creates the new network before restarting the containers. This causes a brief outage during migration. The old `article-reader` network is left in place; no data volumes are removed. A reload alone does not reconfigure an existing Podman network, so the new network has a distinct name. For manual migration, stop all three services and restart `article-reader-network.service` after installing the definitions and rebuilding the browser image, then start the services again.

The updater preserves `NoNewPrivileges=false` when already set in either installed browser container definition, retaining the Ubuntu/crun AppArmor workaround. The source defaults remain hardened for hosts without that issue. Podman 4.9 installations should apply this setting directly in the installed `.container` files rather than relying on Quadlet drop-ins.

Verify connectivity from the API container:

```sh
sudo podman exec ainews curl --noproxy '*' --connect-timeout 5 --max-time 10 -sS -o /dev/null -w 'MCP HTTP %{http_code}\n' http://10.203.0.3:8931/mcp
```

An HTTP response confirms transport reachability; an MCP protocol error to this plain GET is expected and does not verify a complete MCP session. Use **Read in app** to verify the handshake, browser launch, and proxy together.

### Visual Studio / local development

Automatic container startup is **enabled for development** (`ArticleReader:AutoStartLocalServices=true`). F5 starts Podman if needed and prepares the reader containers.

Development provisioning applies to direct API debugging and Aspire AppHost launches. It checks Podman, starts its Windows VM if stopped, starts the two Compose services, and verifies an MCP handshake plus an isolated Chromium launch before API startup completes. Existing pinned images are reused; missing images are built on the first run. Production, integration tests, and Swagger generation do not run this setup.

Podman's original state is preserved: an already running VM stays running; a VM started by the debugger is stopped after the last participating debug session ends. A separate hidden cleanup watcher handles Visual Studio terminating the API without a graceful shutdown. Process IDs plus process start times identify sessions, and a shared file lock prevents shutdown during another debugger's startup. Cleanup can take a few seconds. Ownership state is kept under `%LOCALAPPDATA%/LocalAIAgent/reader-debug`; a cleanup failure is recorded in `cleanup-error.log` there. The watcher does not delete images, containers, or volumes. Manually started Podman sessions are not adopted for shutdown.

Install Podman with a working Compose provider once and initialize a VM with `podman machine init` if none exists. Restart Visual Studio after installing container tools so its PATH includes them. The first build may take several minutes; watch the API console/Debug Output for **Article reader ready** or the startup failure. A failure is logged with the command output while the rest of the app still starts.

Automatic startup manages only `http://localhost:8931/mcp` (or its loopback-IP equivalent); custom/remote MCP endpoints are left alone. Set `ArticleReader:AutoStartLocalServices=false` to manage services yourself, or set `ArticleReader:ContainerEngine=docker` when using an already running Docker Desktop engine. These settings can also be overridden with environment variables such as `ArticleReader__AutoStartLocalServices=false`.

For manual startup or an explicit rebuild after changing sidecar/proxy sources:

```sh
podman compose -p ainews-reader -f deploy/article-reader/compose.yaml up --build -d
```

`appsettings.Development.json` points the API at `http://localhost:8931/mcp`. To override the address, set `ArticleReader__McpEndpoint` in the API environment. Compose binds the MCP endpoint only to loopback and keeps the services running between article requests. When finished, use `podman compose -p ainews-reader -f deploy/article-reader/compose.yaml down`. Local development uses the same isolated network and proxy as production. Podman/Docker must be running before manually starting Compose. `.dockerignore` and `.containerignore` keep build output, databases, and `node_modules` out of the build context.

If the reader fails immediately, its modal now distinguishes a missing browser configuration, an unreachable browser service, and an unconfigured AI model. Check `podman compose -p ainews-reader -f deploy/article-reader/compose.yaml ps` for the local services. These errors are independent of whether a publisher requires login.

For a failed request, expand **Diagnostic details** in the modal or use **Copy error details**. This includes the failure code, last loading phase, HTTP status, server request ID, and underlying browser/connection error (including the MCP tool name when available). Match the request ID with the server's `Article reader failed` log entry. URL credentials, query strings, and common secret headers are redacted; page snapshots are omitted. DNS, TLS, proxy, navigation, extraction, timeout, and publisher HTTP failures have distinct explanations. Unknown failures retain diagnostic details instead of only displaying a generic message.

## Network isolation

The browser is connected **only** to an internal network, with no default internet route. Chromium sends HTTP(S), including loopback addresses, redirects and subresources, through `10.203.0.2:3128`. Service workers and non-proxied WebRTC UDP are disabled. The proxy connects only to public addresses on ports 80/443; it compiles the same `PublicNetworkHttpHandler` source used by feed fetching. DNS resolution and socket connection use the same validated IP, preventing DNS rebinding. IPv4-mapped IPv6, loopback, private, link-local and metadata ranges are rejected.

The proxy and API join both internal and internet networks; the browser never does. The proxy has no published port. A dedicated DNS-enabled egress network is necessary: combining an internal network with Podman's default DNS-disabled bridge can prevent public DNS resolution. Do not attach the browser to the internet network or remove its proxy configuration. MCP host checks and origin filters alone are not network security boundaries. The MCP endpoint exposes powerful trusted-client tools; the application gives the model only constrained wrappers and never passes model-generated JavaScript to MCP.

## Behavior and limits

- Two browser sessions at once; queued requests are cancellable. The 120-second extraction deadline includes queue time. Recovery uses at most 12 tool calls.
- A fixed DOM extractor preserves headings, paragraphs, lists, quotes, and links, filtering navigation, comments and related content. It reads visible public text, not an LLM reconstruction. Generic layout fallback, remaining expansion controls, access barriers and the 500,000-character limit produce explicit partial status.
- Article selection prefers matching publisher URLs and page-title metadata, then the first primary heading in page order. Longer appended stories cannot win simply by containing more text. Nested stories, account prompts and paywall offers are excluded; a gated primary article retains its public teaser with partial status.
- Related-story filtering combines publisher-independent component markers or recommendation headings with compact, link-heavy structure. Multilingual headings and inline recommendation callouts are supported; link density alone does not remove ordinary citations, reference lists or prose sections.
- Embedded LTN channel-subscription, app-download and prize-draw promotions are removed before translation by matching promotional wording and link destinations together. Ordinary reporting and citations to the same destinations are preserved.
- Access barriers stop recovery. Unknown layouts may yield partial/unavailable content; no extractor can prove publisher completeness across every site. Publisher login, CAPTCHA solving, pagination across separate URLs, images and PDF reading are outside v1.
- Detection examines actual article text. Matching languages skip translation. Translation uses paragraph-aware batches up to 6,000 characters, splitting oversized paragraphs at 4,000 characters. All block IDs must be present and unique; incomplete/invalid/truncated responses return the original without a partial translation. Translation has a ten-minute deadline.
- Models without usable tool support retain the initial deterministic extraction. Both reader and feed use the selected model; limited model capacity can increase latency. They do not cancel or intentionally pause each other.
- Successful complete results (original and translated content) are cached for 30 minutes, partitioned by user, URL, target language, model and endpoint. A dedicated SQLite cache survives API/debug restarts, with a 16 MiB memory cache for fast repeat reads. The disk cache is bounded to 16 MiB/64 articles per user and 64 MiB/256 articles overall; oldest entries are evicted and expired entries are pruned on writes. Expiry is absolute and does not reset when reopening an article. Partial/failed results are not cached. Reader content is never sent to dataset collection. The database defaults to `article-reader-cache.db` beside the application database (on `/data` in production); override it with `ArticleReader:CachePath`. Cache failures fall back to normal retrieval without failing the reader.
- Reader logs contain timings, status, recovery counts and language/translation token counts. They do not log article bodies, prompts, URLs or credentials. Browser transient files reside in container tmpfs.

## API

`POST /api/News/ReadArticle` accepts `{ "url": "https://publisher/article", "sourceLanguage": "fi" }`; `sourceLanguage` is optional. Identity, target language and model selection are resolved server-side. The response contains `original` (Markdown, title, URL, language/author/date metadata, access flag and extraction status), optional translated title/Markdown, detected and target language, translation status and an optional user-facing message.

Statuses: extraction `complete`, `partial`, `blocked`, `unavailable`; translation `notNeeded`, `complete`, `failed`, `unavailable`. Invalid URLs return 400, unauthenticated callers 401, missing preferences 404, extraction timeouts 504 and unavailable reader services 503. Reader failure does not change global feed state.

## Verification

```sh
dotnet test LocalAIAgent.Tests -p:SkipClientBuild=true
npm --prefix LocalAIAgent.WebUI test -- --runInBand
npm --prefix LocalAIAgent.WebUI run lint
npm --prefix LocalAIAgent.WebUI run build
```

To include real pinned-MCP tests, first start the local sidecars and set `ARTICLE_READER_MCP_TEST_ENDPOINT=http://localhost:8931/mcp`, then run `dotnet test LocalAIAgent.Tests -p:SkipClientBuild=true --filter FullyQualifiedName~ArticleReader`. On PowerShell use `$env:ARTICLE_READER_MCP_TEST_ENDPOINT='http://localhost:8931/mcp'`.

The smoke tests use test-only Playwright routes for rendered article fixtures and a public example.com request for proxy connectivity. They cover extraction, delayed rendering, session separation, recovery buttons, paywall labeling, and blocked private-network navigation/redirects. Frontend regressions verify feed continuity, stale responses, close cancellation, focus restoration, Escape, safe Markdown and translated article display without an original-text toggle.

For upgrades, change the MCP version and lockfile together, rebuild the image, and rerun these tests: tool names and parameter shapes can change between MCP versions. To roll back the reader, restore the previous API/UI image and remove its reader-network/configuration additions before stopping the sidecars. No database migration is required.
