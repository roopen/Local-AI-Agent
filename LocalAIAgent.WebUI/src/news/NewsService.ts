import { NewsService as NewsApiClient } from "../clients/UserApiClient/services/NewsService";
import { Relevancy as ClientRelevancy } from "../clients/UserApiClient/models/Relevancy";
import NewsArticle from "../domain/NewsArticle";
import type { Relevancy } from "../domain/Relevancy";
import UserService from "../users/UserService";
import type { INewsService } from "./INewsService";

export default class NewsService implements INewsService {
    private static instance: NewsService | null = null;
    private isCallingGetNews: boolean = false;
    private newsCache: NewsArticle[] | null = null;

    private constructor() {}

    public static getInstance(): NewsService {
        if (!NewsService.instance) {
            NewsService.instance = new NewsService();
        }
        return NewsService.instance;
    }

    async getNews(): Promise<NewsArticle[]> {
        if (this.isCallingGetNews) {
            return this.AwaitForNewsCacheInitialization();
        }
        const userService = UserService.getInstance();
        const currentUser = userService.getCurrentUser();

        if (!currentUser) {
            throw new Error("User not logged in");
        }

        this.isCallingGetNews = true;
        try {
            const evaluatedNews = await NewsApiClient.postApiNewsGetNewsV2(parseInt(currentUser.id, 10));
            this.newsCache = evaluatedNews.newsArticles!.map(item => this.mapClientArticleToDomain(item));
            return this.newsCache;
        } catch (error) {
            throw new Error(`Failed to fetch news: ${error instanceof Error ? error.message : 'Unknown error'}`);
        } finally {
            this.isCallingGetNews = false;
        }
    }

    private async AwaitForNewsCacheInitialization(): Promise<NewsArticle[]> {
        const maxAttempts = 1500; // Prevent infinite loops
        let attempts = 0;
        
        while (this.isCallingGetNews && attempts < maxAttempts) {
            if (this.newsCache !== null && this.newsCache.length > 0) {
                return this.newsCache;
            }
            
            await new Promise(resolve => setTimeout(resolve, 100)); // 100ms delay
            attempts++;
        }
        
        if (this.newsCache !== null) {
            return this.newsCache;
        }
        
        throw new Error('Timeout waiting for news cache initialization');
    }

    public getNewsStream(
        onNewsReceived: (articles: NewsArticle[]) => void,
        onStreamEnd: () => void,
        signal: AbortSignal
    ): void {
        const userService = UserService.getInstance();
        const currentUser = userService.getCurrentUser();

        if (!currentUser) {
            throw new Error("User not logged in");
        }

        fetch(`/api/News/newsStream?userId=${currentUser.id}`, { signal })
            .then(response => {
                if (signal.aborted) {
                    return;
                }
                if (!response.ok) {
                    console.error("Stream request failed with status:", response.status);
                    onStreamEnd();
                    return;
                }
                if (!response.body) {
                    onStreamEnd();
                    return;
                }
                const reader = response.body.getReader();
                this.processStream(reader, onNewsReceived, onStreamEnd, signal);
            })
            .catch((error: unknown) => {
                if (error instanceof Error && error.name === 'AbortError') {
                    return;
                }
                console.error("Error starting stream:", error);
                onStreamEnd();
            });
    }

    private processStream(
        reader: ReadableStreamDefaultReader<Uint8Array>,
        onNewsReceived: (articles: NewsArticle[]) => void,
        onStreamEnd: () => void,
        signal: AbortSignal
    ) {
        this.readAndProcessChunks(reader, onNewsReceived, signal)
            .catch((error: unknown) => {
                if (error instanceof Error && error.name !== 'AbortError') {
                    console.error("Error processing stream:", error);
                }
            })
            .finally(() => {
                if (!signal.aborted) {
                    onStreamEnd();
                }
            });
    }

    private async readAndProcessChunks(
        reader: ReadableStreamDefaultReader<Uint8Array>,
        onNewsReceived: (articles: NewsArticle[]) => void,
        signal: AbortSignal
    ): Promise<void> {
        const decoder = new TextDecoder();
        let buffer = '';
    
        while (true) {
            const { done, value } = await reader.read();
            if (done || signal.aborted) {
                break;
            }
    
            buffer += decoder.decode(value, { stream: true });
            buffer = this.processBuffer(buffer, onNewsReceived);
        }
    
        if (buffer && !signal.aborted) {
            this.processStreamPart(buffer, onNewsReceived);
        }
    }

    private processBuffer(buffer: string, onNewsReceived: (articles: NewsArticle[]) => void): string {
        const parts = buffer.split('\n');
        const remaining = parts.pop() || '';
    
        for (const part of parts) {
            this.processStreamPart(part, onNewsReceived);
        }
    
        return remaining;
    }

    private processStreamPart(part: string, onNewsReceived: (articles: NewsArticle[]) => void) {
        if (part) {
            try {
                const articlesChunk = JSON.parse(part);
                const mappedArticles = this.mapClientArticlesToDomain(articlesChunk);
                onNewsReceived(mappedArticles);
            } catch (e) {
                console.error("Error parsing stream chunk", e);
            }
        }
    }

    private mapClientArticlesToDomain(articles: ArticleData[]): NewsArticle[] {
        return articles.map(item => this.mapArticleDataToDomain(item));
    }

    private mapClientArticleToDomain(item: { title: string | null; summary: string | null; publishedDate: string; link: string | null; source: string | null; categories: string[] | null; relevancy: ClientRelevancy; reasoning?: string | null; }): NewsArticle {
        return new NewsArticle(
            item.title!,
            item.summary!,
            new Date(item.publishedDate!),
            item.link!,
            item.source!,
            item.categories || [],
            mapRelevancy(item.relevancy),
            item.reasoning ?? null
        );
    }

    private mapArticleDataToDomain(item: ArticleData): NewsArticle {
        const get = <T>(key: keyof ArticleData) => (item[key] ?? item[key.charAt(0).toUpperCase() + key.slice(1) as keyof ArticleData]) as T;

        const title = get<string>('title')!;
        const summary = get<string>('summary')!;
        const publishedDate = new Date(get<string>('publishedDate')!);
        const link = get<string>('link')!;
        const source = get<string>('source')!;
        const categories = get<string[]>('categories') || [];
        const relevancy = mapRelevancy(get<ClientRelevancy>('relevancy'));
        const reasoning = get<string | null>('reasoning') ?? null;

        return new NewsArticle(title, summary, publishedDate, link, source, categories, relevancy, reasoning);
    }
}

type ArticleData = {
    title?: string;
    Title?: string;
    summary?: string;
    Summary?: string;
    publishedDate?: string;
    PublishedDate?: string;
    link?: string;
    Link?: string;
    source?: string;
    Source?: string;
    categories?: string[];
    Categories?: string[];
    relevancy?: ClientRelevancy;
    Relevancy?: ClientRelevancy;
    reasoning?: string;
    Reasoning?: string;
};

function mapRelevancy(relevancy: ClientRelevancy): Relevancy {
    switch (relevancy) {
        case ClientRelevancy._0:
            return 'High';
        case ClientRelevancy._1:
            return 'Medium';
        case ClientRelevancy._2:
            return 'Low';
    }
}