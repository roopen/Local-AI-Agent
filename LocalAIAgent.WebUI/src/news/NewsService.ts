import { NewsService as NewsApiClient } from "../clients/UserApiClient";
import type { NewsArticle } from "../domain/NewsArticle";
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
        return newsItems.map((item) => ({
            title: item.source!,
            content: item.content!,
            publishedDate: new Date(item.publishDate!),
            link: item.link!
        }));
    }
}