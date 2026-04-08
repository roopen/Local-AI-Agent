import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { NewsStreamClient } from '../clients/NewsStreamingClient';
import NewsArticle from '../domain/NewsArticle';
import ChatComponent from './ChatComponent';
import FeedbackModal from './FeedbackModal';
import { Button, Chip } from '@progress/kendo-react-buttons';
import { NewsClient } from '../clients/NewsClient';
import UserService from '../users/UserService';
import UserSettings from '../domain/UserSettings';

const newsClient = NewsClient.getInstance();
const userService = UserService.getInstance();
const newsStreamClient = NewsStreamClient.getInstance();

interface TokenStatsBarProps {
    avgInput: number | null;
    avgOutput: number | null;
    avgTotal: number | null;
}

function TokenStatsBar({ avgInput, avgOutput, avgTotal }: TokenStatsBarProps) {
    return (
        <p style={{ textAlign: 'center', fontSize: '0.8em', color: '#888', marginBottom: '1vh', marginTop: '1vh' }}>
            Avg token usage per article:
            {avgInput != null && <> &nbsp;In: {Math.round(avgInput).toLocaleString()}</>}
            {avgInput != null && avgOutput != null && <> &nbsp;·&nbsp; </>}
            {avgOutput != null && <>Out: {Math.round(avgOutput).toLocaleString()}</>}
            {avgTotal != null && <> &nbsp;·&nbsp; Total: {Math.round(avgTotal).toLocaleString()}</>}
        </p>
    );
}

function ArticleStatusMessage({ isLoading, filteredCount, error, dots }: { isLoading: boolean; filteredCount: number; error: string | null; dots: number }) {
    if (isLoading) return <p>Loading articles{'.'.repeat(dots)}</p>;
    if (!isLoading && filteredCount === 0 && !error) return <p>No articles found.</p>;
    return null;
}

function addArticleToMap(topicMap: Map<string, Map<string, NewsArticle[]>>, topicOrder: string[], article: NewsArticle) {
    const topicKey = article.Topic?.trim() ?? '';
    const eventKey = article.Event?.trim() ?? '';
    if (!topicMap.has(topicKey)) {
        topicMap.set(topicKey, new Map());
        topicOrder.push(topicKey);
    }
    const eventMap = topicMap.get(topicKey)!;
    if (!eventMap.has(eventKey)) {
        eventMap.set(eventKey, []);
    }
    eventMap.get(eventKey)!.push(article);
}

function TokenTotal({ inputTokens, outputTokens }: { inputTokens: number | null; outputTokens: number | null }) {
    if (inputTokens == null || outputTokens == null) return null;
    return <> &nbsp;·&nbsp; Total: {(inputTokens + outputTokens).toLocaleString()}</>;
}

function ArticleTokenUsage({ inputTokens, outputTokens }: { inputTokens: number | null; outputTokens: number | null }) {
    if (inputTokens == null && outputTokens == null) return null;
    return (
        <p style={{ textAlign: 'center', fontSize: '0.72em', color: '#888', marginTop: 0, marginBottom: '0.5vh' }}>
            Token usage:
            {inputTokens != null && <>In: {inputTokens.toLocaleString()}</>}
            {inputTokens != null && outputTokens != null && <> &nbsp;·&nbsp; </>}
            {outputTokens != null && <>Out: {outputTokens.toLocaleString()}</>}
            <TokenTotal inputTokens={inputTokens} outputTokens={outputTokens} />
        </p>
    );
}

interface ArticleCardProps {
    article: NewsArticle;
    feedback: Record<string, boolean>;
    isSelected: boolean;
    onToggleChat: () => void;
    onFeedbackClick: (isLiked: boolean) => void;
}

function ArticleCard({ article, feedback, isSelected, onToggleChat, onFeedbackClick }: ArticleCardProps) {
    const liked = feedback[article.Link] === true;
    const disliked = feedback[article.Link] === false;
    return (
        <div>
            <h2 style={{ marginBottom: '1vh', marginTop: '1vh' }}>{article.Title}</h2>
            <p style={{ marginBottom: '1.5vh', marginTop: '0vh' }}>{article.Summary}</p>
            <div style={{ margin: '0 auto', marginBottom: '1.5vh' }}>
                <Button
                    themeColor={'tertiary'}
                    fillMode={'outline'}
                    style={{ marginRight: 5 }}
                    onClick={() => window.open(article.Link, '_blank', 'noopener,noreferrer')}>
                    Read the article at {article.Source} <span>&#x1F5D7;</span>
                </Button>
                <Button fillMode={'outline'} onClick={onToggleChat} style={{ cursor: 'pointer' }}>
                    AIChat<span style={{ marginRight: '5px' }}>&#x1F4AC;</span>
                </Button>
                <Button
                    fillMode={liked ? 'solid' : 'outline'}
                    themeColor={liked ? 'success' : 'base'}
                    style={{ marginLeft: 5, cursor: 'pointer' }}
                    title="I liked this article"
                    onClick={() => onFeedbackClick(true)}>
                    👍
                </Button>
                <Button
                    fillMode={disliked ? 'solid' : 'outline'}
                    themeColor={disliked ? 'error' : 'base'}
                    style={{ marginLeft: 5, cursor: 'pointer' }}
                    title="I didn't like this article"
                    onClick={() => onFeedbackClick(false)}>
                    👎
                </Button>
                {article.Reasoning && (
                    <p style={{ textAlign: 'center', fontSize: '0.72em', color: '#888', marginTop: '0.5vh', marginBottom: '0.5vh' }}>
                        {article.Reasoning}
                    </p>
                )}
                <ArticleTokenUsage inputTokens={article.InputTokens} outputTokens={article.OutputTokens} />
            </div>
            {isSelected && (
                <div style={{ height: '500px', margin: '10px auto', border: '1px solid #ccc' }}>
                    <ChatComponent article={article} />
                </div>
            )}
            <hr style={{ width: '60%', marginRight: '0 auto', marginLeft: '0 auto', marginTop: 5 }} />
        </div>
    );
}

