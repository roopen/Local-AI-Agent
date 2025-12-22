import React, { useState, useEffect } from 'react';
import { NewsStreamClient } from '../clients/NewsClient';
import NewsArticle from '../domain/NewsArticle';
import ChatComponent from './ChatComponent';
import { Button } from '@progress/kendo-react-buttons';

const NewsComponent: React.FC = () => {
    const [articles, setArticles] = useState<NewsArticle[]>([]);
    const [selectedArticleIndex, setSelectedArticleIndex] = useState<number | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [dots, setDots] = useState(1);

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
        const newsStreamClient = NewsStreamClient.getInstance();

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

    return (
        <div>
            {error && <p>{error}</p>}
            <div>
                <div>
                </div>
                <hr style={{ marginBottom: '3vh' }} />
                {articles.map((article, index) => (
                    <div key={index}>
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
                                <ChatComponent initialMessage={`I have a news article I'd like to talk about.\nNewsTitle: ${article.Title}\nNewsSummary: ${article.Summary}\nPublished: ${article.PublishDate}\nLink: ${article.Link}`} />
                            </div>
                        )}
                        <hr style={{ width: '60%', marginRight: '0 auto', marginLeft: '0 auto', marginTop: 5 }} />
                    </div>
                ))}
                {isLoading && <p>Loading articles{'.'.repeat(dots)}</p>}
                {!isLoading && articles.length === 0 && !error && <p>No articles found.</p>}
            </div>
        </div>
    );
};

export default NewsComponent;