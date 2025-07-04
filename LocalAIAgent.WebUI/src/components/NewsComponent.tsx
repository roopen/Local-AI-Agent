import React, { useState, useEffect } from 'react';
import NewsService from '../news/NewsService';
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
        const newsService = NewsService.getInstance();
        setIsLoading(true);
        
        newsService.getNews()
            .then(setArticles)
            .catch(err => {
                setError(`Error loading articles: ${err.message}`);
                console.error(err);
            })
            .finally(() => setIsLoading(false));
    }, []); 

    return (
        <div>
            {isLoading && <p>Loading articles{'.'.repeat(dots)}</p>}
            {error && <p>{error}</p>}
            {!isLoading && !error && (
                <div>
                    {articles.map((article, index) => (
                        <div key={index}>
                            <h2>{article.Title} <a href={article.Link} target="_blank" rel="noopener noreferrer">({article.Source})</a></h2>
                            <p>{article.Summary}</p>
                            <hr />
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
};

export default NewsComponent;