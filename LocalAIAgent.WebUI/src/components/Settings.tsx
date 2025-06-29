import React from 'react';

export const SettingsPageUrl = () => { return '/settings' };

const Settings: React.FC = () => {
    return (
        <div>
            <h1>Settings</h1>
            <p>This is the settings page.</p>
        </div>
    );
};

export default Settings;