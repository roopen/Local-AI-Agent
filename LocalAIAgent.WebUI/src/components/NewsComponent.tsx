import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { NewsStreamClient } from '../clients/NewsStreamingClient';
import NewsArticle from '../domain/NewsArticle';
import ChatComponent from './ChatComponent';
import { Button, Chip } from '@progress/kendo-react-buttons';
import { TextArea } from '@progress/kendo-react-inputs';
import { NewsClient } from '../clients/NewsClient';
import UserService from '../users/UserService';

const newsClient = NewsClient.getInstance();
const userService = UserService.getInstance();

const NewsComponent: React.FC = () => {
    const [articles, setArticles] = useState<NewsArticle[]>([]);
    const [selectedArticleIndex, setSelectedArticleIndex] = useState<number | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [dots, setDots] = useState(1);
    const [selectedSource, setSelectedSource] = useState<string | null>(null);
    const [feedback, setFeedback] = useState<Record<string, boolean>>({});
    const [pendingFeedback, setPendingFeedback] = useState<{ article: NewsArticle; isLiked: boolean } | null>(null);
    const [feedbackReason, setFeedbackReason] = useState('');
    const [isDark, setIsDark] = useState(() => window.matchMedia('(prefers-color-scheme: dark)').matches);
    const newsStreamClient = NewsStreamClient.getInstance();

    useEffect(() => {
        const mq = window.matchMedia('(prefers-color-scheme: dark)');
        const handler = (e: MediaQueryListEvent) => setIsDark(e.matches);
        mq.addEventListener('change', handler);
        return () => mq.removeEventListener('change', handler);
    }, []);

    const toggleChat = (index: number) => {
        if (selectedArticleIndex === index) {
            setSelectedArticleIndex(null);
        } else {
            setSelectedArticleIndex(index);
        }
    };

    const handleFeedback = useCallback(async (article: NewsArticle, isLiked: boolean, reason?: string) => {
        const user = userService.getCurrentUser();
        if (!user) return;

        const key = article.Link;
        const isToggleOff = feedback[key] === isLiked;

        try {
            await newsClient.submitFeedback({
                userId: parseInt(user.id, 10),
                articleLink: article.Link,
                articleTitle: article.Title,
                articleSummary: article.Summary,
                isLiked,
                reason
            });
            if (isToggleOff) {
                setFeedback(prev => {
                    const next = { ...prev };
                    delete next[key];
                    return next;
                });
            } else {
                setFeedback(prev => ({ ...prev, [key]: isLiked }));
            }
        } catch (err) {
            console.error('❌ Failed to submit feedback:', err);
        }
    }, [feedback]);

    useEffect(() => {
        if (isLoading) {
            const interval = setInterval(() => {
                setDots(d => (d % 3) + 1);
            }, 600);
            return () => clearInterval(interval);
        }
    }, [isLoading]);

    useEffect(() => {
        const handleNewArticle = (newArticle: NewsArticle) => {
            setArticles(prevArticles => [...prevArticles, newArticle]);
        };

        const handleStreamEnd = () => {
            setIsLoading(false);
        };

        const handleError = (err: Error) => {
            setError(`Error loading articles: ${err.message}`);
            console.error(err);
            setIsLoading(false);
        };

        console.log('Starting news stream...');
        setIsLoading(true);
        newsStreamClient.start(handleNewArticle, handleStreamEnd, handleError);

        return () => {
            console.log('Stopping news stream...');
            newsStreamClient.stop();
            setIsLoading(false);
        };
    }, []);

    const sources = useMemo(() => {
        const set = new Set<string>();
        for (const a of articles) {
            if (a.Source) {
                set.add(a.Source);
            }
        }
        return Array.from(set).sort((a, b) => a.localeCompare(b));
    }, [articles]);

    const filteredArticles = useMemo(() => {
        if (!selectedSource) {
            return articles;
        }
        return articles.filter(a => a.Source === selectedSource);
    }, [articles, selectedSource]);

    const tokenStats = useMemo(() => {
        const withInput = filteredArticles.filter(a => a.InputTokens != null);
        const withOutput = filteredArticles.filter(a => a.OutputTokens != null);
        const withBoth = filteredArticles.filter(a => a.InputTokens != null && a.OutputTokens != null);
        if (withInput.length === 0 && withOutput.length === 0) return null;
        const avgInput = withInput.length > 0 ? withInput.reduce((s, a) => s + a.InputTokens!, 0) / withInput.length : null;
        const avgOutput = withOutput.length > 0 ? withOutput.reduce((s, a) => s + a.OutputTokens!, 0) / withOutput.length : null;
        const avgTotal = withBoth.length > 0 ? withBoth.reduce((s, a) => s + a.InputTokens! + a.OutputTokens!, 0) / withBoth.length : null;
        return { avgInput, avgOutput, avgTotal };
    }, [filteredArticles]);

    return (
        <div>
            {error && <p>{error}</p>}
            <div>
                {tokenStats && (
                    <p style={{ textAlign: 'center', fontSize: '0.8em', color: '#888', marginBottom: '1vh', marginTop: '1vh' }}>
                        Avg token usage per article:
                        {tokenStats.avgInput != null && <> &nbsp;In: {Math.round(tokenStats.avgInput).toLocaleString()}</>}
                        {tokenStats.avgInput != null && tokenStats.avgOutput != null && <> &nbsp;·&nbsp; </>}
                        {tokenStats.avgOutput != null && <>Out: {Math.round(tokenStats.avgOutput).toLocaleString()}</>}
                        {tokenStats.avgTotal != null && <> &nbsp;·&nbsp; Total: {Math.round(tokenStats.avgTotal).toLocaleString()}</>}
                    </p>
                )}
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center', margin: 'auto', marginBottom: '3vh', marginTop: '3vh', width: '80%' }}>
                    <span style={{ fontWeight: 600 }}>Filter by source:</span>
                    <Chip
                        selected={!selectedSource}
                        onClick={() => setSelectedSource(null)}
                        size={'large'}
                        fillMode={'outline'}
                        themeColor={'base'}
                    >
                        All
                    </Chip>
                    {sources.map(src => (
                        <Chip
                            key={src}
                            selected={selectedSource === src}
                            onClick={() => setSelectedSource(src)}
                            size={'large'}
                            fillMode={'outline'}
                            themeColor={'base'}
                        >
                            {src}
                        </Chip>
                    ))}
                </div>
                <hr style={{ marginBottom: '3vh' }} />
                {filteredArticles.map((article, index) => (
                    <div key={`${article.Link}-${index}`}>
                        <h2 style={{ marginBottom: '1vh', marginTop: '1vh' }}>{article.Title}</h2>
                        <p style={{ marginBottom: '1.5vh', marginTop: '0vh' }}>{article.Summary}</p>
                        <div style={{ margin: '0 auto', marginBottom: '1.5vh' }} >
                            <Button
                                themeColor={'tertiary'}
                                fillMode={'outline'}
                                style={{ marginRight: 5 }}
                                onClick={() => window.open(article.Link, "_blank", "noopener,noreferrer")}>
                                Read the article at {article.Source} <span>&#x1F5D7;</span>
                            </Button>
                            <Button
                                fillMode={'outline'}
                                onClick={() => toggleChat(index)}
                                style={{ cursor: 'pointer' }}>
                                AIChat
                                <span style={{ marginRight: '5px' }}>&#x1F4AC;</span>
                            </Button>
                            <Button
                                fillMode={feedback[article.Link] === true ? 'solid' : 'outline'}
                                themeColor={feedback[article.Link] === true ? 'success' : 'base'}
                                style={{ marginLeft: 5, cursor: 'pointer' }}
                                title="I liked this article"
                                onClick={() => {
                                    if (feedback[article.Link] === true) {
                                        handleFeedback(article, true);
                                    } else {
                                        setPendingFeedback({ article, isLiked: true });
                                        setFeedbackReason('');
                                    }
                                }}>
                                👍
                            </Button>
                            <Button
                                fillMode={feedback[article.Link] === false ? 'solid' : 'outline'}
                                themeColor={feedback[article.Link] === false ? 'error' : 'base'}
                                style={{ marginLeft: 5, cursor: 'pointer' }}
                                title="I didn't like this article"
                                onClick={() => {
                                    if (feedback[article.Link] === false) {
                                        handleFeedback(article, false);
                                    } else {
                                        setPendingFeedback({ article, isLiked: false });
                                        setFeedbackReason('');
                                    }
                                }}>
                                👎
                            </Button>
                            {(article.InputTokens != null || article.OutputTokens != null) && (
                                <p style={{ textAlign: 'center', fontSize: '0.72em', color: '#888', marginTop: 0, marginBottom: '1.5vh' }}>
                                    {(article.InputTokens != null || article.OutputTokens != null) && <>Token usage: </>}
                                    {article.InputTokens != null && <>In: {article.InputTokens.toLocaleString()}</>}
                                    {article.InputTokens != null && article.OutputTokens != null && <> &nbsp;·&nbsp; </>}
                                    {article.OutputTokens != null && <>Out: {article.OutputTokens.toLocaleString()}</>}
                                    {article.InputTokens != null && article.OutputTokens != null && (
                                        <> &nbsp;·&nbsp; Total: {(article.InputTokens + article.OutputTokens).toLocaleString()}</>
                                    )}
                                </p>
                            )}
                        </div>
                        {selectedArticleIndex === index && (
                            <div style={{ height: '500px', margin: '10px auto', border: '1px solid #ccc' }}>
                                <ChatComponent
                                    article={article}
                                />
                            </div>
                        )}
                        <hr style={{ width: '60%', marginRight: '0 auto', marginLeft: '0 auto', marginTop: 5 }} />
                    </div>
                ))}
                {isLoading && <p>Loading articles{'.'.repeat(dots)}</p>}
                {!isLoading && filteredArticles.length === 0 && !error && <p>No articles found.</p>}
            </div>
            {pendingFeedback && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
                    <div style={{ background: isDark ? '#282c34' : 'white', color: isDark ? 'rgba(255,255,255,0.87)' : '#213547', padding: 24, borderRadius: 8, width: 420, boxShadow: '0 4px 24px rgba(0,0,0,0.4)', border: isDark ? '1px solid #444' : '1px solid #ccc' }}>
                        <h3 style={{ marginTop: 0 }}>
                            {pendingFeedback.isLiked ? '👍 Why did you like this?' : '👎 Why didn\'t you like this?'}
                        </h3>
                        <p style={{ fontSize: 13, color: isDark ? '#aaa' : '#555', marginTop: 0 }}>{pendingFeedback.article.Title}</p>
                        <TextArea
                            value={feedbackReason}
                            onChange={e => setFeedbackReason(e.value)}
                            rows={4}
                            placeholder="Enter your reason…"
                            style={{ width: '100%' }}
                        />
                        <div style={{ marginTop: 16, display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                            <Button fillMode={'outline'} onClick={() => setPendingFeedback(null)}>Cancel</Button>
                            <Button
                                themeColor={'primary'}
                                disabled={!feedbackReason.trim()}
                                onClick={async () => {
                                    await handleFeedback(pendingFeedback.article, pendingFeedback.isLiked, feedbackReason.trim());
                                    setPendingFeedback(null);
                                    setFeedbackReason('');
                                }}>
                                Submit
                            </Button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default NewsComponent;