import { NewsService } from "./UserApiClient/services/NewsService";
import type { ExpandedNewsResult } from "./UserApiClient/models/ExpandedNewsResult";

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
                    console.log('getExpandedNews');
                    console.log('article was translated:' + result.articleWasTranslated);
                    console.log('translation:' + result.translation);
                    for (const detail of result.termsAndExplanations!) {
                        console.log('term:' + detail.key!.term);
                        console.log('explanation:' + detail.value!.explanation);
                    }
                    resolve(result);
                })
                .catch((err: Error) => {
                    console.error("❌ Error getting expanded news:", err);
                    reject(err);
                });
        });
    }
}