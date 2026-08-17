import { NewsService } from './UserApiClient/services/NewsService';
import type { ExpandedNewsResult } from './UserApiClient/models/ExpandedNewsResult';

export interface NewsFeedbackDto {
    articleLink: string;
    articleTitle: string;
    articleSummary: string;
    articleTopic: string;
    isLiked: boolean;
    reason?: string;
}

export class NewsClient {
    private static _instance: NewsClient;

    private constructor() {
    }

    public static getInstance(): NewsClient {
        if (!NewsClient._instance) {
            NewsClient._instance = new NewsClient();
        }
        return NewsClient._instance;
    }

    async getExpandedNews(article: string): Promise<ExpandedNewsResult> {
        return await NewsService.postApiNewsGetExpandedNews(article);
    }

    async submitFeedback(feedback: NewsFeedbackDto): Promise<void> {
        await NewsService.postApiNewsFeedback(feedback);
    }
}
