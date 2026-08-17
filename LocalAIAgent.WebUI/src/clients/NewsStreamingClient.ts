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

export const LLM_CONNECTION_FAILURE_CODE = "LLM_CONNECTION_FAILED:";

export class LlmConnectionError extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'LlmConnectionError';
    }
}

export function extractLlmConnectionFailureMessage(error: unknown): string | null {
    const message = error instanceof Error ? error.message : String(error);
    const markerIndex = message.indexOf(LLM_CONNECTION_FAILURE_CODE);
    if (markerIndex < 0) return null;

    const detail = message.slice(markerIndex + LLM_CONNECTION_FAILURE_CODE.length).trim();
    return detail.length > 0 ? detail : 'The AI service could not be reached.';
}

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
    private _articleCount = 0;
    private _loadStartTime: Date | null = null;
    private _loadEndTime: Date | null = null;
    private _streamSubscription: signalR.ISubscription<NewsDto> | null = null;
    private _lifecycleVersion = 0;

    public get isLoading(): boolean {
        return this._isLoading;
    }

    public get articleCount(): number {
        return this._articleCount;
    }

    public get loadStartTime(): Date | null {
        return this._loadStartTime;
    }

    public get loadEndTime(): Date | null {
        return this._loadEndTime;
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
        .withUrl(new URL('/newsHub', window.location.origin).toString())
        .withAutomaticReconnect()
        .build();
    
    public getConnection() {
        return this.connection;
    }

    // Connection cancellation and stream lifecycle checks are deliberately colocated.
    // eslint-disable-next-line complexity
    async start(
        onArticleReceived: ArticleCallback,
        onComplete: CompletionCallback,
        onError: ErrorCallback,
        onLoadingChange: LoadingChangeCallback
    ): Promise<void> {
        if (this.connection.state !== signalR.HubConnectionState.Disconnected) {
            return;
        }

        const lifecycleVersion = ++this._lifecycleVersion;

        const currentUser = this.userService.getCurrentUser();
        if (!currentUser) {
            const error = new Error("User not logged in. Cannot start news stream.");
            console.error(`❌ ${error.message}`);
            onError(error);
            return;
        }

        try {
            this._isLoading = true;
            this._articleCount = 0;
            this._loadStartTime = new Date();
            this._loadEndTime = null;
            onLoadingChange(true);
            await this.connection.start();

            // stop() may have been called while the connection was still starting.
            // In that case, never create a server stream after the component is gone.
            if (lifecycleVersion !== this._lifecycleVersion) {
                if (this.connection.state !== signalR.HubConnectionState.Disconnected) {
                    await this.connection.stop();
                }
                return;
            }

            console.log("✅ Connected to SignalR hub.");

            const stream = this.connection.stream("GetNewsStream");

            this._streamSubscription = stream.subscribe({
                next: (item: NewsDto) => {
                    if (lifecycleVersion !== this._lifecycleVersion) return;
                    this._articleCount++;
                    onArticleReceived(mapNewsDto(item));
                },
                complete: () => {
                    if (lifecycleVersion !== this._lifecycleVersion) return;
                    this._streamSubscription = null;
                    this._isLoading = false;
                    this._loadEndTime = new Date();
                    onLoadingChange(false);
                    console.log("✅ News stream completed.");
                    onComplete();
                },
                error: (err) => {
                    if (lifecycleVersion !== this._lifecycleVersion) return;
                    this._streamSubscription = null;
                    this._isLoading = false;
                    this._loadEndTime = new Date();
                    onLoadingChange(false);
                    console.error("❌ News stream error:", err);
                    const error = err instanceof Error ? err : new Error(String(err));
                    const llmConnectionMessage = extractLlmConnectionFailureMessage(error);
                    onError(llmConnectionMessage
                        ? new LlmConnectionError(llmConnectionMessage)
                        : error);
                }
            });
        } catch (err) {
            if (lifecycleVersion !== this._lifecycleVersion) return;
            this._isLoading = false;
            this._loadEndTime = new Date();
            onLoadingChange(false);
            const error = err instanceof Error ? err : new Error("Failed to connect to SignalR hub");
            console.error(`❌ ${error.message}`);
            onError(error);
        }
    }

    async stop(): Promise<void> {
        ++this._lifecycleVersion;
        this._streamSubscription?.dispose();
        this._streamSubscription = null;
        this._isLoading = false;
        this._loadEndTime = new Date();

        if (this.connection.state === signalR.HubConnectionState.Disconnected) return;

        try {
            await this.connection.stop();
            console.log("🛑 Disconnected from SignalR hub.");
        } catch (err) {
            console.error("❌ Error disconnecting:", err);
        }
    }
}
