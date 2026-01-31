import React, { useState, useEffect, useMemo } from 'react';
import { NewsStreamClient } from '../clients/NewsStreamingClient';
import NewsArticle from '../domain/NewsArticle';
import ChatComponent from './ChatComponent';
import { Button, Chip } from '@progress/kendo-react-buttons';

const NewsComponent: React.FC = () => {
    const [articles, setArticles] = useState<NewsArticle[]>([]);
    const [selectedArticleIndex, setSelectedArticleIndex] = useState<number | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [dots, setDots] = useState(1);
    const [selectedSource, setSelectedSource] = useState<string | null>(null);
    const newsStreamClient = NewsStreamClient.getInstance();

    const toggleChat = (index: number) => {
        if (selectedArticleIndex === index) {
            setSelectedArticleIndex(null);
        } else {
            setSelectedArticleIndex(index);
        }
    };

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

    return (
        <div>
            {error && <p>{error}</p>}
            <div>
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
        </div>
    );
};

export default NewsComponent;