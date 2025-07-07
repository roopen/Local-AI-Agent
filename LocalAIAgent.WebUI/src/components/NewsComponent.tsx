import React, { useState, useEffect } from 'react';
import { NewsStreamClient } from '../clients/NewsClient';
import NewsArticle from '../domain/NewsArticle';

const NewsComponent: React.FC = () => {
    const [articles, setArticles] = useState<NewsArticle[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [dots, setDots] = useState(1);

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
                        <h2>{article.Title} <a href={article.Link} target="_blank" rel="noopener noreferrer">({article.Source})</a></h2>
                        <p>{article.Summary}</p>
                        <hr style={{ width: '40%', margin: '0 auto' }} />
                    </div>
                ))}
                {isLoading && <p>Loading articles{'.'.repeat(dots)}</p>}
                {!isLoading && articles.length === 0 && !error && <p>No articles found.</p>}
            </div>
        </div>
    );
};

export default NewsComponent;