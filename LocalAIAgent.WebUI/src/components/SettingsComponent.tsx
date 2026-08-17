import React, { useState } from 'react';
import PromptSettingsComponent from './PromptSettingsComponent';
import AuthenticationSettingsComponent from './AuthenticationSettingsComponent';
import FeedSettingsComponent from './FeedSettingsComponent';
import LlmSettingsComponent from './LlmSettingsComponent';

interface SettingsComponentProps {
    onSave?: () => Promise<void>;
    initialTab?: SettingsTab;
    initialLlmError?: string | null;
}

export type SettingsTab = 'prompt' | 'llm' | 'feeds' | 'auth';

const settingsTabs: { id: SettingsTab; label: string }[] = [
    { id: 'llm', label: 'LLM API' },
    { id: 'prompt', label: 'Prompts' },
    { id: 'feeds', label: 'Feeds' },
    { id: 'auth', label: 'Authentication' },
];

const SettingsComponent: React.FC<SettingsComponentProps> = ({
    onSave,
    initialTab = 'prompt',
    initialLlmError,
}) => {
    const [activeTab, setActiveTab] = useState<SettingsTab>(initialTab);
    const tabContent: Record<SettingsTab, React.ReactNode> = {
        prompt: <PromptSettingsComponent onSave={onSave} />,
        llm: <LlmSettingsComponent onSave={onSave} initialError={initialLlmError} />,
        feeds: <FeedSettingsComponent />,
        auth: <AuthenticationSettingsComponent />,
    };

    return (
        <div style={{ backgroundColor: '#121214', color: 'var(--foreground)', padding: '20px', borderRadius: 8 }}>
            <h1 className="settings-page-title">Settings</h1>

            <div style={{ marginBottom: '20px', borderBottom: '1px solid var(--border)', display: 'flex', gap: 0 }}>
                {settingsTabs.map(tab => (
                    <button
                        key={tab.id}
                        className={`settings-tab${activeTab === tab.id ? ' active' : ''}`}
                        onClick={() => setActiveTab(tab.id)}>
                        {tab.label}
                    </button>
                ))}
            </div>

            {tabContent[activeTab]}
        </div>
    );
};

export default SettingsComponent;
