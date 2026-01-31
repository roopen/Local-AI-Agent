import { useState, type FormEvent, useEffect, useRef } from 'react';
import { HubConnectionState } from "@microsoft/signalr";
import { ChatConnection } from '../clients/ChatClient';
import ChatMessage from '../domain/ChatMessage';
import type NewsArticle from '../domain/NewsArticle';
import { NewsClient } from '../clients/NewsClient';
import type { ExpandedNewsResult } from '../clients/UserApiClient/models/ExpandedNewsResult';

// eslint-disable-next-line complexity
function ChatComponent({ article }: { article: NewsArticle }) {
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [input, setInput] = useState<string>('');
    const [isConnected, setIsConnected] = useState<boolean>(false);
    const [connection, setConnection] = useState<ChatConnection | null>(null);
    const messagesContainerRef = useRef<HTMLDivElement>(null);
    const textareaRef = useRef<HTMLTextAreaElement>(null);
    const [newsArticleExpandedDetails, setNewsArticleExpandedDetails] = useState<ExpandedNewsResult | null>(null);
    const [loadingNewsDetails, setLoadingNewsDetails] = useState<boolean>(false);

    useEffect(() => {
        setConnection(new ChatConnection());

        const fetchExpandedDetails = async () => {
            try {
                const newsClient = NewsClient.getInstance();
                const expandedDetails = await newsClient.getExpandedNews(article.Title + ' ' + article.Summary);
                setNewsArticleExpandedDetails(expandedDetails);
            } catch (err) {
                console.error("Error fetching expanded news details:", err);
            }
        };

        if (!loadingNewsDetails) {
            fetchExpandedDetails();
            setLoadingNewsDetails(true);
        }
    }, [article, newsArticleExpandedDetails]);

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
            if (messages.length === 0) {
                const systemMessage = `${article.Title} ${article.Summary}`;
                await connection.sendMessage('AI', systemMessage);
            }
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
                    <div id="article-expanded-details" style={{ borderBottom: '1px solid #ccc', padding: '10px', maxHeight: '150px', overflowY: 'auto' }}>
                        <span style={{ fontStyle: 'bold' }}>
                            {newsArticleExpandedDetails?.articleWasTranslated ? `Translation: ${newsArticleExpandedDetails.translation}` : ''}
                            {newsArticleExpandedDetails?.termsAndExplanations?.map(te => (
                                <div key={te.key!.term}>
                                    <strong>{te.key!.term}:</strong> {te.value!.explanation}
                                </div>
                            ))}
                        </span>
                    </div>
                    <div ref={messagesContainerRef} style={{ height: '100%', overflowY: 'auto', padding: 10 }}>
                        {messages.length === 0 ? (
                            <p>Ask away.</p>
                        ) : (
                            messages.map((msg, idx) => (
                                (idx === 0) ? null : (
                                    <div key={idx} style={{ margin: '5px 0', backgroundColor: idx % 2 !== 0 ? '#2f2e2e' : 'inherit' }}>
                                        <div style={{ whiteSpace: 'pre-wrap' }}>
                                            <strong>{msg.user}</strong>:<br/>
                                            {msg.message}
                                        </div>
                                    </div>
                                )))
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
