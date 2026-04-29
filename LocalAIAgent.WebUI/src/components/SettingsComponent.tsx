import React, { useState } from 'react';
import PromptSettingsComponent from './PromptSettingsComponent';
import AuthenticationSettingsComponent from './AuthenticationSettingsComponent';

interface SettingsComponentProps {
    onSave?: () => Promise<void>;
}

const SettingsComponent: React.FC<SettingsComponentProps> = ({ onSave }) => {
    const [activeTab, setActiveTab] = useState<'prompt' | 'auth'>('prompt');

    return (
        <div style={{ backgroundColor: '#121214', color: 'var(--foreground)', padding: '20px', borderRadius: 8 }}>
            <h1 style={{ marginTop: '0px', marginBottom: '16px', fontSize: '1.4em' }}>Settings</h1>

            <div style={{ marginBottom: '20px', borderBottom: '1px solid var(--border)', display: 'flex', gap: 0 }}>
                <button
                    className={`settings-tab${activeTab === 'prompt' ? ' active' : ''}`}
                    onClick={() => setActiveTab('prompt')}>
                    Prompt Settings
                </button>
                <button
                    className={`settings-tab${activeTab === 'auth' ? ' active' : ''}`}
                    onClick={() => setActiveTab('auth')}>
                    Authentication
                </button>
            </div>

            {activeTab === 'prompt' && <PromptSettingsComponent onSave={onSave} />}
            {activeTab === 'auth' && <AuthenticationSettingsComponent />}
        </div>
    );
};

export default SettingsComponent;