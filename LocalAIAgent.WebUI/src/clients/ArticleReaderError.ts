import { ApiError } from './UserApiClient/core/ApiError';
import axios from 'axios';

const setupMessages: Record<string, string> = {
    article_browser_not_configured: 'The article browser service is not configured on this server.',
    article_browser_unreachable: 'The article browser service is not running or could not be reached. Start the article browser services and retry.',
    article_model_unavailable: 'Connect an AI model in settings before using the article reader.',
    article_invalid_url: 'This article link is not supported. Use a public HTTP or HTTPS link.',
    article_preferences_missing: 'Save your news preferences in settings before opening an article.',
    article_page_timeout: 'The publisher’s page took too long to load. Retry or open the publisher’s site.',
    article_host_not_found: 'The publisher’s address could not be resolved. Check the article link or retry later.',
    article_certificate_error: 'The browser could not establish a secure connection to the publisher. Open the publisher’s site to check it.',
    article_proxy_failed: 'The article browser’s network proxy could not reach this page. Check the proxy service and its network policy.',
    article_connection_failed: 'The connection to the publisher failed. Retry or open the publisher’s site.',
    article_navigation_failed: 'The article browser could not open the publisher’s page. See the diagnostic details below.',
    article_publisher_http_error: 'The publisher returned an error page. Check the publisher’s HTTP status in the diagnostic details, or open the site directly.',
    article_extraction_failed: 'The article browser could not extract readable text. Retry or read the article at the publisher’s site.',
    article_redirect_blocked: 'The article redirected to an address the reader cannot open. Use the publisher’s site.',
    article_browser_disconnected: 'The connection to the article browser was interrupted. Retry the article.',
    article_reader_failed: 'The article reader encountered an unexpected error. See the diagnostic details below and retry.',
    article_timeout: 'Article retrieval timed out. Please retry or open the publisher’s site.',
};

const statusMessages: Record<number, string> = {
    400: 'The article request is invalid. Check the link and retry.',
    401: 'Your session has expired. Sign in again, then retry the article.',
    403: 'The article request was rejected. Refresh the page and sign in again if needed.',
    404: 'The article reader endpoint or your saved preferences could not be found. Refresh the app and check settings.',
    429: 'Too many requests. Wait a moment before retrying the article.',
    502: 'The server gateway could not reach the article reader. Retry in a moment.',
    503: 'The article reader is unavailable. Check the diagnostic details and retry.',
    504: setupMessages.article_timeout,
};

export class ArticleReaderResponseError extends Error {}

function apiErrorMessage(error: ApiError): string {
    const code: unknown = error.body?.code;
    if (typeof code === 'string' && Object.prototype.hasOwnProperty.call(setupMessages, code)) return setupMessages[code];
    return statusMessages[error.status] ?? 'The article could not be loaded. Please retry or open the publisher’s site.';
}

export function articleReaderErrorMessage(error: unknown): string {
    if (error instanceof ApiError) return apiErrorMessage(error);
    if (error instanceof ArticleReaderResponseError) return 'The article reader returned an incomplete or invalid response. Retry the article.';
    if (axios.isAxiosError(error)) return 'The connection to the app server failed or was interrupted. Check your connection and retry.';
    return 'The article could not be loaded. Please retry or open the publisher’s site.';
}

export interface ArticleReaderFailure { message: string; details: string }

export function articleReaderFailure(error: unknown, phase: string): ArticleReaderFailure {
    const details = [`Phase: ${phase}`];
    if (error instanceof ApiError) {
        details.push(`HTTP status: ${error.status}`);
        for (const [field, label] of [['code', 'Code'], ['phase', 'Server phase'], ['requestId', 'Request ID'], ['diagnostic', 'Details']]) {
            const value: unknown = error.body?.[field];
            if (typeof value === 'string') details.push(`${label}: ${value.slice(0, 2000)}`);
        }
    } else if (error instanceof Error) {
        details.push(`${error.name}: ${error.message.slice(0, 2000)}`);
    }
    return { message: articleReaderErrorMessage(error), details: details.join('\n') };
}
