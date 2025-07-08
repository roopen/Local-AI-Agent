import { useState, type FormEvent, useEffect, useRef } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { HubConnectionState } from "@microsoft/signalr";
import { ChatConnection } from '../clients/ChatClient';
import ChatMessage from '../domain/ChatMessage';

function ChatComponent({ initialMessage }: { initialMessage?: string }) {
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [input, setInput] = useState<string>('');
    const [isConnected, setIsConnected] = useState<boolean>(false);
    const [connection, setConnection] = useState<ChatConnection | null>(null);
    const messagesContainerRef = useRef<HTMLDivElement>(null);
    const textareaRef = useRef<HTMLTextAreaElement>(null);

    useEffect(() => {
        setConnection(new ChatConnection());
    }, []);

    useEffect(() => {
        if (!connection) return;

        const cleanup = connection.onMessageReceived((msg: ChatMessage) => {
            if (!msg || msg.message === null || msg.message === undefined || msg.message.trim() === '') return;

            setMessages(prevMessages => {
                if (prevMessages.length > 0) {
                    const lastMessage = prevMessages[prevMessages.length - 1];
                    if (lastMessage.tryAppend(msg)) {
                        return [...prevMessages.slice(0, -1), lastMessage];
                    }
                }
                return [...prevMessages, msg];
            });
        });

        return () => {
            cleanup();
        };
    }, [connection]);

    useEffect(() => {
        if (!connection) return;

        const connect = async () => {
            if (connection.getState() === HubConnectionState.Disconnected) {
                try {
                    await connection.start();
                    console.log("Connected to SignalR Hub");
                    setIsConnected(true);
                } catch (err) {
                    console.error("SignalR Error:", err);
                }
            } else {
                setIsConnected(connection.getState() === HubConnectionState.Connected);
            }
        };

        connect();

        return () => {
            connection.stop()
                .then(() => console.log("Disconnected from SignalR Hub"))
                .catch(err => console.error("SignalR Disconnect Error:", err));
        };
    }, [connection]);

    useEffect(() => {
        if (initialMessage && isConnected && connection) {
            connection.sendMessage("User", initialMessage);
        }
    }, [initialMessage, isConnected, connection]);

    useEffect(() => {
        if (messagesContainerRef.current) {
            messagesContainerRef.current.scrollTop = messagesContainerRef.current.scrollHeight;
        }
    }, [messages]);

    useEffect(() => {
        if (textareaRef.current) {
            textareaRef.current.style.height = 'auto';
            textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
        }
    }, [input]);

    const sendMessage = async () => {
        if (input.trim() === '' || !isConnected || !connection) return;

        const user = "You";
        const message = input;
        setInput('');
        try {
            await connection.sendMessage(user, message);
        } catch (err) {
            console.error("Failed to send message:", err);
        }
    };

    const handleSubmit = async (e: FormEvent) => {
        e.preventDefault();
        await sendMessage();
    };

    const handleKeyDown = async (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            await sendMessage();
        }
    };

    return (
        <div style={{ display: 'flex', height: '100%' }}>
            <div style={{ flex: 1, display: 'flex', flexDirection: 'column', margin: '0 auto', padding: '10px' }}>
                <div style={{ border: '1px solid #ccc', marginBottom: 10, flex: 1, overflow: 'hidden' }}>
                    <div ref={messagesContainerRef} style={{ height: '100%', overflowY: 'auto', padding: 10 }}>
                        {messages.length === 0 ? (
                            <p>No messages yet.</p>
                        ) : (
                            messages.map((msg, idx) => (
                                <div key={idx} style={{ margin: '5px 0' }}>
                                    <div style={{ whiteSpace: 'pre-wrap' }}>
                                        {msg.user}: <ReactMarkdown
                                            remarkPlugins={[remarkGfm]}
                                            components={{
                                                a: ({ ...props }) => <a {...props} target="_blank" rel="noopener noreferrer" />
                                            }}
                                        >{msg.message}</ReactMarkdown>
                                    </div>
                                </div>
                            ))
                        )}
                    </div>
                </div>
                <form onSubmit={handleSubmit} style={{ display: 'flex' }}>
                    <textarea
                        ref={textareaRef}
                        rows={1}
                        value={input}
                        onChange={e => setInput(e.target.value)}
                        onKeyDown={handleKeyDown}
                        style={{ flex: 1, marginRight: 5, resize: 'none', overflowY: 'auto', maxHeight: '150px' }}
                        placeholder={isConnected ? "Type a message... (Shift+Enter for new line)" : "Connecting..."}
                        disabled={!isConnected}
                    />
                    <button type="submit" disabled={!isConnected}>Send</button>
                </form>
                {!isConnected && <p style={{ color: 'red' }}>Connecting to chat server...</p>}
            </div>
        </div>
    );
}

export default ChatComponent;
