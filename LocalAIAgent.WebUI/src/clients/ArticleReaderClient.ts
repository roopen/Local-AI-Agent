import { OpenAPI } from './UserApiClient/core/OpenAPI';
import { request } from './UserApiClient/core/request';
import axios from 'axios';
import { CancelablePromise } from './UserApiClient/core/CancelablePromise';
import { ApiError } from './UserApiClient/core/ApiError';
import { ArticleReaderResponseError } from './ArticleReaderError';

export interface ArticleContent {
    title: string;
    markdown: string;
    sourceUrl: string;
    language?: string | null;
    author?: string | null;
    publishedAt?: string | null;
    status: 'complete' | 'partial' | 'blocked' | 'unavailable';
}

export interface ReadArticleResult {
    original: ArticleContent;
    translatedMarkdown?: string | null;
    translatedTitle?: string | null;
    detectedLanguage?: string | null;
    targetLanguage: string;
    translationStatus: 'notNeeded' | 'complete' | 'failed' | 'unavailable';
    message?: string | null;
}

export interface ArticleReaderProgress {
    phase: 'waiting' | 'opening' | 'extracting' | 'detecting' | 'translating';
    completed?: number | null;
    total?: number | null;
}

type ReaderEvent = { type: 'progress'; progress: ArticleReaderProgress }
    | { type: 'result'; result: ReadArticleResult }
    | { type: 'error'; status: number; code?: string };

function decodeResponse(data: string, contentType: unknown): unknown {
    if (String(contentType).includes('application/x-ndjson')) return data;
    try { return JSON.parse(data); }
    catch { return data; } // Preserve HTTP status for non-JSON gateway error pages.
}

// One authenticated HTTP request owns both progress and its own cancellation scope.
export function readArticle(url: string, onProgress?: (progress: ArticleReaderProgress) => void): CancelablePromise<ReadArticleResult> {
    return new CancelablePromise((resolve, reject, onCancel) => {
        let offset = 0;
        let result: ReadArticleResult | undefined;
        let failure: unknown;
        const options = { method: 'POST' as const, url: '/api/News/ReadArticle', body: { url },
            mediaType: 'application/json', headers: { Accept: 'application/x-ndjson' } };
        const handle = (event: ReaderEvent) => {
            if (event.type === 'progress') onProgress?.(event.progress);
            if (event.type === 'result') result = event.result;
            if (event.type === 'error') failure = new ApiError(options,
                { url: options.url, ok: false, status: event.status, statusText: 'Reader failed', body: event }, 'Reader failed');
        };
        const receive = (text: string) => {
            if (onCancel.isCancelled) return;
            let end: number;
            while ((end = text.indexOf('\n', offset)) >= 0) {
                const line = text.slice(offset, end).trim();
                offset = end + 1;
                if (!line) continue;
                try {
                    handle(JSON.parse(line) as ReaderEvent);
                } catch { failure = new ArticleReaderResponseError('Invalid JSON in the article response stream.'); }
            }
        };
        const transport = axios.create({
            responseType: 'text',
            transformResponse: [(data: string, headers) => decodeResponse(data, headers['content-type'])],
            onDownloadProgress: event => {
                const xhr = event.event?.target as XMLHttpRequest | undefined;
                if (typeof xhr?.responseText === 'string') receive(xhr.responseText);
            },
        });
        const pending = request<string>(OpenAPI, options, transport);
        onCancel(() => pending.cancel());
        pending.then(text => {
            if (typeof text !== 'string') throw new ArticleReaderResponseError('Expected an article progress stream; received a different response format.');
            receive(text);
            if (failure) throw failure;
            if (!result) throw new ArticleReaderResponseError('The article response ended before completion.');
            resolve(result);
        }).catch(reject);
    });
}
