import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:7276/chatHub")
    .withAutomaticReconnect()
    .build();

export function getConnection() {
    return connection;
}

export function onMessageReceived(callback: (user: string, message: string) => void) {
    connection.on("ReceiveMessage", callback);
}

export async function sendMessage(user: string, message: string) {
    try {
        await connection.invoke("SendMessage", user, message);
        console.log(`Message sent: ${message}`);
    } catch (err) {
        console.error("Error sending message:", err);
    }
}