import { useState } from 'react';
import NewsArticle from '../domain/NewsArticle';
import UserSettings from '../domain/UserSettings';
import { Button, Chip } from '@progress/kendo-react-buttons';
import { TextArea, TextBox } from '@progress/kendo-react-inputs';

export interface FeedbackModalProps {
    pendingFeedback: { article: NewsArticle; isLiked: boolean };
    correctedTopic: string;
    userSettings: UserSettings | null;
    isDark: boolean;
    onTopicChange: (value: string) => void;
    onCancel: () => void;
    onSubmit: (reason: string, selectedLikes: string[], selectedDislikes: string[]) => void;
}

interface ChipsSectionProps {
    likes: string[];
    dislikes: string[];
    selectedLikes: string[];
    selectedDislikes: string[];
    prefColor: string;
    onToggleLike: (like: string) => void;
    onToggleDislike: (dislike: string) => void;
}

function ChipsSection({ likes, dislikes, selectedLikes, selectedDislikes, prefColor, onToggleLike, onToggleDislike }: ChipsSectionProps) {
    return (
        <div style={{ marginBottom: 12 }}>
            {likes.length > 0 && (
                <div style={{ marginBottom: 8 }}>
                    <span style={{ fontSize: 12, fontWeight: 600, color: prefColor }}>Likes:</span>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 4 }}>
                        {likes.map(like => (
                            <Chip key={like} selected={selectedLikes.includes(like)} onClick={() => onToggleLike(like)}
                                fillMode={selectedLikes.includes(like) ? 'solid' : 'outline'} themeColor={'success'}>
                                {like}
                            </Chip>
                        ))}
                    </div>
                </div>
            )}
            {dislikes.length > 0 && (
                <div>
                    <span style={{ fontSize: 12, fontWeight: 600, color: prefColor }}>Dislikes:</span>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 4 }}>
                        {dislikes.map(dislike => (
                            <Chip key={dislike} selected={selectedDislikes.includes(dislike)} onClick={() => onToggleDislike(dislike)}
                                fillMode={selectedDislikes.includes(dislike) ? 'solid' : 'outline'} themeColor={'error'}>
                                {dislike}
                            </Chip>
                        ))}
                    </div>
                </div>
            )}
        </div>
    );
}

function buildReason(topic: string, selectedLikes: string[], selectedDislikes: string[], article: NewsArticle, isLiked: boolean): string {
    const likes = selectedLikes.length > 0 ? selectedLikes.join(', ') : 'None';
    const dislikes = selectedDislikes.length > 0 ? selectedDislikes.join(', ') : 'None';
    const result = article.Reasoning ?? (isLiked ? 'High' : 'Low');
    return `<|think|\nTopic: ${topic}\nLikes: ${likes}\nDislikes: ${dislikes}\nResult: *reason* → ${result}\n|end|>`;
}

function FeedbackModal({ pendingFeedback, correctedTopic, userSettings, isDark, onTopicChange, onCancel, onSubmit }: FeedbackModalProps) {
    const [reason, setReason] = useState(() =>
        buildReason(correctedTopic, [], [], pendingFeedback.article, pendingFeedback.isLiked)
    );
    const [selectedLikes, setSelectedLikes] = useState<string[]>([]);
    const [selectedDislikes, setSelectedDislikes] = useState<string[]>([]);

    const colors = isDark
        ? { background: '#282c34', text: 'rgba(255,255,255,0.87)', subtitle: '#aaa', prefs: '#aaa', border: '1px solid #444' }
        : { background: 'white', text: '#213547', subtitle: '#555', prefs: '#666', border: '1px solid #ccc' };

    const handleTopicChange = (newTopic: string) => {
        onTopicChange(newTopic);
        setReason(buildReason(newTopic, selectedLikes, selectedDislikes, pendingFeedback.article, pendingFeedback.isLiked));
    };

    const toggleLike = (like: string) => {
        const next = selectedLikes.includes(like) ? selectedLikes.filter(l => l !== like) : [...selectedLikes, like];
        setSelectedLikes(next);
        setReason(buildReason(correctedTopic, next, selectedDislikes, pendingFeedback.article, pendingFeedback.isLiked));
    };

    const toggleDislike = (dislike: string) => {
        const next = selectedDislikes.includes(dislike) ? selectedDislikes.filter(d => d !== dislike) : [...selectedDislikes, dislike];
        setSelectedDislikes(next);
        setReason(buildReason(correctedTopic, selectedLikes, next, pendingFeedback.article, pendingFeedback.isLiked));
    };

    return (
        <div style={{
            position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)',
            display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000
        }}>
            <div style={{ background: colors.background, color: colors.text, padding: 24, borderRadius: 8, width: 480, boxShadow: '0 4px 24px rgba(0,0,0,0.4)', border: colors.border }}>
                <h3 style={{ marginTop: 0 }}>
                    {pendingFeedback.isLiked ? '👍 Why did you like this?' : '👎 Why didn\'t you like this?'}
                </h3>
                <p style={{ fontSize: 13, color: colors.subtitle, marginTop: 0 }}>{pendingFeedback.article.Title}</p>
                <div style={{ marginBottom: 12 }}>
                    <label style={{ fontSize: 13, fontWeight: 600, display: 'block', marginBottom: 4 }}>Topic</label>
                    <TextBox
                        value={correctedTopic}
                        onChange={e => handleTopicChange(e.value as string)}
                        placeholder="Correct the topic if needed…"
                        style={{ width: '100%' }}
                    />
                </div>
                {userSettings && (
                    <ChipsSection
                        likes={userSettings.likes}
                        dislikes={userSettings.dislikes}
                        selectedLikes={selectedLikes}
                        selectedDislikes={selectedDislikes}
                        prefColor={colors.prefs}
                        onToggleLike={toggleLike}
                        onToggleDislike={toggleDislike}
                    />
                )}
                <TextArea
                    value={reason}
                    onChange={e => setReason(e.value)}
                    rows={6}
                    placeholder="Enter your reason…"
                    style={{ width: '100%' }}
                />
                <div style={{ marginTop: 16, display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                    <Button fillMode={'outline'} onClick={onCancel}>Cancel</Button>
                    <Button themeColor={'primary'} disabled={!reason.trim()} onClick={() => onSubmit(reason.trim(), selectedLikes, selectedDislikes)}>
                        Submit
                    </Button>
                </div>
            </div>
        </div>
    );
}

export default FeedbackModal;
