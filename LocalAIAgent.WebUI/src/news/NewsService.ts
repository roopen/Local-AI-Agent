import { NewsService as NewsApiClient } from "../clients/UserApiClient/services/NewsService";
import { Relevancy as ClientRelevancy } from "../clients/UserApiClient/models/Relevancy";
import NewsArticle from "../domain/NewsArticle";
import type { Relevancy } from "../domain/Relevancy";
import UserService from "../users/UserService";
import type { INewsService } from "./INewsService";

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
            this.newsCache = evaluatedNews.newsArticles!.map((item) => new NewsArticle(
                item.title!,
                item.summary!,
                new Date(item.publishedDate!),
                item.link!,
                item.source!,
                item.categories! as string[],
                mapRelevancy(item.relevancy),
                item.reasoning!
            ));
            return this.newsCache;
        } catch (error) {
            throw new Error(`Failed to fetch news: ${error instanceof Error ? error.message : 'Unknown error'}`);
        } finally {
            this.isCallingGetNews = false;
        }
    }

    private async AwaitForNewsCacheInitialization(): Promise<NewsArticle[]> {
        const maxAttempts = 600; // Prevent infinite loops
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
}