const NewsComponent: React.FC = () => {
    const [articles, setArticles] = useState<NewsArticle[]>([]);
    const [selectedArticleLink, setSelectedArticleLink] = useState<string | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [dots, setDots] = useState(1);
    const [selectedSource, setSelectedSource] = useState<string | null>(null);
    const [selectedTopic, setSelectedTopic] = useState<string | null>(null);
    const [selectedEvent, setSelectedEvent] = useState<string | null>(null);
    const [feedback, setFeedback] = useState<Record<string, boolean>>({});
    const [pendingFeedback, setPendingFeedback] = useState<{ article: NewsArticle; isLiked: boolean } | null>(null);
    const [isDark, setIsDark] = useState(() => window.matchMedia('(prefers-color-scheme: dark)').matches);
    const [correctedTopic, setCorrectedTopic] = useState('');
    const [userSettings, setUserSettings] = useState<UserSettings | null>(null);

    useEffect(() => {
        const user = userService.getCurrentUser();
        if (user) {
            userService.getUserPreferences(user.id)
                .then(prefs => setUserSettings(prefs))
                .catch(err => console.error('Failed to load user preferences:', err));
        }
    }, []);

    useEffect(() => {
        const mq = window.matchMedia('(prefers-color-scheme: dark)');
        const handler = (e: MediaQueryListEvent) => setIsDark(e.matches);
        mq.addEventListener('change', handler);
        return () => mq.removeEventListener('change', handler);
    }, []);

    const toggleChat = (link: string) => {
        setSelectedArticleLink(prev => prev === link ? null : link);
    };

    const handleFeedback = useCallback(async (article: NewsArticle, isLiked: boolean, reason?: string, correctedTopic?: string, selectedLikes?: string[], selectedDislikes?: string[]) => {
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
                articleTopic: correctedTopic ?? article.Topic ?? '',
                isLiked,
                reason,
                selectedLikes,
                selectedDislikes
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
            setIsLoading(newsStreamClient.isLoading);
        };

        const handleError = (err: Error) => {
            setError(`Error loading articles: ${err.message}`);
            console.error(err);
            setIsLoading(newsStreamClient.isLoading);
        };

        const handleLoadingChange = (loading: boolean) => {
            setIsLoading(loading);
        };

        console.log('Starting news stream...');
        newsStreamClient.start(handleNewArticle, handleStreamEnd, handleError, handleLoadingChange);

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

    const topics = useMemo(() => {
        const base = selectedSource ? articles.filter(a => a.Source === selectedSource) : articles;
        const set = new Set<string>();
        for (const a of base) {
            if (a.Topic?.trim()) set.add(a.Topic.trim());
        }
        return Array.from(set).sort((a, b) => a.localeCompare(b));
    }, [articles, selectedSource]);

    const events = useMemo(() => {
        const base = articles.filter(a =>
            (!selectedSource || a.Source === selectedSource) &&
            (!selectedTopic || (a.Topic?.trim() ?? '') === selectedTopic)
        );
        const set = new Set<string>();
        for (const a of base) {
            if (a.Event?.trim()) set.add(a.Event.trim());
        }
        return Array.from(set).sort((a, b) => a.localeCompare(b));
    }, [articles, selectedSource, selectedTopic]);

    const filteredArticles = useMemo(() => {
        return articles
            .filter(a => !selectedSource || a.Source === selectedSource)
            .filter(a => !selectedTopic || (a.Topic?.trim() ?? '') === selectedTopic)
            .filter(a => !selectedEvent || (a.Event?.trim() ?? '') === selectedEvent);
    }, [articles, selectedSource, selectedTopic, selectedEvent]);

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

    const groupedArticles = useMemo(() => {
        const topicOrder: string[] = [];
        const topicMap = new Map<string, Map<string, NewsArticle[]>>();

        for (const article of filteredArticles) {
            addArticleToMap(topicMap, topicOrder, article);
        }

        const sortedTopics = [
            ...topicOrder.filter(k => k !== ''),
            ...topicOrder.filter(k => k === '')
        ];

        return sortedTopics.map(topicKey => ({
            topic: topicKey || null,
            eventGroups: Array.from(topicMap.get(topicKey)!.entries()).map(([eventKey, arts]) => ({
                event: eventKey || null,
                articles: arts
            }))
        }));
    }, [filteredArticles]);

    return (
        <div>
            {error && <p>{error}</p>}
            <div>
                {tokenStats && <TokenStatsBar avgInput={tokenStats.avgInput} avgOutput={tokenStats.avgOutput} avgTotal={tokenStats.avgTotal} />}
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center', margin: 'auto', marginBottom: '1.5vh', marginTop: '3vh', width: '80%' }}>
                    <span style={{ fontWeight: 600 }}>Source:</span>
                    <Chip
                        selected={!selectedSource}
                        onClick={() => { setSelectedSource(null); setSelectedTopic(null); setSelectedEvent(null); }}
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
                            onClick={() => { setSelectedSource(src); setSelectedTopic(null); setSelectedEvent(null); }}
                            size={'large'}
                            fillMode={'outline'}
                            themeColor={'base'}
                        >
                            {src}
                        </Chip>
                    ))}
                </div>
                {topics.length > 0 && (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center', margin: 'auto', marginBottom: '1.5vh', width: '80%' }}>
                        <span style={{ fontWeight: 600 }}>Topic:</span>
                        <Chip
                            selected={!selectedTopic}
                            onClick={() => { setSelectedTopic(null); setSelectedEvent(null); }}
                            size={'large'}
                            fillMode={'outline'}
                            themeColor={'base'}
                        >
                            All
                        </Chip>
                        {topics.map(topic => (
                            <Chip
                                key={topic}
                                selected={selectedTopic === topic}
                                onClick={() => { setSelectedTopic(topic); setSelectedEvent(null); }}
                                size={'large'}
                                fillMode={'outline'}
                                themeColor={'base'}
                            >
                                {topic}
                            </Chip>
                        ))}
                    </div>
                )}
                {events.length > 0 && (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center', margin: 'auto', marginBottom: '3vh', width: '80%' }}>
                        <span style={{ fontWeight: 600 }}>Event:</span>
                        <Chip
                            selected={!selectedEvent}
                            onClick={() => setSelectedEvent(null)}
                            size={'large'}
                            fillMode={'outline'}
                            themeColor={'base'}
                        >
                            All
                        </Chip>
                        {events.map(event => (
                            <Chip
                                key={event}
                                selected={selectedEvent === event}
                                onClick={() => setSelectedEvent(event)}
                                size={'large'}
                                fillMode={'outline'}
                                themeColor={'base'}
                            >
                                {event}
                            </Chip>
                        ))}
                    </div>
                )}
                <hr style={{ marginBottom: '3vh' }} />
                {groupedArticles.map(({ topic, eventGroups }) => (
                    <div key={topic ?? '__no_topic__'}>
                        {topic && (
                            <div style={{ fontSize: '1.25em', fontWeight: 700, marginTop: '5vh', marginBottom: '1.5vh', borderLeft: '4px solid #888', paddingLeft: '12px' }}>
                                {topic.toUpperCase()}
                            </div>
                        )}
                        {eventGroups.map(({ event, articles: groupArticles }) => (
                            <div key={event ?? '__no_event__'}>
                                {event && (
                                    <div style={{ fontSize: '1.25em', fontWeight: 700, marginTop: '1.5vh', marginBottom: '1.5vh', paddingLeft: topic ? '16px' : '0', color: '#888' }}>
                                        {event.toUpperCase()}
                                    </div>
                                )}
                                {groupArticles.map((article) => (
                                    <ArticleCard
                                        key={article.Link}
                                        article={article}
                                        feedback={feedback}
                                        isSelected={selectedArticleLink === article.Link}
                                        onToggleChat={() => toggleChat(article.Link)}
                                        onFeedbackClick={(isLiked) => {
                                            if (feedback[article.Link] === isLiked) {
                                                handleFeedback(article, isLiked);
                                            } else {
                                                setPendingFeedback({ article, isLiked });
                                                setCorrectedTopic(article.Topic?.trim() ?? '');
                                            }
                                        }}
                                    />
                                ))}
                            </div>
                        ))}
                    </div>
                ))}
                <ArticleStatusMessage isLoading={isLoading} filteredCount={filteredArticles.length} error={error} dots={dots} />
            </div>
            {pendingFeedback && (
                <FeedbackModal
                            pendingFeedback={pendingFeedback}
                            correctedTopic={correctedTopic}
                            userSettings={userSettings}
                            isDark={isDark}
                            onTopicChange={setCorrectedTopic}
                            onCancel={() => setPendingFeedback(null)}
                            onSubmit={async (reason, selectedLikes, selectedDislikes) => {
                                await handleFeedback(pendingFeedback.article, pendingFeedback.isLiked, reason, correctedTopic, selectedLikes, selectedDislikes);
                                setPendingFeedback(null);
                                setCorrectedTopic('');
                            }}
                        />
            )}
        </div>
    );
};

export default NewsComponent;