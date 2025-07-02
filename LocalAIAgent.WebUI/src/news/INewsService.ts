
import type { NewsArticle } from "../domain/NewsArticle";

export interface INewsService {
    getNews(userId: number): Promise<NewsArticle[]>;
}