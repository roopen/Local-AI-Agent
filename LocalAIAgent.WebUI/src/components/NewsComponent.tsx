import React, { useState, useEffect } from 'react';
import NewsService from '../news/NewsService';
import type { NewsArticle } from '../domain/NewsArticle';

const NewsComponent: React.FC = () => {
    const [articles, setArticles] = useState<NewsArticle[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const newsService = new NewsService();
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
            <h1>News Page</h1>
            {isLoading && <p>Loading articles...</p>}
            {error && <p>{error}</p>}
            {!isLoading && !error && (
                <div>
                    {articles.map((article, index) => (
                        <div key={index}>
                            <h2><a href={article.link} target="_blank" rel="noopener noreferrer">{article.title}</a></h2>
                            <p>{article.content}</p>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
};

export default NewsComponent;