using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LocalAIAgent.Application.News.Reader;

internal sealed class PlaywrightArticleBrowserFactory(IConfiguration configuration,
    ILogger<PlaywrightArticleBrowserFactory> logger) : IArticleBrowserFactory
{
    public async Task<IArticleBrowser> OpenAsync(CancellationToken cancellationToken)
    {
        string? endpoint = configuration["ArticleReader:McpEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArticleReaderUnavailableException("article_browser_not_configured", "The article browser service is not configured on this server.");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? address) || address.Scheme is not ("http" or "https"))
            throw new ArticleReaderUnavailableException("article_browser_not_configured", "The article browser service configuration is invalid.");
        try
        {
            McpClient client = await McpClient.CreateAsync(new HttpClientTransport(new()
            {
                Endpoint = address,
                Name = "article-reader",
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = TimeSpan.FromSeconds(10),
            }), cancellationToken: cancellationToken);
            return new PlaywrightArticleBrowser(client);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning("Article browser connection failed ({FailureType}). Check the MCP service and ArticleReader:McpEndpoint.", ex.GetType().Name);
            throw new ArticleReaderUnavailableException("article_browser_unreachable", "The article browser service is not running or could not be reached. Start the article browser services and retry.",
                $"MCP endpoint: {address}\n{ex.GetType().Name}: {ex.Message}\n{ex.InnerException?.Message}");
        }
    }
}

internal sealed class PlaywrightArticleBrowser(McpClient client) : IArticleBrowser
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private string snapshot = "";

    private async Task<string> CallAsync(string tool, Dictionary<string, object?> args, CancellationToken ct)
    {
        CallToolResult result;
        try { result = await client.CallToolAsync(tool, args, cancellationToken: ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw ArticleBrowserFailure.FromToolError(tool, $"{ex.GetType().Name}: {ex.Message}\n{ex.InnerException?.Message}"); }
        string text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        if (result.IsError == true) throw ArticleBrowserFailure.FromToolError(tool, text);
        return text;
    }

    public async Task NavigateAsync(string url, CancellationToken cancellationToken)
    {
        if (!ArticleUrlPolicy.IsValid(url)) throw new ArgumentException("A public HTTP(S) article URL is required.");
        await CallAsync("browser_navigate", new() { ["url"] = url }, cancellationToken);
        await WaitAsync(cancellationToken);
    }

    public async Task<ArticleContent> ExtractAsync(CancellationToken cancellationToken)
    {
        string text = await CallAsync("browser_evaluate", new() { ["function"] = ExtractionScript }, cancellationToken);
        // Playwright wraps evaluate output in a '### Result' section followed by diagnostics.
        int start = text.IndexOf('{');
        if (start < 0) throw new ArticleReaderUnavailableException("article_extraction_failed", "The article browser returned no readable content.");
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(text[start..]));
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.TryGetProperty("httpStatus", out JsonElement status) && status.GetInt32() >= 400)
        {
            int code = status.GetInt32();
            throw new ArticleReaderUnavailableException("article_publisher_http_error", "The publisher returned an HTTP error instead of the article.",
                $"Publisher HTTP status: {code}\nTool: browser_evaluate (article extraction)");
        }
        ArticleContent content = document.RootElement.Deserialize<ArticleContent>(JsonOptions)
            ?? throw new InvalidOperationException("The article browser returned invalid content.");
        if (!ArticleUrlPolicy.IsValid(content.SourceUrl))
            throw new ArticleReaderUnavailableException("article_redirect_blocked", "The article redirected to an address the reader cannot open. Use the publisher's site.");
        return content;
    }

    public async Task<string> SnapshotAsync(CancellationToken cancellationToken)
    {
        snapshot = await CallAsync("browser_snapshot", new(), cancellationToken);
        snapshot = snapshot[..Math.Min(snapshot.Length, 16000)];
        return snapshot;
    }

    public Task<string> ClickAsync(string reference, string label, CancellationToken cancellationToken)
    {
        // Only observed buttons for optional consent dismissal or article expansion are available.
        string? line = snapshot.Split('\n').FirstOrDefault(line => line.Contains($"[ref={reference}]", StringComparison.Ordinal));
        if (!System.Text.RegularExpressions.Regex.IsMatch(reference, "^e[0-9]+$")
            || line is null || !line.Contains("button", StringComparison.OrdinalIgnoreCase)
            || !System.Text.RegularExpressions.Regex.IsMatch(line,
                "reject|decline|necessary only|continue without|read more|show more|continue reading|expand|hylkää|välttämättömät|lue lisää|avvisa|visa mer|ablehnen|weiterlesen|rechazar|leer más",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return Task.FromResult("Only observed optional-consent dismissal or article-expansion buttons may be clicked.");
        return CallAsync("browser_click", new() { ["target"] = reference }, cancellationToken);
    }

    public async Task WaitAsync(CancellationToken cancellationToken) =>
        await CallAsync("browser_wait_for", new() { ["time"] = 2 }, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try
        {
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(5));
            await CallAsync("browser_close", new(), cleanup.Token);
        }
        catch (Exception) { /* Session deletion below still runs after browser failure. */ }
        finally { await client.DisposeAsync(); }
    }

    internal const string ExtractionScript = """
        () => {
          const httpStatus = performance.getEntriesByType('navigation')[0]?.responseStatus || 0;
          if (httpStatus >= 400) return {httpStatus};
          const visible = e => !!(e.getClientRects().length) && getComputedStyle(e).visibility !== 'hidden';
          const meta = name => document.querySelector(`meta[name="${name}"],meta[property="${name}"]`)?.content?.slice(0,1024) || null;
          const barrierText = /verify you are human|complete the captcha|access denied|sign in to continue|subscribe to continue|subscriber.only|purchase a subscription/i;
          let blocked = barrierText.test((document.body?.innerText || '').slice(0, 100000));
          // Prefer an article over its enclosing main: main also contains ranked stories and widgets.
          let candidates = [...document.querySelectorAll('article')].filter(visible);
          if(!candidates.length) candidates = [...document.querySelectorAll('[itemprop="articleBody"],main,[role="main"]')].filter(visible);
          if(!candidates.length) candidates.push(...[...document.querySelectorAll('p')].filter(visible).map(p=>p.parentElement).filter(e=>e && e!==document.body));
          // Infinite-scroll pages can contain several complete stories. Length is not identity.
          const normalizeTitle = text => (text || '').toLowerCase().replace(/\s+/g,' ').trim();
          const urlKey = value => { try { const u=new URL(value,location.href); return u.origin+u.pathname.replace(/\/$/,''); } catch { return null; } };
          const expectedUrls = new Set([location.href, document.querySelector('link[rel="canonical"]')?.href, meta('og:url')].filter(Boolean).map(urlKey));
          const ownHeading = e => [...e.querySelectorAll('h1')].find(h=>visible(h) && (!e.matches('article') || h.closest('article')===e));
          const identityUrls = e => [e.getAttribute('data-url'),e.getAttribute('itemid'),...[...e.querySelectorAll('a[rel="bookmark"],a[itemprop="url"],h1 a[href]')].filter(a=>!e.matches('article') || a.closest('article')===e)].map(v=>typeof v==='string'?v:v?.getAttribute('href')).filter(Boolean);
          const matchesUrl = e => identityUrls(e).some(url=>expectedUrls.has(urlKey(url)));
          const eligible = candidates.filter(e=>!identityUrls(e).length || matchesUrl(e));
          const expectedTitle = normalizeTitle(meta('og:title'));
          const root = eligible.find(matchesUrl)
            || eligible.find(e=>expectedTitle && normalizeTitle(ownHeading(e)?.innerText)===expectedTitle)
            || eligible.find(e=>ownHeading(e)) || eligible[0];
          const title = (root?.querySelector('h1')?.innerText || document.querySelector('h1')?.innerText || meta('og:title') || document.title).slice(0,1024);
          if (!root) return {title, markdown:'', sourceUrl:location.href, accessRestricted:blocked, status:blocked?'blocked':'unavailable'};
          const paywallSelector = '[class*="paywall"],[id*="paywall"],[data-paywall]';
          blocked = barrierText.test((root.innerText || '').slice(0,100000)) || [...root.querySelectorAll(paywallSelector)].some(visible);
          const skip = 'nav,aside,footer,header,script,style,noscript,form,button,figcaption,[hidden],[aria-hidden="true"],[role="navigation"],[role="complementary"],[class*="comment"],[class*="advert"],[class*="social"],[class*="share"],[class*="newsletter"],[class*="most-read"],[class*="most-popular"],[class*="author-bio"],[class*="caption-credit"],.ue-c-article__subtitles';
          // Publishers can split the body around adverts; keep ALL body sections in document order.
          const bodySelector = '[itemprop="articleBody"],.post-content,.article-content,.article-body,.entry-content,.ue-c-article__body';
          const bodies = root.matches(bodySelector) ? [root] : [...root.querySelectorAll(bodySelector)].filter(e=>visible(e) && !e.closest(skip));
          const roots = bodies.length ? bodies.filter(e=>!bodies.some(other=>other!==e && other.contains(e))) : [root];
          // Publisher-independent recommendation detection. Combine semantics with link-heavy
          // structure; link density alone would remove legitimate references and article lists.
          const normalize = text => text.replace(/([a-z])([A-Z])/g,'$1 $2').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[_-]+/g,' ').replace(/\s+/g,' ').trim();
          const label = /^(?:(?:related|recommended|suggested)(?: articles?|stories|content|posts?|news|links|reading|for you)?|recommendations|read (?:next|also|more)|also read|you (?:may|might) also like|more (?:on this|stories|articles)|most (?:read|popular)|(?:contenido|articulos|noticias) relacionad[oa]s?|relacionados|lea tambien|lee tambien|tambien te puede interesar|lue myos|lue seuraavaksi|aiheeseen liittyvaa|las ocksa|las mer|a lire aussi|lire aussi|sur le meme sujet|auch interessant|lesen sie auch|leggi anche)(?:\s*[:：–—-])?$/;
          const marked = /(?:^|\s)(?:related(?:\s+(?:articles?|stories|content|posts?|news|links))?|recommendations?|recommended|suggested content|recirculation|recirc|read next|read also|also read|more stories|most read|most popular|relacionados|relacionad[oa]s?|outbrain|taboola)(?:\s|$)/;
          const rejected = new Set();
          // LTN inserts these promotional paragraphs directly into the article body.
          // Require both a promotional lead and CTA destinations; ordinary reporting linking
          // to YouTube, an app, or a prize draw must remain readable.
          const promotionLead = /^(?:一手掌握經濟脈動|一鍵訂閱|不用抽\s*不用搶|one[ -]click subscribe|no need to withdraw|click (?:me |here )?to (?:subscribe|download)|[點按]我(?:訂閱|下載|看活動))/i;
          const promotionalLink = a => {
            const text = normalize(a.innerText || '');
            if (!/(?:[點按]我(?:訂閱|下載|看活動)|click (?:me |here )?to (?:subscribe|download|see.*(?:draw|promotion)))/i.test(text)) return false;
            try {
              const u = new URL(a.href);
              return (['youtube.com','www.youtube.com'].includes(u.hostname) && /^\/(?:channel\/|@)/.test(u.pathname))
                || (u.hostname==='service.ltn.com.tw' && /^\/app\/?$/.test(u.pathname))
                || (u.hostname==='drawpage.ltn.com.tw' && /^\/slot_v\d+\//.test(u.pathname));
            } catch { return false; }
          };
          for (const e of root.querySelectorAll('p,li')) {
            const text = (e.innerText || '').replace(/\s+/g,' ').trim();
            const links = [...e.querySelectorAll('a[href]')].filter(visible);
            if (text.length <= 700 && promotionLead.test(text) && links.length && links.every(promotionalLink)) rejected.add(e);
          }
          // Nested stories and account-acquisition widgets are not part of this article.
          for (const e of root.querySelectorAll('article,'+paywallSelector)) rejected.add(e);
          const accountHeading = /^(?:fa ut mer av dn som inloggad(?: anvandare)?|get more out of dn as a logged in user)$/;
          const accountMarker = /(?:^|\s)(?:registration|register|login|sign up|signup|account)(?:\s+(?:prompt|puff|promo|wall|widget|banner|benefits))(?:\s|$)/;
          for (const e of root.querySelectorAll('section,div,aside')) {
            if (roots.some(body=>e.contains(body))) continue;
            const markers = normalize([e.className,e.id,e.getAttribute('data-component')].join(' '));
            const heading = e.querySelector('h2,h3,h4');
            if (accountMarker.test(markers) || (heading?.parentElement===e && accountHeading.test(normalize(heading.innerText)) && (e.innerText || '').length < 2000)) rejected.add(e);
          }
          const linkGroup = element => {
            const text = (element.innerText || '').replace(/\s+/g,' ').trim();
            if (!text || text.length > 4000) return false;
            const links = [...element.querySelectorAll('a[href]')].filter(a=>visible(a) && a.innerText.trim() && /^https?:/.test(a.href));
            if (!links.length || links.length > 20) return false;
            const linked = links.reduce((length,a)=>length+a.innerText.replace(/\s+/g,' ').trim().length,0);
            const paragraphs = element.matches('p') ? [element] : [...element.querySelectorAll('p')];
            const hasProse = paragraphs.some(p=> {
              const length=p.innerText.trim().length;
              const linkLength=[...p.querySelectorAll('a')].reduce((n,a)=>n+a.innerText.trim().length,0);
              return length-linkLength > 100;
            });
            return !hasProse && linked/text.length >= .5 && text.length-linked <= 300;
          };
          for (const element of root.querySelectorAll('section,div,ul,ol,p')) {
            if (roots.some(body=>element.contains(body))) continue;
            const markers = [...element.attributes].filter(a=>['class','id','aria-label','role'].includes(a.name)||a.name.startsWith('data-')).map(a=>normalize(a.value)).join(' ');
            if (marked.test(markers) && linkGroup(element)) rejected.add(element);
            // Inline callouts, e.g. "Read also: <a>Another story</a>".
            const prefix = (element.innerText || '').split(/[:：]/,1)[0];
            if (element.tagName==='P' && element.innerText.includes(':') && label.test(normalize(prefix)) && linkGroup(element)) rejected.add(element);
          }
          for (const heading of root.querySelectorAll('h2,h3,h4,h5,h6,strong,b,[role="heading"]')) {
            if (!label.test(normalize(heading.innerText || ''))) continue;
            // Prefer the heading's own widget, but never discard an entire article/body container.
            const parent = heading.parentElement;
            if (parent && parent!==root && !roots.some(body=>parent.contains(body)) && parent.matches('section,div,aside') && linkGroup(parent)) {
              rejected.add(parent);
              continue;
            }
            const block = heading.closest('h2,h3,h4,h5,h6,p,[role="heading"]') || heading;
            let next = block.nextElementSibling;
            let count = 0;
            while (next && count++ < 6 && linkGroup(next)) {
              rejected.add(block);
              rejected.add(next);
              if (next.matches('ul,ol,[role="list"]')) break;
              next = next.nextElementSibling;
            }
          }
          const excluded = element => {
            if (element.closest(skip)) return true;
            for(let current=element;current && current!==root;current=current.parentElement) if(rejected.has(current)) return true;
            return false;
          };
          const escape = t => t.replace(/([\\`*_[\]<>])/g,'\\$1');
          const inline = e => [...e.childNodes].map(n => {
            if(n.nodeType===3) return escape(n.textContent.replace(/\s+/g,' '));
            if(n.nodeType!==1 || excluded(n)) return '';
            if(n.tagName==='BR') return '\n';
            const text=inline(n);
            if(n.tagName==='A') {if(!text.trim()) return ''; try {const u=new URL(n.getAttribute('href'),location.href); if(['https:','http:'].includes(u.protocol)) return `[${text.trim()}](${u.href.replace(/\)/g,'%29')})`;}catch{}}
            return text;
          }).join('');
          const parts=[];
          for(const e of roots.flatMap(body=>[...body.querySelectorAll('h2,h3,h4,h5,h6,p,li,blockquote,pre')])) {
            if(!visible(e) || excluded(e) || e.parentElement?.closest('li,blockquote,pre')) continue;
            const text=inline(e).trim(); if(!text) continue;
            if (/^(listing image|image credit|photo credit)\s*:/i.test(e.textContent.trim())) continue;
            const tag=e.tagName;
            const marker = e.parentElement?.tagName==='OL' ? (Number(e.parentElement.getAttribute('start')||1)+[...e.parentElement.children].filter(n=>n.tagName==='LI').indexOf(e))+'. ' : '- ';
            const code = e.textContent.replace(/\r\n/g,'\n').trimEnd();
            const fence = '`'.repeat(Math.max(3, ...[...code.matchAll(/`+/g)].map(m=>m[0].length+1)));
            parts.push(/^H[2-6]$/.test(tag)?'#'.repeat(Number(tag[1]))+' '+text:tag==='LI'?marker+text:tag==='BLOCKQUOTE'?'> '+text.replace(/\n/g,'\n> '):tag==='PRE'?fence+'\n'+code+'\n'+fence:text);
          }
          const full=parts.join('\n\n');
          const truncated=full.length>500000;
          const expandable=[...root.querySelectorAll('button')].some(e=>visible(e)&&/read more|show more|continue reading|expand|lue lisää|visa mer|weiterlesen|leer más/i.test(e.innerText));
          const explicitBody=root.matches('article,[itemprop="articleBody"]') || !!root.querySelector('article,[itemprop="articleBody"]');
          return {title,markdown:full.slice(0,500000),sourceUrl:location.href,
            language:(root.lang || document.documentElement.lang || meta('og:locale') || '').slice(0,35),accessRestricted:blocked,
            author:meta('author'),publishedAt:meta('article:published_time'),
            status:blocked?(full?'partial':'blocked'):!full?'unavailable':truncated||expandable||!explicitBody?'partial':'complete'};
        }
        """;
}
