import { useState } from 'react';
import NewsArticle from '../domain/NewsArticle';
import ChatComponent from './ChatComponent';
import { Card, CardTitle, CardBody, CardActions } from '@progress/kendo-react-layout';
import { Button } from '@progress/kendo-react-buttons';

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

export interface ArticleCardProps {
    article: NewsArticle;
    feedback: Record<string, boolean>;
    isSelected: boolean;
    onToggleChat: () => void;
    onFeedbackClick: (isLiked: boolean) => void;
}

export default function ArticleCard({ article, feedback, isSelected, onToggleChat, onFeedbackClick }: ArticleCardProps) {
    const liked = feedback[article.Link] === true;
    const disliked = feedback[article.Link] === false;
    const [likeHovered, setLikeHovered] = useState(false);
    const [dislikeHovered, setDislikeHovered] = useState(false);
    return (
        <Card style={{ maxWidth: '80%', margin: 'auto', marginBottom: '1vh' }}>
            <CardBody style={{ background: '#121214' }}>
                <CardTitle>
                    <h2 style={{ marginBottom: '1vh', marginTop: '1vh' }}>{article.Title}</h2>
                </CardTitle>
                <p style={{ marginBottom: '1.5vh', marginTop: '0vh', color: 'rgb(136, 136, 136)' }}>{article.Summary}</p>
                <CardActions>
                    <div style={{ margin: '0 auto' }}>
                        <Button
                            themeColor={'info'}
                            fillMode={'outline'}
                            style={{ marginRight: 5 }}
                            onClick={() => window.open(article.Link, '_blank', 'noopener,noreferrer')}>
                            Read the article at {article.Source} <span>&#x1F5D7;</span>
                        </Button>
                        <Button fillMode={'outline'} onClick={onToggleChat} style={{ cursor: 'pointer' }}>
                            AIChat<span style={{ marginRight: '5px' }}>&#x1F4AC;</span>
                        </Button>
                        <span style={{ marginLeft: 5 }}>
                            <Button
                                title="I liked this article"
                                onClick={() => onFeedbackClick(true)}
                                onMouseEnter={() => setLikeHovered(true)}
                                onMouseLeave={() => setLikeHovered(false)}
                                style={{
                                    cursor: 'pointer',
                                    maxHeight: '30px',
                                    padding: '0.4em 0.75em',
                                    fontSize: '1em',
                                    border: '1px solid var(--border)',
                                    borderRight: 'none',
                                    borderRadius: '6px 0 0 6px',
                                    background: liked ? 'var(--chart-2)' : likeHovered ? 'var(--secondary)' : 'transparent',
                                    color: 'var(--foreground)',
                                    transition: 'background 0.15s',
                                }}>
                                👍
                            </Button>
                            <Button
                                title="I didn't like this article"
                                onClick={() => onFeedbackClick(false)}
                                onMouseEnter={() => setDislikeHovered(true)}
                                onMouseLeave={() => setDislikeHovered(false)}
                                style={{
                                    cursor: 'pointer',
                                    maxHeight: '30px',
                                    padding: '0.4em 0.75em',
                                    fontSize: '1em',
                                    border: '1px solid var(--border)',
                                    borderRadius: '0 6px 6px 0',
                                    background: disliked ? 'var(--destructive)' : dislikeHovered ? 'var(--secondary)' : 'transparent',
                                    color: 'var(--foreground)',
                                    transition: 'background 0.15s',
                                }}>
                                👎
                            </Button>
                        </span>
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
                </CardActions>
            </CardBody>
        </Card>
    );
}
