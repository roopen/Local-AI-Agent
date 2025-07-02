import * as signalR from "@microsoft/signalr";
import ChatMessage from "../domain/ChatMessage";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:7276/chatHub")
    .withAutomaticReconnect()
    .build();

export function getConnection() {
    return connection;
}

export function onMessageReceived(callback: (message: ChatMessage) => void): () => void {
    const handler = (message: string, user: string, id: string) => {
        callback(new ChatMessage(user, message, id));
    };
    connection.on("ReceiveMessage", handler);
    // Return a cleanup function to remove the listener
    return () => {
        connection.off("ReceiveMessage", handler);
    };
}

export async function sendMessage(user: string, message: string) {
    try {
        await connection.invoke("SendMessage", user, message);
        console.log(`Message sent: ${message}`);
    } catch (err) {
        console.error("Error sending message:", err);
    }
}