import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { NewsStreamClient } from '../clients/NewsStreamingClient';
import NewsArticle from '../domain/NewsArticle';
import FeedbackModal from './FeedbackModal';
import { Chip } from '@progress/kendo-react-buttons';
import { Card, CardBody } from '@progress/kendo-react-layout';
import { NewsClient } from '../clients/NewsClient';
import UserService from '../users/UserService';
import ArticleCard from './ArticleCard';

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
        <Card style={{ maxWidth: '40%', margin: 'auto', marginBottom: '1vh', marginTop: '1vh' }}>
            <CardBody style={{ background: '#121214', textAlign: 'center', fontSize: '0.8em', padding: '0.75em 1em', color: '#fff' }}>
                <span style={{ color: '#A1A1AA' }}>Avg token usage per article:</span>
                {avgInput != null && <> &nbsp;<span style={{  }}>In:</span> <span style={{ fontWeight: 'bold' }}>{Math.round(avgInput).toLocaleString()}</span></>}
                {avgInput != null && avgOutput != null && <span style={{  }}> &nbsp;·&nbsp; </span>}
                {avgOutput != null && <><span style={{  }}>Out:</span> <span style={{ fontWeight: 'bold' }}>{Math.round(avgOutput).toLocaleString()}</span></>}
                {avgTotal != null && <><span style={{  }}> &nbsp;·&nbsp; Total:</span> <span style={{ fontWeight: 'bold' }}>{Math.round(avgTotal).toLocaleString()}</span></>}
            </CardBody>
        </Card>
    );
}

function ArticleStatusMessage({ isLoading, filteredCount, error, dots }: { isLoading: boolean; filteredCount: number; error: string | null; dots: number }) {
    if (isLoading) return <p>Loading articles{'.'.repeat(dots)}</p>;
    if (!isLoading && filteredCount === 0 && !error) return <p>No articles found.</p>;
    return null;
}

const NewsComponent: React.FC = () => {
    const [articles, setArticles] = useState<NewsArticle[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [dots, setDots] = useState(1);
    const [selectedSource, setSelectedSource] = useState<string | null>(null);
    const [selectedTopic, setSelectedTopic] = useState<string | null>(null);
    const [feedback, setFeedback] = useState<Record<string, boolean>>({});
    const [pendingFeedback, setPendingFeedback] = useState<{ article: NewsArticle; isLiked: boolean } | null>(null);
    const [isDark, setIsDark] = useState(() => window.matchMedia('(prefers-color-scheme: dark)').matches);
    const [correctedTopic, setCorrectedTopic] = useState('');

    useEffect(() => {
        const mq = window.matchMedia('(prefers-color-scheme: dark)');
        const handler = (e: MediaQueryListEvent) => setIsDark(e.matches);
        mq.addEventListener('change', handler);
        return () => mq.removeEventListener('change', handler);
    }, []);

    const handleFeedback = useCallback(async (article: NewsArticle, isLiked: boolean, reason?: string, correctedTopic?: string) => {
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

    const filteredArticles = useMemo(() => {
        return articles
            .filter(a => !selectedSource || a.Source === selectedSource)
            .filter(a => !selectedTopic || (a.Topic?.trim() ?? '') === selectedTopic);
    }, [articles, selectedSource, selectedTopic]);

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
        const topicMap = new Map<string, NewsArticle[]>();

        for (const article of filteredArticles) {
            const topicKey = article.Topic?.trim() ?? '';
            if (!topicMap.has(topicKey)) {
                topicMap.set(topicKey, []);
                topicOrder.push(topicKey);
            }
            topicMap.get(topicKey)!.push(article);
        }

        const sortedTopics = [
            ...topicOrder.filter(k => k !== ''),
            ...topicOrder.filter(k => k === '')
        ];

        return sortedTopics.map(topicKey => ({
            topic: topicKey || null,
            articles: topicMap.get(topicKey)!
        }));
    }, [filteredArticles]);

    return (
        <div>
            {error && <Chip style={{ marginTop: '2vh' }} themeColor={ 'error'} > { error }</Chip>}

            <div style={{margin: 'auto'}}>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center', margin: 'auto', marginBottom: '1.5vh', marginTop: '3vh', width: '80%' }}>
                    <span style={{ fontWeight: 600, color: '#A1A1AA' }}>Source:</span>
                    <Chip
                        selected={!selectedSource}
                        onClick={() => { setSelectedSource(null); setSelectedTopic(null); }}
                        size={'large'}
                        fillMode={'outline'}
                        themeColor={'info'}
                    >
                        All
                    </Chip>
                    {sources.map(src => (
                        <Chip
                            key={src}
                            selected={selectedSource === src}
                            onClick={() => { setSelectedSource(src); setSelectedTopic(null); }}
                            size={'large'}
                            fillMode={'outline'}
                            themeColor={'info'}
                        >
                            {src}
                        </Chip>
                    ))}
                </div>
                {topics.length > 0 && (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center', margin: 'auto', marginBottom: '1.5vh', width: '80%' }}>
                        <span style={{ fontWeight: 600, color: '#A1A1AA' }}>Topic:</span>
                        <Chip
                            selected={!selectedTopic}
                            onClick={() => setSelectedTopic(null)}
                            size={'large'}
                            fillMode={'outline'}
                            themeColor={'info'}
                        >
                            All
                        </Chip>
                        {topics.map(topic => (
                            <Chip
                                key={topic}
                                selected={selectedTopic === topic}
                                onClick={() => setSelectedTopic(topic)}
                                size={'large'}
                                fillMode={'outline'}
                                themeColor={'info'}
                            >
                                {topic}
                            </Chip>
                        ))}
                    </div>
                )}

                <div style={{ height: '1px', maxWidth: '80%', flex: 1, backgroundColor: '#27272A', margin: 'auto', marginBottom: '3vh' }}></div>

                {groupedArticles.map(({ topic, articles: groupArticles }) => (
                    <div key={topic ?? '__no_topic__'}>
                        {topic && (
                            <div style={{
                                display: 'flex', margin: 'auto', maxWidth: '80%', alignItems: 'center',
                                gap: '12px', color: '#2563EB', fontSize: '1.25em', fontWeight: 700, marginTop: '0.5vh',
                                marginBottom: '1.5vh', paddingLeft: '12px'
                            }}>
                                {topic.toUpperCase()}
                                <div style={{ height: '1px', flex: 1, backgroundColor: '#27272A', opacity: 0.5 }}></div>
                            </div>
                        )}
                        {groupArticles.map((article) => (
                            <ArticleCard
                                key={article.Link}
                                article={article}
                                feedback={feedback}
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
                <ArticleStatusMessage isLoading={isLoading} filteredCount={filteredArticles.length} error={error} dots={dots} />
            </div>

            {tokenStats && <TokenStatsBar avgInput={tokenStats.avgInput} avgOutput={tokenStats.avgOutput} avgTotal={tokenStats.avgTotal} />}

            {pendingFeedback && (
                <FeedbackModal
                            pendingFeedback={pendingFeedback}
                            correctedTopic={correctedTopic}
                            isDark={isDark}
                            onTopicChange={setCorrectedTopic}
                            onCancel={() => setPendingFeedback(null)}
                            onSubmit={async (reason) => {
                                await handleFeedback(pendingFeedback.article, pendingFeedback.isLiked, reason, correctedTopic);
                                setPendingFeedback(null);
                                setCorrectedTopic('');
                            }}
                        />
            )}
        </div>
    );
};

export default NewsComponent;