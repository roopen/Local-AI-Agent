import * as signalR from "@microsoft/signalr";
import NewsArticle from "../domain/NewsArticle";
import type { Relevancy } from "../domain/Relevancy";
import type { NewsArticle as NewsDto } from "./UserApiClient/models/NewsArticle";
import { Relevancy as RelevancyDto } from "./UserApiClient/models/Relevancy";
import UserService from "../users/UserService";

type ArticleCallback = (article: NewsArticle) => void;
type CompletionCallback = () => void;
type ErrorCallback = (error: Error) => void;

function mapRelevancy(relevancy: RelevancyDto): Relevancy {
    switch (relevancy) {
        case RelevancyDto._0:
            return 'High';
        case RelevancyDto._1:
            return 'Medium';
        case RelevancyDto._2:
            return 'Low';
    }
}

export class NewsStreamClient {
    private static _instance: NewsStreamClient;
    private userService = UserService.getInstance();

    private constructor() {
    }

    public static getInstance(): NewsStreamClient {
        if (!NewsStreamClient._instance) {
            NewsStreamClient._instance = new NewsStreamClient();
        }
        return NewsStreamClient._instance;
    }

    private connection = new signalR.HubConnectionBuilder()
        .withUrl("https://localhost:7276/newsHub")
        .withAutomaticReconnect()
        .build();
    
    public getConnection() {
        return this.connection;
    }

    async start(
        onArticleReceived: ArticleCallback,
        onComplete: CompletionCallback,
        onError: ErrorCallback
    ): Promise<void> {
        if (this.connection.state !== signalR.HubConnectionState.Disconnected) {
            return;
        }

        const currentUser = this.userService.getCurrentUser();
        if (!currentUser) {
            const error = new Error("User not logged in. Cannot start news stream.");
            console.error(`❌ ${error.message}`);
            onError(error);
            return;
        }

        try {
            await this.connection.start();
            console.log("✅ Connected to SignalR hub.");

            const stream = this.connection.stream("GetNewsStream", parseInt(currentUser.id, 10));

            stream.subscribe({
                next: (item: NewsDto) => {
                    const article = new NewsArticle(
                        item.title ?? '',
                        item.summary ?? '',
                        new Date(item.publishedDate),
                        item.link ?? '',
                        item.source ?? '',
                        item.categories ?? [],
                        mapRelevancy(item.relevancy),
                        item.reasoning ?? null
                    );
                    onArticleReceived(article);
                },
                complete: () => {
                    console.log("✅ News stream completed.");
                    onComplete();
                },
                error: (err) => {
                    console.error("❌ News stream error:", err);
                    onError(err);
                }
            });
        } catch (err) {
            const error = err instanceof Error ? err : new Error("Failed to connect to SignalR hub");
            console.error(`❌ ${error.message}`);
            onError(error);
        }
    }

    async stop(): Promise<void> {
        if (this.connection.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        try {
            await this.connection.stop();
            console.log("🛑 Disconnected from SignalR hub.");
        } catch (err) {
            console.error("❌ Error disconnecting:", err);
        }
    }
}
