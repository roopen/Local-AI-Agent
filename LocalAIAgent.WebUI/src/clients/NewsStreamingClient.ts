import * as signalR from "@microsoft/signalr";
import NewsArticle from "../domain/NewsArticle";
import type { Relevancy } from "../domain/Relevancy";
import type { NewsArticle as NewsDto } from "./newsHub/NewsArticle";
import { Relevancy as RelevancyDto } from "./newsHub/Relevancy";
import UserService from "../users/UserService";

type ArticleCallback = (article: NewsArticle) => void;
type CompletionCallback = () => void;
type ErrorCallback = (error: Error) => void;
type LoadingChangeCallback = (isLoading: boolean) => void;

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

function extractTextFields(item: NewsDto) {
    return {
        title: item.title ?? '',
        summary: item.summary ?? '',
        link: item.link ?? '',
        source: item.source ?? '',
        categories: item.categories ?? [],
    };
}

function extractNullableFields(item: NewsDto) {
    return {
        reasoning: item.reasoning ?? null,
        topic: item.topic ?? null,
        event: item.event ?? null,
        inputTokens: item.inputTokens ?? null,
        outputTokens: item.outputTokens ?? null,
    };
}

function mapNewsDto(item: NewsDto): NewsArticle {
    const { title, summary, link, source, categories } = extractTextFields(item);
    const { reasoning, topic, event, inputTokens, outputTokens } = extractNullableFields(item);
    return new NewsArticle(
        title,
        summary,
        new Date(item.publishedDate),
        link,
        source,
        categories,
        mapRelevancy(item.relevancy),
        reasoning,
        topic,
        event,
        inputTokens,
        outputTokens
    );
}

export class NewsStreamClient {
    private static _instance: NewsStreamClient;
    private userService = UserService.getInstance();
    private _isLoading = false;

    public get isLoading(): boolean {
        return this._isLoading;
    }

    private constructor() {
    }

    public static getInstance(): NewsStreamClient {
        if (!NewsStreamClient._instance) {
            NewsStreamClient._instance = new NewsStreamClient();
        }
        return NewsStreamClient._instance;
    }

    private connection = new signalR.HubConnectionBuilder()
        .withUrl("https://apiainews.dev.localhost:7276/newsHub")
        .withAutomaticReconnect()
        .build();
    
    public getConnection() {
        return this.connection;
    }

    async start(
        onArticleReceived: ArticleCallback,
        onComplete: CompletionCallback,
        onError: ErrorCallback,
        onLoadingChange: LoadingChangeCallback
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
            this._isLoading = true;
            onLoadingChange(true);
            await this.connection.start();
            console.log("✅ Connected to SignalR hub.");

            const stream = this.connection.stream("GetNewsStream", parseInt(currentUser.id, 10));

            stream.subscribe({
                next: (item: NewsDto) => onArticleReceived(mapNewsDto(item)),
                complete: () => {
                    this._isLoading = false;
                    onLoadingChange(false);
                    console.log("✅ News stream completed.");
                    onComplete();
                },
                error: (err) => {
                    this._isLoading = false;
                    onLoadingChange(false);
                    console.error("❌ News stream error:", err);
                    onError(err);
                }
            });
        } catch (err) {
            this._isLoading = false;
            onLoadingChange(false);
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
            this._isLoading = false;
            await this.connection.stop();
            console.log("🛑 Disconnected from SignalR hub.");
        } catch (err) {
            console.error("❌ Error disconnecting:", err);
        }
    }
}
