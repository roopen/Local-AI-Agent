import { useState } from 'react';
import type { ArticleReaderFailure as Failure } from '../clients/ArticleReaderError';

export default function ArticleReaderFailure({ error }: { error: Failure }) {
    const [copyStatus, setCopyStatus] = useState('');
    const copy = async () => {
        try {
            await navigator.clipboard.writeText(`${error.message}\n\n${error.details}`);
            setCopyStatus('Copied');
        } catch { setCopyStatus('Select the details below to copy them.'); }
    };
    return <section className="article-reader-error">
        <div role="alert"><strong>Couldn’t load the article</strong><p>{error.message}</p></div>
        <details>
            <summary>Diagnostic details</summary>
            <pre>{error.details}</pre>
            <button type="button" onClick={copy}>Copy error details</button>
            <span role="status">{copyStatus}</span>
        </details>
    </section>;
}
