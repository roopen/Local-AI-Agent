using System.Text.Json;
using LocalAIAgent.Application.News.Reader;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LocalAIAgent.Tests.UseCaseTests;

// Opt in with ARTICLE_READER_MCP_TEST_ENDPOINT=http://localhost:8931/mcp.
// Requires the pinned MCP/proxy containers, never a publisher login or real model.
public class ArticleReaderMcpTests
{
    private static string? Endpoint => Environment.GetEnvironmentVariable("ARTICLE_READER_MCP_TEST_ENDPOINT");
    public static bool Enabled => Endpoint is not null;

    private static Task<McpClient> ConnectAsync() => McpClient.CreateAsync(new HttpClientTransport(new()
    {
        Endpoint = new Uri(Endpoint!), TransportMode = HttpTransportMode.StreamableHttp,
    }), cancellationToken: TestContext.Current.CancellationToken);

    private static async Task<string> RunAsync(McpClient client, string code)
    {
        CallToolResult result = await client.CallToolAsync("browser_run_code_unsafe", new Dictionary<string, object?> { ["code"] = code },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.IsError != true, string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text)));
        return string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text));
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT to run the container smoke tests.")]
    public async Task PublisherPromotionsAreRemovedBeforeTranslationWithoutRemovingReporting()
    {
        McpClient client = await ConnectAsync();
        await using PlaywrightArticleBrowser browser = new(client);
        await RunAsync(client, """
            async page => {
              await page.route('https://example.com/article', r=>r.fulfill({contentType:'text/html',body:`
                <meta charset="utf-8"><article><h1>Financial news</h1><div class="article-body">
                <p>Article introduction.</p>
                <p>一手掌握經濟脈動 <a href="https://www.youtube.com/channel/UCdm3nYbbJ3gcbbOWhGDNqhw">點我訂閱自由財經Youtube頻道</a></p>
                <p class="appE1121">不用抽 不用搶 現在用APP看新聞 保證天天中獎
                  <a href="https://service.ltn.com.tw/app">點我下載APP</a>
                  <a href="https://drawpage.ltn.com.tw/slot_v9/">按我看活動辦法</a></p>
                <p>One-click subscribe to the financial channel <a href="https://www.youtube.com/channel/UCdm3nYbbJ3gcbbOWhGDNqhw"><b>Click me to subscribe to the independent financial Youtube channel</b></a></p>
                <p>No need to withdraw, no need to wait, use the current APP to see news and ensure you don't miss out every day
                  <a href="https://service.ltn.com.tw/app"><b>Click me to download the APP</b></a>
                  <a href="https://drawpage.ltn.com.tw/slot_v9/">Click me to see active draw methods</a></p>
                <p>The interview is available on <a href="https://www.youtube.com/channel/UCdm3nYbbJ3gcbbOWhGDNqhw">the financial channel</a>.</p>
                <p>The publisher changed <a href="https://service.ltn.com.tw/app">its app</a> and <a href="https://drawpage.ltn.com.tw/slot_v9/">draw rules</a>.</p>
                <p>The campaign says <a href="https://service.ltn.com.tw/app">Click me to download the APP</a>, according to the report.</p>
                <ul><li>Subscribe to government bonds through your broker.</li></ul>
                <p>Article conclusion.</p></div></article>`}));
              await page.goto('https://example.com/article');
            }
            """);
        ArticleContent content = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.Equal("complete", content.Status);
        Assert.StartsWith("Article introduction.", content.Markdown);
        Assert.EndsWith("Article conclusion.", content.Markdown);
        Assert.DoesNotContain("一手掌握", content.Markdown);
        Assert.DoesNotContain("不用抽", content.Markdown);
        Assert.DoesNotContain("One-click subscribe", content.Markdown);
        Assert.DoesNotContain("No need to withdraw", content.Markdown);
        Assert.Contains("The interview is available", content.Markdown);
        Assert.Contains("[its app](https://service.ltn.com.tw/app)", content.Markdown);
        Assert.Contains("[draw rules](https://drawpage.ltn.com.tw/slot_v9/)", content.Markdown);
        Assert.Contains("The campaign says", content.Markdown);
        Assert.Contains("Subscribe to government bonds", content.Markdown);
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT to run the container smoke tests.")]
    public async Task RequestedStoryWinsOverLongerStoriesAndAccountPromptsAreRemoved()
    {
        McpClient client = await ConnectAsync();
        await using PlaywrightArticleBrowser browser = new(client);
        await RunAsync(client, """
            async (page) => {
              await page.route('https://example.com/requested', r => r.fulfill({contentType:'text/html',body:`
                <meta charset="utf-8"><meta property="og:title" content="Requested story">
                <article data-url="/other"><h1>Other story</h1><p>${'Wrong story. '.repeat(500)}</p></article>
                <article data-url="/requested"><h1>Requested story</h1><p>Correct introduction.</p>
                  <div><h3>Få ut mer av DN som inloggad användare</h3>
                    <p>Du vet väl att du kan skapa ett gratis konto på DN?</p>
                    <ul><li>Följ dina intressen</li><li>Nyhetsbrev</li></ul><a href="/register">Skapa konto</a></div>
                  <div class="registration-prompt"><h3>Create an account</h3><p>Account promotion.</p></div>
                  <p>Reporting about registration and login remains part of the story.</p>
                  <article><h2>Nested recommendation</h2><p>Nested wrong story.</p></article>
                  <p>Correct conclusion with <a href="/source">a citation</a>.</p>
                </article>
                <article><h1>Long follow-up</h1><p>${'More wrong story. '.repeat(500)}</p></article>`}));
              await page.goto('https://example.com/requested');
            }
            """);
        ArticleContent content = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Requested story", content.Title);
        Assert.Equal("complete", content.Status);
        Assert.Contains("Correct introduction.", content.Markdown);
        Assert.Contains("Reporting about registration and login", content.Markdown);
        Assert.Contains("[a citation](https://example.com/source)", content.Markdown);
        Assert.DoesNotContain("wrong story", content.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("konto", content.Markdown);
        Assert.DoesNotContain("Nyhetsbrev", content.Markdown);
        Assert.DoesNotContain("Account promotion", content.Markdown);
        // Metadata title still identifies the story when publisher URL attributes are absent.
        await RunAsync(client, "async page => { await page.locator('article').evaluateAll(es=>es.forEach(e=>e.removeAttribute('data-url'))); }");
        Assert.Equal(content.Markdown, (await browser.ExtractAsync(TestContext.Current.CancellationToken)).Markdown);
        // A short, gated primary article must never be replaced with a longer accessible one.
        await RunAsync(client, """
            async page => { await page.locator('article').nth(1).evaluate(e=>e.innerHTML='<h1>Requested story</h1><p>Public teaser.</p><div class="paywall-wrapper"><p>Logga in för att läsa.</p></div>'); }
            """);
        ArticleContent gated = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Requested story", gated.Title);
        Assert.Equal("Public teaser.", gated.Markdown);
        Assert.True(gated.AccessRestricted);
        Assert.Equal("partial", gated.Status);
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT to run the container smoke tests.")]
    public async Task RealMcpExtractsRenderedArticleAndIsolatesSessions()
    {
        McpClient client = await ConnectAsync();
        await using PlaywrightArticleBrowser browser = new(client);
        // Test-only route fulfills a public URL locally; production extraction never uses run_code.
        await RunAsync(client, """
            async (page) => {
              await page.route('https://example.com/article', route => route.fulfill({contentType:'text/html',body:
                `<html lang="fi"><head><meta name="author" content="Test author"></head><body><nav>Navigation noise</nav><article><h1>Fixture article</h1><div id="content"></div></article><script>setTimeout(()=>document.getElementById("content").innerHTML='<h2>Heading</h2><p>First paragraph with <a href="/source">a source</a>.</p><p>Second paragraph.</p><aside>Related noise</aside>',100)</script></body></html>`}));
              await page.goto('https://example.com/article');
              await page.evaluate(() => localStorage.setItem('reader-session-test','private'));
            }
            """);
        await browser.WaitAsync(TestContext.Current.CancellationToken);
        ArticleContent content = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.Equal("complete", content.Status);
        Assert.Contains("## Heading", content.Markdown);
        Assert.Contains("[a source](https://example.com/source)", content.Markdown);
        Assert.Contains("Second paragraph", content.Markdown);
        Assert.DoesNotContain("noise", content.Markdown);
        Assert.Equal("Test author", content.Author);
        await RunAsync(client, """
            async (page) => {
              await page.evaluate(() => { document.querySelector('article').innerHTML='<h1>Long article</h1><p>'+'word '.repeat(6000)+'LAST PARAGRAPH MARKER</p>'; });
            }
            """);
        ArticleContent longArticle = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.True(longArticle.Markdown.Length > 30000);
        Assert.EndsWith("LAST PARAGRAPH MARKER", longArticle.Markdown);

        await using McpClient other = await ConnectAsync();
        string storage = await RunAsync(other, """
            async (page) => {
              await page.route('https://example.com/article', r => r.fulfill({body:'<article><p>Other session.</p></article>'}));
              await page.goto('https://example.com/article');
              return await page.evaluate(() => localStorage.getItem('reader-session-test'));
            }
            """);
        Assert.Contains("null", storage);
        Assert.DoesNotContain("private", storage);
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT to run the container smoke tests.")]
    public async Task SplitArticleBodyExcludesPublisherWidgetsAndLayoutWhitespace()
    {
        McpClient client = await ConnectAsync();
        await using PlaywrightArticleBrowser browser = new(client);
        await RunAsync(client, """
            async (page) => {
              await page.route('https://example.com/article', r => r.fulfill({contentType:'text/html',body:`
                <main><article><h1>Story</h1>
                <div class="post-content post-content-double"><p>First
                    paragraph with <a href="/source">
                       a source
                    </a>.</p><p>Listing image: <a href="/credit">Photographer</a></p></div>
                <aside><p>Advertisement noise.</p></aside>
                <div class="post-content post-content-double"><h2>Second section</h2><p>Last paragraph.</p>
                  <ol><li>First step</li><li>Second step</li></ol><pre>const x = 1;\n    console.log(x);</pre>
                </div><div class="author-bio"><p>Biography noise.</p></div></article>
                <section><header>Most Read</header><ol><li><a href="/other"><img alt="Related story image"></a>
                  <a href="/other">Related story noise.</a></li></ol><p>Outside noise.</p></section></main>`}));
              await page.goto('https://example.com/article');
            }
            """);
        ArticleContent content = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.Equal("complete", content.Status);
        Assert.StartsWith("First paragraph with [a source](https://example.com/source).", content.Markdown);
        Assert.Contains("## Second section\n\nLast paragraph.", content.Markdown);
        Assert.Contains("1. First step\n\n2. Second step", content.Markdown);
        Assert.Contains("```\nconst x = 1;\n    console.log(x);\n```", content.Markdown);
        Assert.DoesNotContain("noise", content.Markdown);
        Assert.DoesNotContain("Listing image", content.Markdown);
        Assert.DoesNotContain("Photographer", content.Markdown);
        Assert.DoesNotContain("/other", content.Markdown);
        Assert.Equal(2, content.Markdown.Split("```").Length - 1);

        // The semantic article boundary must also work without publisher-specific body classes.
        await RunAsync(client, """
            async (page) => {
              await page.locator('.post-content').evaluateAll(elements => elements.forEach(e => e.removeAttribute('class')));
            }
            """);
        ArticleContent fallback = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.Equal(content.Markdown, fallback.Markdown);
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT to run the container smoke tests.")]
    public async Task PublisherRelatedStoryListsAreRemovedWithoutRemovingArticleLinksOrLists()
    {
        McpClient client = await ConnectAsync();
        await using PlaywrightArticleBrowser browser = new(client);
        await RunAsync(client, """
            async (page) => {
              await page.route('https://example.com/article', r => r.fulfill({contentType:'text/html',body:`
                <article><h1>Upcoming phone</h1><div class="ue-l-article__body" data-section="articleBody">
                  <ul class="ue-c-article__subtitles" aria-label="Contenido relacionado">
                    <li><span>iPhone 18</span><a href="/related-sale">Related sale dates and prices</a></li>
                    <li><span>We test the iPhone Duo</span><a href="/related-review">Related phone review</a></li>
                  </ul>
                  <div class="ue-c-article__body" data-section="articleBody">
                    <p>The announcement includes <a href="/source">a source within the article</a>.</p>
                    <ul><li>First real feature</li><li>Second real feature</li></ul>
                    <ul class="ue-c-article__subtitles"><li><a href="/related-inline">Inline recommendation</a></li></ul>
                    <section aria-label="Contenido relacionado"><p><a href="/related-extra">Another recommendation</a></p></section>
                    <p>Final article paragraph.</p>
                  </div>
                </div></article>`}));
              await page.goto('https://example.com/article');
            }
            """);
        ArticleContent article = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.Equal("complete", article.Status);
        Assert.Contains("[a source within the article](https://example.com/source)", article.Markdown);
        Assert.Contains("- First real feature", article.Markdown);
        Assert.Contains("- Second real feature", article.Markdown);
        Assert.Contains("Final article paragraph.", article.Markdown);
        Assert.DoesNotContain("related-", article.Markdown);
        Assert.DoesNotContain("recommendation", article.Markdown);
        Assert.DoesNotContain("iPhone 18", article.Markdown);
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT to run the container smoke tests.")]
    public async Task GenericRecommendationDetectionPreservesProseCitationsAndOrdinaryLists()
    {
        McpClient client = await ConnectAsync();
        await using PlaywrightArticleBrowser browser = new(client);
        await RunAsync(client, """
            async (page) => {
              await page.route('https://example.com/article', r => r.fulfill({contentType:'text/html',body:`
                <meta charset="utf-8"><article><h1>Generic article</h1>
                  <p>Article introduction with <a href="/citation">an important citation</a>.</p>
                  <section data-component="RecommendedStories"><a href="/promo-one">A completely different recommended story</a></section>
                  <div data-testid="recirculation-card"><p><a href="/promo-two">Another interesting story elsewhere</a></p></div>
                  <div id="suggested-content"><a href="/promo-three">A suggested story about something else</a></div>
                  <h2>You might also like</h2><ul><li><a href="/promo-four">A further recommended story</a></li></ul>
                  <p>Read also: <a href="/promo-five">Another story you can read elsewhere</a></p>
                  <section><h3>Artículos relacionados</h3><p><a href="/promo-six">Una noticia relacionada bastante interesante</a></p></section>
                  <h3>Lue myös</h3><ul><li><a href="/promo-seven">Toinen kiinnostava uutinen aiheesta</a></li></ul>
                  <h2>Recommended settings</h2><p>These settings are part of the article.</p>
                  <ul><li>Actual feature one</li><li>Actual feature two</li></ul>
                  <h2>References</h2><ul><li><a href="/reference-one">Primary reference one</a></li><li><a href="/reference-two">Primary reference two</a></li></ul>
                  <div class="unrelated-analysis"><p>An unrelated observation that belongs to the story.</p></div>
                  <section class="related-work"><h2>Related work</h2><p>This paragraph discusses previous research in detail and belongs to the actual article. It explains how the authors arrived at their conclusions, compares the findings, and cites <a href="/research">research</a>.</p></section>
                  <h2>Read more</h2><p>This is ordinary explanatory prose under an ambiguous heading, and should stay because it is not a collection of links to other stories.</p>
                  <p>Article conclusion.</p>
                </article>`}));
              await page.goto('https://example.com/article');
            }
            """);
        ArticleContent article = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("/promo-", article.Markdown);
        Assert.DoesNotContain("You might also like", article.Markdown);
        Assert.DoesNotContain("Lue myös", article.Markdown);
        Assert.Contains("[an important citation](https://example.com/citation)", article.Markdown);
        Assert.Contains("## Recommended settings", article.Markdown);
        Assert.Contains("Actual feature one", article.Markdown);
        Assert.Contains("[Primary reference one](https://example.com/reference-one)", article.Markdown);
        Assert.Contains("An unrelated observation", article.Markdown);
        Assert.Contains("## Related work", article.Markdown);
        Assert.Contains("[research](https://example.com/research)", article.Markdown);
        Assert.Contains("## Read more", article.Markdown);
        Assert.Contains("Article conclusion.", article.Markdown);
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT to run the container smoke tests.")]
    public async Task RealBrowserCannotReachPrivateNetworkOrRedirectThere()
    {
        await using McpClient client = await ConnectAsync();
        string result = await RunAsync(client, """
            async (page) => {
              const failures=[];
              for (const url of ['http://127.0.0.1:8931/','http://169.254.169.254/','http://article-egress:3128/']) {
                try { const response=await page.goto(url,{timeout:8000}); failures.push(!response || response.status()>=400); }
                catch { failures.push(true); }
              }
              await page.route('https://example.com/redirect', r => r.fulfill({status:302,headers:{location:'http://169.254.169.254/'}}));
              try { const response=await page.goto('https://example.com/redirect',{timeout:8000}); failures.push(!response || response.status()>=400); }
              catch { failures.push(true); }
              return failures;
            }
            """);
        Assert.Contains("[true,true,true,true]", result);
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT to run the container smoke tests.")]
    public async Task PublicInternetWorksThroughEgressProxy()
    {
        McpClient client = await ConnectAsync();
        await using PlaywrightArticleBrowser browser = new(client);
        CallToolResult navigation = await client.CallToolAsync("browser_navigate", new Dictionary<string, object?> { ["url"] = "https://example.com/" },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(navigation.IsError != true, string.Join('\n', navigation.Content.OfType<TextContentBlock>().Select(b => b.Text)));
        ArticleContent content = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Example Domain", content.Title);
        Assert.NotEmpty(content.Markdown);
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT to run the container smoke tests.")]
    public async Task ObservedExpansionButtonRetrievesRemainingArticleText()
    {
        McpClient client = await ConnectAsync();
        await using PlaywrightArticleBrowser browser = new(client);
        await RunAsync(client, """
            async (page) => {
              await page.route('https://example.com/expand', r => r.fulfill({contentType:'text/html',body:'<article><h1>Story</h1><p>First part.</p><button onclick="this.outerHTML=\'<p>Remaining article text.</p>\'">Read more</button></article>'}));
              await page.goto('https://example.com/expand');
            }
            """);
        Assert.Equal("partial", (await browser.ExtractAsync(TestContext.Current.CancellationToken)).Status);
        string snapshot = await browser.SnapshotAsync(TestContext.Current.CancellationToken);
        string reference = System.Text.RegularExpressions.Regex.Match(snapshot, "button \\\"Read more\\\" \\[ref=(e[0-9]+)\\]").Groups[1].Value;
        Assert.NotEmpty(reference);
        await browser.ClickAsync(reference, "Read more", TestContext.Current.CancellationToken);
        ArticleContent result = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.Equal("complete", result.Status);
        Assert.Contains("Remaining article text", result.Markdown);
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT to run the container smoke tests.")]
    public async Task RealMcpReportsPartialPaywallAndOnlyAllowsObservedRecoveryButtons()
    {
        McpClient client = await ConnectAsync();
        await using PlaywrightArticleBrowser browser = new(client);
        await RunAsync(client, """
            async (page) => {
              await page.route('https://example.com/paywall', r => r.fulfill({body:'<article><h1>Story</h1><p>Public teaser.</p><p>Subscribe to continue</p></article><button>Buy subscription</button>'}));
              await page.goto('https://example.com/paywall');
            }
            """);
        ArticleContent content = await browser.ExtractAsync(TestContext.Current.CancellationToken);
        Assert.Equal("partial", content.Status);
        await browser.SnapshotAsync(TestContext.Current.CancellationToken);
        string click = await browser.ClickAsync("e999", "Buy subscription", TestContext.Current.CancellationToken);
        Assert.Contains("Only observed", click);
    }
}
