import React, { useState } from 'react';
import PromptSettingsComponent from './PromptSettingsComponent';
import AuthenticationSettingsComponent from './AuthenticationSettingsComponent';
import { Button, ButtonGroup } from '@progress/kendo-react-buttons';

interface SettingsComponentProps {
    onSave?: () => Promise<void>;
}

interface TabButtonProps {
    isActive: boolean;
    onClick: () => void;
    children: React.ReactNode;
    style?: React.CSSProperties;
}

const TabButton: React.FC<TabButtonProps> = ({ isActive, onClick, children, style }) => (
    <Button
        fillMode="flat"
        themeColor={isActive ? 'primary' : 'base'}
        onClick={onClick}
        style={{
            ...style,
            borderBottom: isActive ? '2px solid #007bff' : '2px solid transparent',
            color: isActive ? '#007bff' : 'white',
            borderRadius: 0,
            backgroundColor: 'transparent',
            marginTop: '1vh',
            marginBottom: '1vh'
        }}
    >
        {children}
    </Button>
);

const SettingsComponent: React.FC<SettingsComponentProps> = ({ onSave }) => {
    const [activeTab, setActiveTab] = useState<'prompt' | 'auth'>('prompt');

    return (
        <div style={{ backgroundColor: '#282c34', color: 'white', padding: '20px' }}>
            <h1 style={{ marginTop: '0px', marginBottom: '10px' }}>Settings</h1>
            
            <div style={{ marginBottom: '20px', borderBottom: '1px solid #555', display: 'flex' }}>
                <ButtonGroup>
                    <TabButton 
                        isActive={activeTab === 'prompt'} 
                        onClick={() => setActiveTab('prompt')}
                        style={{ marginRight: '10px' }}
                    >
                        Prompt Settings
                    </TabButton>
                    <TabButton 
                        isActive={activeTab === 'auth'} 
                        onClick={() => setActiveTab('auth')}
                    >
                        Authentication
                    </TabButton>
                </ButtonGroup>
            </div>

            {activeTab === 'prompt' && <PromptSettingsComponent onSave={onSave} />}
            {activeTab === 'auth' && <AuthenticationSettingsComponent />}
        </div>
    );
};

export default SettingsComponent;