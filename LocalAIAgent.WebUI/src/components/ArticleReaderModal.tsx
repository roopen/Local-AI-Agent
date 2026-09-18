import { useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import type NewsArticle from '../domain/NewsArticle';
import { readArticle, type ReadArticleResult, type ArticleReaderProgress } from '../clients/ArticleReaderClient';
import ArticleReaderLoading from './ArticleReaderLoading';
import { articleReaderFailure, type ArticleReaderFailure as Failure } from '../clients/ArticleReaderError';
import ArticleReaderFailure from './ArticleReaderFailure';
import './ArticleReaderModal.css';

function safeArticleUrl(value: string): string | undefined {
    try { const url = new URL(value); return ['https:', 'http:'].includes(url.protocol) ? url.href : undefined; }
    catch { return undefined; }
}

function ReaderHeader({ title, url, onClose }: { title: string; url: string; onClose: () => void }) {
    return <header className="article-reader-header">
        <div className="article-reader-heading">
            <h2 id="reader-title">{title}</h2>
            <PublisherLink url={url} />
        </div>
        <button type="button" onClick={onClose} autoFocus aria-label="Close article">Close</button>
    </header>;
}

function PublisherLink({ url }: { url: string }) {
    const href = safeArticleUrl(url);
    return href ? <a className="article-reader-publisher" href={href} target="_blank" rel="noopener noreferrer"
        aria-label="Open publisher’s site" title="Open publisher’s site">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"
            strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
            <path d="M15 3h6v6M10 14 21 3M21 14v5a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5" />
        </svg>
    </a> : null;
}

function ReaderNotices({ result }: { result: ReadArticleResult }) {
    const messages: Record<string, string> = {
        partial: 'Only part of this article could be retrieved.',
        blocked: 'This article requires access at the publisher’s site.',
        unavailable: 'Article text could not be extracted from this page.',
    };
    return <>
        {messages[result.original.status] && <p role="status">{messages[result.original.status]}</p>}
        {result.message && <p role="status">{result.message}</p>}
    </>;
}

function ArticleText({ body, language }: { body: string; language: string | null | undefined }) {
    return <article className="article-reader-body" lang={language ?? undefined}>
        <ReactMarkdown skipHtml disallowedElements={['img']} components={{
            a: ({ href, children }) => <a href={safeArticleUrl(href || '')} target="_blank" rel="noopener noreferrer">{children}</a>,
        }}>{body}</ReactMarkdown>
    </article>;
}

function ArticleMetadata({ author, publishedAt }: { author?: string | null; publishedAt?: string | null }) {
    const date = new Date(publishedAt ?? '');
    const validDate = !Number.isNaN(date.getTime());
    if (!author && !validDate) return null;
    return <p className="article-reader-metadata">
        {author}{author && validDate && ' · '}
        {validDate && <time dateTime={date.toISOString()}>
            {new Intl.DateTimeFormat(undefined, { dateStyle: 'long', timeStyle: 'short' }).format(date)}
        </time>}
    </p>;
}

function RetryArticle({ result, onRetry }: { result: ReadArticleResult; onRetry: () => void }) {
    if (result.original.status === 'complete' && ['complete', 'notNeeded'].includes(result.translationStatus)) return null;
    return <button type="button" onClick={onRetry}>Retry article</button>;
}

function ReaderResult({ result, onClose, onRetry }: { result: ReadArticleResult; onClose: () => void; onRetry: () => void }) {
    const translated = result.translationStatus === 'complete';
    const title = translated ? result.translatedTitle : result.original.title;
    const body = translated ? result.translatedMarkdown : result.original.markdown;
    const language = translated ? result.targetLanguage : result.detectedLanguage;
    return <>
        <div className="article-reader-chrome">
            <ReaderHeader title={title ?? 'Article'} url={result.original.sourceUrl} onClose={onClose} />
            <div className="article-reader-toolbar">
                <RetryArticle result={result} onRetry={onRetry} />
            </div>
        </div>
        <div className="article-reader-scroll" role="region" aria-label="Article content" tabIndex={0}>
            <ReaderNotices result={result} />
            <ArticleMetadata author={result.original.author} publishedAt={result.original.publishedAt} />
            <ArticleText body={body ?? ''} language={language} />
        </div>
    </>;
}

function ReaderRequest({ article, onClose, onRetry }: { article: NewsArticle; onClose: () => void; onRetry: () => void }) {
    const [result, setResult] = useState<ReadArticleResult | null>(null);
    const [error, setError] = useState<Failure | null>(null);
    const [progress, setProgress] = useState<ArticleReaderProgress | null>(null);
    useEffect(() => {
        let active = true;
        let phase = 'connecting';
        const pending = readArticle(article.Link, value => { if (active) { phase = value.phase; setProgress(value); } });
        pending.then(value => { if (active) setResult(value); }).catch(cause => { if (active) setError(articleReaderFailure(cause, phase)); });
        return () => { active = false; pending.cancel(); };
    }, [article.Link]);

    if (result) return <ReaderDialog result={result} onClose={onClose} onRetry={onRetry} />;
    return <aside className="article-reader-progress" aria-label="Full article request">
        <div className="article-reader-progress-header">
            <strong>{article.Title}</strong>
            <button type="button" onClick={onClose} aria-label="Close article">{error ? 'Dismiss' : 'Cancel'}</button>
        </div>
        {error && <div className="article-reader-toolbar">
            <button type="button" onClick={onRetry}>Retry article</button>
        </div>}
        {error ? <ArticleReaderFailure error={error} />
            : <ArticleReaderLoading progress={progress} />}
    </aside>;
}

function ReaderDialog({ result, onClose, onRetry }: { result: ReadArticleResult; onClose: () => void; onRetry: () => void }) {
    const dialog = useRef<HTMLDialogElement>(null);
    const [opener] = useState(() => document.activeElement as HTMLElement | null);
    useEffect(() => {
        const element = dialog.current;
        element?.showModal();
        return () => { element?.close(); opener?.focus(); };
    }, [opener]);
    return <dialog ref={dialog} className="article-reader" aria-labelledby="reader-title"
        onCancel={event => { event.preventDefault(); onClose(); }}>
        <ReaderResult result={result} onClose={onClose} onRetry={onRetry} />
    </dialog>;
}

export default function ArticleReaderModal({ article, onClose }: { article: NewsArticle; onClose: () => void }) {
    const [attempt, setAttempt] = useState(0);
    return <ReaderRequest key={`${article.Link}:${attempt}`} article={article} onClose={onClose} onRetry={() => setAttempt(value => value + 1)} />;
}
