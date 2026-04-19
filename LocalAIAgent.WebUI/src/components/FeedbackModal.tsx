import { useState } from 'react';
import NewsArticle from '../domain/NewsArticle';
import { Button } from '@progress/kendo-react-buttons';
import { TextArea, TextBox } from '@progress/kendo-react-inputs';

export interface FeedbackModalProps {
    pendingFeedback: { article: NewsArticle; isLiked: boolean };
    correctedTopic: string;
    isDark: boolean;
    onTopicChange: (value: string) => void;
    onCancel: () => void;
    onSubmit: (reason: string) => void;
}

function FeedbackModal({ pendingFeedback, correctedTopic, isDark, onTopicChange, onCancel, onSubmit }: FeedbackModalProps) {
    const [reason, setReason] = useState(() => pendingFeedback.article.Reasoning ?? '');

    const colors = isDark
        ? { background: '#282c34', text: 'rgba(255,255,255,0.87)', subtitle: '#aaa', border: '1px solid #444' }
        : { background: 'white', text: '#213547', subtitle: '#555', border: '1px solid #ccc' };

    const handleTopicChange = (newTopic: string) => {
        onTopicChange(newTopic);
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
                <TextArea
                    value={reason}
                    onChange={e => setReason(e.value)}
                    rows={6}
                    placeholder="Enter your reason…"
                    style={{ width: '100%' }}
                />
                <div style={{ marginTop: 16, display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                    <Button fillMode={'outline'} onClick={onCancel}>Cancel</Button>
                    <Button themeColor={'primary'} disabled={!reason.trim()} onClick={() => onSubmit(reason.trim())}>
                        Submit
                    </Button>
                </div>
            </div>
        </div>
    );
}

export default FeedbackModal;
