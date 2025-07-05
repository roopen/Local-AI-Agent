import React, { useState, useEffect, useMemo } from 'react';
import NewsService from '../news/NewsService';
import NewsArticle from '../domain/NewsArticle';
import type { Relevancy } from '../domain/Relevancy';

const NewsComponent: React.FC = () => {
    const [articles, setArticles] = useState<NewsArticle[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [dots, setDots] = useState(1);
    const [selectedCategory, setSelectedCategory] = useState<string>('All');
    const [sortBy, setSortBy] = useState<string>('Relevancy');

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

    const allCategories = useMemo(() => {
        const categories = new Set<string>();
        articles.forEach(article => {
            article.Categories.forEach(category => {
                categories.add(category);
            });
        });
        return ['All', ...Array.from(categories)];
    }, [articles]);

    const sortedAndFilteredArticles = useMemo(() => {
        let filtered = articles;
        if (selectedCategory !== 'All') {
            filtered = articles.filter(article => article.Categories.includes(selectedCategory));
        }

        const sorted = [...filtered];

        if (sortBy === 'Relevancy') {
            const relevancyOrder: Relevancy[] = ['High', 'Medium', 'Low'];
            sorted.sort((a, b) => relevancyOrder.indexOf(a.Relevancy) - relevancyOrder.indexOf(b.Relevancy));
        } else if (sortBy === 'Category') {
            sorted.sort((a, b) => (a.Categories[0] || '').localeCompare(b.Categories[0] || ''));
        } else if (sortBy === 'Source') {
            sorted.sort((a, b) => a.Source.localeCompare(b.Source));
        }

        return sorted;
    }, [articles, selectedCategory, sortBy]);

    return (
        <div>
            {isLoading && <p>Loading articles{'.'.repeat(dots)}</p>}
            {error && <p>{error}</p>}
            {!isLoading && !error && (
                <div>
                    <div>
                        <label htmlFor="category-filter">Filter by category: </label>
                        <select id="category-filter" value={selectedCategory} onChange={e => setSelectedCategory(e.target.value)}>
                            {allCategories.map(category => (
                                <option key={category} value={category}>{category}</option>
                            ))}
                        </select>

                        <label htmlFor="sort-by">Sort by: </label>
                        <select id="sort-by" value={sortBy} onChange={e => setSortBy(e.target.value)}>
                            <option value="Relevancy">Relevancy</option>
                            <option value="Category">Category</option>
                            <option value="Source">Source</option>
                        </select>
                    </div>
                    <hr />
                    {sortedAndFilteredArticles.map((article, index) => (
                        <div key={index}>
                            <h2>{article.Title} <a href={article.Link} target="_blank" rel="noopener noreferrer">({article.Source})</a></h2>
                            <p>{article.Summary}</p>
                            <hr style={{ width: '40%', margin: '0 auto' }} />
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
};

export default NewsComponent;