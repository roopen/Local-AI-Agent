import { NewsService as NewsApiClient } from "../clients/UserApiClient";
import NewsArticle from "../domain/NewsArticle";
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
            const newsItems = await NewsApiClient.getApiNews(parseInt(currentUser.id, 10));
            this.newsCache = newsItems.map((item) => new NewsArticle(
                item.title!,
                item.summary!,
                new Date(item.publishDate!),
                item.link!,
                item.source!
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