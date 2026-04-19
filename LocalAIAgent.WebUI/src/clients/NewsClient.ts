import axios from "axios";
import { NewsService } from "./UserApiClient/services/NewsService";
import type { ExpandedNewsResult } from "./UserApiClient/models/ExpandedNewsResult";
import { OpenAPI } from "./UserApiClient/core/OpenAPI";

export interface NewsFeedbackDto {
    userId: number;
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
        return new Promise<ExpandedNewsResult>((resolve, reject) => {
            NewsService.postApiNewsGetExpandedNews(article)
                .then((result: ExpandedNewsResult) => {
                    resolve(result);
                })
                .catch((err: Error) => {
                    console.error("❌ Error getting expanded news:", err);
                    reject(err);
                });
        });
    }

    async submitFeedback(feedback: NewsFeedbackDto): Promise<void> {
        await axios.post(
            `${OpenAPI.BASE}/api/News/Feedback`,
            feedback,
            { withCredentials: true }
        );
    }
}