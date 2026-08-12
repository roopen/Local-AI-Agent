import React, { useState } from 'react';
import PromptSettingsComponent from './PromptSettingsComponent';
import AuthenticationSettingsComponent from './AuthenticationSettingsComponent';
import FeedSettingsComponent from './FeedSettingsComponent';
import LlmSettingsComponent from './LlmSettingsComponent';

interface SettingsComponentProps {
    onSave?: () => Promise<void>;
}

type SettingsTab = 'prompt' | 'llm' | 'feeds' | 'auth';

const SettingsComponent: React.FC<SettingsComponentProps> = ({ onSave }) => {
    const [activeTab, setActiveTab] = useState<SettingsTab>('prompt');

    return (
        <div style={{ backgroundColor: '#121214', color: 'var(--foreground)', padding: '20px', borderRadius: 8 }}>
            <h1 className="settings-page-title">Settings</h1>

            <div style={{ marginBottom: '20px', borderBottom: '1px solid var(--border)', display: 'flex', gap: 0 }}>
                <button
                    className={`settings-tab${activeTab === 'llm' ? ' active' : ''}`}
                    onClick={() => setActiveTab('llm')}>
                    LLM API
                </button>
                <button
                    className={`settings-tab${activeTab === 'prompt' ? ' active' : ''}`}
                    onClick={() => setActiveTab('prompt')}>
                    Prompts
                </button>
                <button
                    className={`settings-tab${activeTab === 'feeds' ? ' active' : ''}`}
                    onClick={() => setActiveTab('feeds')}>
                    Feeds
                </button>
                <button
                    className={`settings-tab${activeTab === 'auth' ? ' active' : ''}`}
                    onClick={() => setActiveTab('auth')}>
                    Authentication
                </button>
            </div>

            {activeTab === 'prompt' && <PromptSettingsComponent onSave={onSave} />}
            {activeTab === 'llm' && <LlmSettingsComponent onSave={onSave} />}
            {activeTab === 'feeds' && <FeedSettingsComponent />}
            {activeTab === 'auth' && <AuthenticationSettingsComponent />}
        </div>
    );
};

export default SettingsComponent;
