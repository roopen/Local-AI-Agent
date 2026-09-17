import type { ArticleReaderProgress } from '../clients/ArticleReaderClient';

const phases = [
    { phase: 'waiting', label: 'Waiting for the article browser' },
    { phase: 'opening', label: 'Opening the publisher’s page' },
    { phase: 'extracting', label: 'Extracting the article' },
    { phase: 'detecting', label: 'Checking the article language' },
    { phase: 'translating', label: 'Translating if needed' },
] as const;

export default function ArticleReaderLoading({ progress }: { progress: ArticleReaderProgress | null }) {
    const current = phases.findIndex(item => item.phase === progress?.phase);
    const label = phases[current]?.label ?? 'Connecting to the article reader';
    return <div className="article-reader-loading">
        <div className="article-reader-loading-status" role="status" aria-live="polite" aria-atomic="true">
            <span className="article-reader-spinner" aria-hidden="true" />
            <div><strong>{label}…</strong>
                {progress?.phase === 'translating' && <p>{progress.completed ?? 0} of {progress.total} sections translated</p>}
            </div>
        </div>
        <ol className="article-reader-phases" aria-label="Article loading phases">
            {phases.map((item, index) => <li key={item.phase} aria-current={index === current ? 'step' : undefined}
                className={index < current ? 'is-complete' : ''}>
                <span aria-hidden="true">{index < current ? '✓' : index + 1}</span>{item.label}
                {index < current && <span className="article-reader-sr-only"> (complete)</span>}
            </li>)}
        </ol>
        <div className="article-reader-skeleton" aria-hidden="true"><span /><span /><span /></div>
        <p className="article-reader-loading-note">News continues loading in the background. You can close this reader at any time.</p>
    </div>;
}
