import NewsArticle from '../domain/NewsArticle';
import { Card, CardTitle, CardBody, CardActions } from '@progress/kendo-react-layout';
import { Button } from '@progress/kendo-react-buttons';


function TokenTotal({ inputTokens, outputTokens }: { inputTokens: number | null; outputTokens: number | null }) {
    if (inputTokens == null || outputTokens == null) return null;
    return <> &nbsp;·&nbsp; Total: {(inputTokens + outputTokens).toLocaleString()}</>;
}

function ArticleTokenUsage({ inputTokens, outputTokens }: { inputTokens: number | null; outputTokens: number | null }) {
    if (inputTokens == null && outputTokens == null) return null;
    return (
        <p className="article-token-usage">
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
    onFeedbackClick: (isLiked: boolean) => void;
}

export default function ArticleCard({ article, feedback, onFeedbackClick }: ArticleCardProps) {
    const liked = feedback[article.Link] === true;
    const disliked = feedback[article.Link] === false;
    return (
        <Card className="article-card">
            <CardBody className="article-card-body">
                <CardTitle>
                    <h2 className="article-card-title">{article.Title}</h2>
                </CardTitle>
                <p className="article-card-summary">{article.Summary}</p>
                <CardActions>
                    <div className="article-card-actions">
                        <a
                            href={article.Link}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="read-article-btn">
                            Read the article at {article.Source} <span>&#x1F5D7;</span>
                        </a>
                        <span className="article-card-feedback">
                            <Button
                                title="I liked this article"
                                onClick={() => onFeedbackClick(true)}
                                className={`feedback-btn feedback-btn--like${liked ? ' active' : ''}`}>
                                👍
                            </Button>
                            <Button
                                title="I didn't like this article"
                                onClick={() => onFeedbackClick(false)}
                                className={`feedback-btn feedback-btn--dislike${disliked ? ' active' : ''}`}>
                                👎
                            </Button>
                        </span>
                        {article.Reasoning && (
                            <p className="article-card-reasoning">
                                {article.Reasoning}
                            </p>
                        )}
                        <ArticleTokenUsage inputTokens={article.InputTokens} outputTokens={article.OutputTokens} />
                    </div>
                </CardActions>
            </CardBody>
        </Card>
    );
}
