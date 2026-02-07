import React, { useState } from 'react';
import PromptSettingsComponent from './PromptSettingsComponent';
import AuthenticationSettingsComponent from './AuthenticationSettingsComponent';
import { Button, ButtonGroup } from '@progress/kendo-react-buttons';

interface SettingsComponentProps {
    onSave?: () => Promise<void>;
}

const SettingsComponent: React.FC<SettingsComponentProps> = ({ onSave }) => {
    const [activeTab, setActiveTab] = useState<'prompt' | 'auth'>('prompt');

    return (
        <div style={{ backgroundColor: '#282c34', color: 'white', padding: '20px' }}>
            <h1 style={{ marginTop: '0px', marginBottom: '10px' }}>Settings</h1>
            
            <div style={{ marginBottom: '20px', borderBottom: '1px solid #555', display: 'flex' }}>
                <ButtonGroup>
                 <Button
                    fillMode="flat"
                    themeColor={activeTab === 'prompt' ? 'primary' : 'base'}
                    onClick={() => setActiveTab('prompt')}
                    style={{ 
                        marginRight: '10px', 
                        borderBottom: activeTab === 'prompt' ? '2px solid #007bff' : '2px solid transparent',
                        color: activeTab === 'prompt' ? '#007bff' : 'white',
                        borderRadius: 0,
                        backgroundColor: 'transparent',
                        marginTop: '1vh',
                        marginBottom: '1vh'
                    }}
                >
                    Prompt Settings
                </Button>
                <Button
                    fillMode="flat"
                    themeColor={activeTab === 'auth' ? 'primary' : 'base'}
                    onClick={() => setActiveTab('auth')}
                    style={{ 
                        borderBottom: activeTab === 'auth' ? '2px solid #007bff' : '2px solid transparent',
                        color: activeTab === 'auth' ? '#007bff' : 'white',
                        borderRadius: 0,
                        backgroundColor: 'transparent',
                        marginTop: '1vh',
                        marginBottom: '1vh'
                    }}
                >
                    Authentication
                </Button>
                </ButtonGroup>
            </div>

            {activeTab === 'prompt' && <PromptSettingsComponent onSave={onSave} />}
            {activeTab === 'auth' && <AuthenticationSettingsComponent />}
        </div>
    );
};

export default SettingsComponent;