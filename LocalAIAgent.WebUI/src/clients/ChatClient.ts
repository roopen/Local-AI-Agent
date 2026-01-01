import * as signalR from "@microsoft/signalr";
import ChatMessage from "../domain/ChatMessage";
import { type ExpandedNewsResult, NewsService } from "./UserApiClient";

export class ChatConnection {
    private connection: signalR.HubConnection;

    constructor() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("https://localhost:7276/chatHub")
            .withAutomaticReconnect()
            .build();
    }

    public start = async () => {
        await this.connection.start();
    }

    public stop = async () => {
        await this.connection.stop();
    }

    public onMessageReceived = (callback: (message: ChatMessage) => void) => {
        const handler = (message: string, user: string, id: string) => {
            callback(new ChatMessage(user, message, id));
        };
        this.connection.on("ReceiveMessage", handler);
        return () => {
            this.connection.off("ReceiveMessage", handler);
        };
    }

    public sendMessage = async (user: string, message: string) => {
        await this.connection.invoke("SendMessage", user, message);
    }

    public getState = () => {
        return this.connection.state;
    }
}