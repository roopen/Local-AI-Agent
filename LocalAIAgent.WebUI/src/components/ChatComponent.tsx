import { useState, type FormEvent, useEffect } from 'react';
import { HubConnection, HubConnectionState } from "@microsoft/signalr";
import { onMessageReceived, sendMessage, getConnection } from '../clients/ChatClient';

function ChatComponent() {
    const [messages, setMessages] = useState<string[]>([]);
    const [input, setInput] = useState<string>('');
    const [isConnected, setIsConnected] = useState<boolean>(false);
    const [connection, setConnection] = useState<HubConnection | null>(null);

    useEffect(() => {
        setConnection(getConnection());
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


        onMessageReceived((user, message) => {
            setMessages(prevMessages => [...prevMessages, `${user}: ${message}`]);
        });

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
        <div style={{ maxWidth: 400, margin: '0 auto' }}>
            <div style={{ border: '1px solid #ccc', padding: 10, minHeight: 200, marginBottom: 10 }}>
                {messages.length === 0 ? (
                    <p>No messages yet.</p>
                ) : (
                    messages.map((msg, idx) => (
                        <div key={idx} style={{ margin: '5px 0' }}>
                            {msg}
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

function useRef(arg0: HubConnection) {
    throw new Error('Function not implemented.');
}
