import { NewsService as NewsApiClient } from "../clients/UserApiClient";
import NewsArticle from "../domain/NewsArticle";
import UserService from "../users/UserService";
import type { INewsService } from "./INewsService";


export default class NewsService implements INewsService {
    async getNews(): Promise<NewsArticle[]> {
        const userService = UserService.getInstance();
        const currentUser = userService.getCurrentUser();

        if (!currentUser) {
            throw new Error("User not logged in");
        }

        const newsItems = await NewsApiClient.getApiNews(parseInt(currentUser.id, 10));
        return newsItems.map((item) => new NewsArticle(
            item.title!,
            item.summary!,
            new Date(item.publishDate!),
            item.link!,
            item.source!
        ));
    }
}