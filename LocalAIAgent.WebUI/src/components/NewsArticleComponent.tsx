import React from 'react';

export interface NewsArticleProps {
    title: string;
    summary: string;
    author?: string;
    publishedAt?: string;
    imageUrl?: string;
}

export const NewsArticleComponent: React.FC<NewsArticleProps> = ({
    title,
    summary,
    author,
    publishedAt,
    imageUrl
}) => (
    <article className="news-article">
        {imageUrl && (
            <img
                src={imageUrl}
                alt={title}
                className="news-article__image"
            />
        )}
        <header className="news-article__header">
            <h2 className="news-article__title">{title}</h2>
            {author && (
                <span className="news-article__author">
                    By {author}
                </span>
            )}
            {publishedAt && (
                <time
                    className="news-article__date"
                    dateTime={publishedAt}
                >
                    {new Date(publishedAt).toLocaleDateString()}
                </time>
            )}
        </header>
        <section className="news-article__summary">
            <p>{summary}</p>
        </section>
    </article>
);

export default NewsArticleComponent;