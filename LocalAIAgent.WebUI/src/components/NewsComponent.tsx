import React, { useState, useEffect } from 'react';
import { NewsStreamClient } from '../clients/NewsClient';
import NewsArticle from '../domain/NewsArticle';
import ChatComponent from './ChatComponent';

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
        };
    }, []);

    return (
        <div>
            {error && <p>{error}</p>}
            <div>
                <div>
                </div>
                <hr />
                {articles.map((article, index) => (
                    <div key={index}>
                        <h2>{article.Title}</h2>
                        <p>{article.Summary}</p>
                        <div style={{ display: 'flex', justifyContent: 'space-between', width: '35%', margin: '0 auto' }} >
                            <a style={{ marginRight: 5 }} href={article.Link} target="_blank" rel="noopener noreferrer">
                                Read the article at {article.Source} <span>&#x1F5D7;</span>
                            </a>
                            <a onClick={() => toggleChat(index)} style={{ cursor: 'pointer' }}>
                                AIChat
                                <span style={{ marginRight: '5px' }}>&#x1F4AC;</span>
                            </a>
                        </div>
                        {selectedArticleIndex === index && (
                            <div style={{ height: '500px', maxWidth: '800px', margin: '10px auto', border: '1px solid #ccc' }}>
                                <ChatComponent initialMessage={
                                    `How old is this article: ${article.Title} - ${article.Summary} - ${article.PublishDate}`} />
                            </div>
                        )}
                        <hr style={{ width: '40%', marginRight: '0 auto', marginLeft: '0 auto', marginTop: 5 }} />
                    </div>
                ))}
                {isLoading && <p>Loading articles{'.'.repeat(dots)}</p>}
                {!isLoading && articles.length === 0 && !error && <p>No articles found.</p>}
            </div>
        </div>
    );
};

export default NewsComponent;