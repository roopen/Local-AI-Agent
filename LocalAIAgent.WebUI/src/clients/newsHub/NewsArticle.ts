import type { Relevancy } from './Relevancy';

export type NewsArticle = {
    title?: string | null;
    summary?: string | null;
    publishedDate: string;
    link?: string | null;
    source?: string | null;
    categories?: Array<string> | null;
    relevancy: Relevancy;
    reasoning?: string | null;
    topic?: string | null;
    event?: string | null;
    inputTokens?: number | null;
    outputTokens?: number | null;
};
