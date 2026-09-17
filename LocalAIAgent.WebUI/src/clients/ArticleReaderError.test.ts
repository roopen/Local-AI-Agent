import { articleReaderErrorMessage, articleReaderFailure } from './ArticleReaderError';
import { ApiError } from './UserApiClient/core/ApiError';

function failure(code: string) {
    return new ApiError({ method: 'POST', url: '/api/News/ReadArticle' }, {
        url: '/api/News/ReadArticle', ok: false, status: 503, statusText: 'Service Unavailable',
        body: { code, detail: 'Internal diagnostic information must not be rendered.' },
    }, 'Service Unavailable');
}

test('explains that the browser service needs to be started', () => {
    expect(articleReaderErrorMessage(failure('article_browser_unreachable'))).toContain('Start the article browser services');
});

test('distinguishes unconfigured browser and model', () => {
    expect(articleReaderErrorMessage(failure('article_browser_not_configured'))).toContain('not configured');
    expect(articleReaderErrorMessage(failure('article_model_unavailable'))).toContain('Connect an AI model');
});

test('does not expose arbitrary server diagnostics', () => {
    expect(articleReaderErrorMessage(failure('unexpected'))).not.toContain('Internal diagnostic');
    expect(articleReaderErrorMessage(new Error('stack trace'))).not.toContain('stack trace');
});

test('diagnostic details retain request id, code, phase, and underlying failure', () => {
    const error = new ApiError({ method: 'POST', url: '/api/News/ReadArticle' }, {
        url: '/api/News/ReadArticle', ok: false, status: 503, statusText: 'Reader failed',
        body: { code: 'article_host_not_found', phase: 'opening', requestId: 'reader-123',
            diagnostic: 'Tool: browser_navigate\nnet::ERR_NAME_NOT_RESOLVED' },
    }, 'Reader failed');
    const result = articleReaderFailure(error, 'opening');
    expect(result.message).toContain('could not be resolved');
    expect(result.details).toContain('Request ID: reader-123');
    expect(result.details).toContain('Code: article_host_not_found');
    expect(result.details).toContain('Server phase: opening');
    expect(result.details).toContain('net::ERR_NAME_NOT_RESOLVED');
});

test.each([401, 403, 429, 502, 504])('explains HTTP %i failures', status => {
    const error = new ApiError({ method: 'POST', url: '/api/News/ReadArticle' }, {
        url: '/api/News/ReadArticle', ok: false, status, statusText: 'Failed', body: '<html>Gateway error</html>',
    }, 'Failed');
    expect(articleReaderErrorMessage(error)).not.toContain('The article could not be loaded');
    expect(articleReaderFailure(error, 'connecting').details).toContain(`HTTP status: ${status}`);
});
