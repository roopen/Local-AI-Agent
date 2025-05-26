import { useState, type FormEvent, useEffect } from 'react';
import { HubConnection, HubConnectionState } from "@microsoft/signalr";
import { onMessageReceived, sendMessage, getConnection } from '../clients/ChatClient';
import ChatMessage from '../clients/ChatMessage';

function ChatComponent() {
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [input, setInput] = useState<string>('');
    const [isConnected, setIsConnected] = useState<boolean>(false);
    const [connection, setConnection] = useState<HubConnection | null>(null);

    useEffect(() => {
        setConnection(getConnection());
    }, []);

    useEffect(() => {
        const cleanup = onMessageReceived((msg) => {
            console.log(msg.user + ': ' + msg.message);
            if (!msg || msg.message === null || msg.message === undefined) return;

            setMessages(prevMessages => {
                console.log('messages length: ' + prevMessages.length);

                if (prevMessages.length > 0) {
                    const lastMessage = prevMessages[prevMessages.length - 1];

                    console.log("Previous message:", lastMessage.message);
                    console.log("Is instance of ChatMessage:", lastMessage instanceof ChatMessage);

                    // Check if the new message can be appended to the last message
                    if (lastMessage.tryAppend(msg)) {
                        // If appended, return a new array with the last message updated
                        return [...prevMessages.slice(0, -1), lastMessage];
                    } else {
                        // If not appended, add the new message to the end
                        return [...prevMessages, msg];
                    }
                }
                // If no previous messages, just add the new message
                return [...prevMessages, msg];
            });
        });

        // Clean up the listener when the component unmounts or the effect re-runs
        return () => {
            cleanup();
        };
    }, []);

    useEffect(() => {
        if (!connection) return;

        // Only start the connection if it's disconnected
        if (connection.state === HubConnectionState.Disconnected) {
            connection.start()
                .then(() => {
                    console.log("Connected to SignalR Hub");
                    setIsConnected(true);
                })
                .catch(err => console.error("SignalR Error:", err));
        } else {
            // If already connected or connecting, just update the state
            setIsConnected(connection.state === HubConnectionState.Connected);
        }

        // Clean up connection on component unmount
        return () => {
            connection.stop()
                .then(() => console.log("Disconnected from SignalR Hub"))
                .catch(err => console.error("SignalR Disconnect Error:", err));
        };
    }, [connection]);

    const handleSubmit = async (e: FormEvent) => {
        e.preventDefault();
        if (input.trim() === '' || !isConnected) return;

        // Assuming a default user for now, replace with actual user logic if available
        const user = "User";
        await sendMessage(user, input);

        setInput('');
    };

    return (
        <div style={{ maxWidth: 800, margin: '0 auto' }}>
            <div style={{ border: '1px solid #ccc', padding: 10, minHeight: 200, marginBottom: 10 }}>
                {messages.length === 0 ? (
                    <p>No messages yet.</p>
                ) : (
                    messages.map((msg, idx) => (
                        <div key={idx} style={{ margin: '5px 0' }}>
                            {msg.user}: {msg.message}
                        </div>
                    ))
                )}
            </div>
            <form onSubmit={handleSubmit} style={{ display: 'flex' }}>
                <input
                    type="text"
                    value={input}
                    onChange={e => setInput(e.target.value)}
                    style={{ flex: 1, marginRight: 5 }}
                    placeholder={isConnected ? "Type a message..." : "Connecting..."}
                    disabled={!isConnected}
                />
                <button type="submit" disabled={!isConnected}>Send</button>
            </form>
            {!isConnected && <p style={{ color: 'red' }}>Connecting to chat server...</p>}
        </div>
    );
}

export default ChatComponent;
