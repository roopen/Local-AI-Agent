import React, { useState, useEffect } from 'react';
import { userSettingsService } from '../services/UserSettingsService';
import UserSettings from '../domain/UserSettings';

export const SettingsPageUrl = () => { return '/settings' };

const Settings: React.FC = () => {
    const [settings, setSettings] = useState<UserSettings>(new UserSettings());
    const [newLike, setNewLike] = useState('');
    const [newDislike, setNewDislike] = useState('');

    useEffect(() => {
        const loadedSettings = userSettingsService.getSettings();
        setSettings(loadedSettings);
    }, []);

    const handleSave = () => {
        userSettingsService.saveSettings(settings);
        alert('Settings saved!');
    };

    const addLike = () => {
        if (newLike) {
            const newSettings = new UserSettings(settings.likes, settings.dislikes);
            newSettings.addLike(newLike);
            setSettings(newSettings);
            setNewLike('');
        }
    };

    const addDislike = () => {
        if (newDislike) {
            const newSettings = new UserSettings(settings.likes, settings.dislikes);
            newSettings.addDislike(newDislike);
            setSettings(newSettings);
            setNewDislike('');
        }
    };

    const removeLike = (item: string) => {
        const newSettings = new UserSettings(settings.likes, settings.dislikes);
        newSettings.removeLike(item);
        setSettings(newSettings);
    };

    const removeDislike = (item: string) => {
        const newSettings = new UserSettings(settings.likes, settings.dislikes);
        newSettings.removeDislike(item);
        setSettings(newSettings);
    };

    return (
        <div>
            <h1>Settings</h1>
            <div>
                <h2>Likes</h2>
                <ul>
                    {settings.likes.map(like => (
                        <li key={like}>
                            {like} <button onClick={() => removeLike(like)}>Remove</button>
                        </li>
                    ))}
                </ul>
                <input
                    type="text"
                    value={newLike}
                    onChange={(e) => setNewLike(e.target.value)}
                    placeholder="Add a new like"
                />
                <button onClick={addLike}>Add Like</button>
            </div>
            <div>
                <h2>Dislikes</h2>
                <ul>
                    {settings.dislikes.map(dislike => (
                        <li key={dislike}>
                            {dislike} <button onClick={() => removeDislike(dislike)}>Remove</button>
                        </li>
                    ))}
                </ul>
                <input
                    type="text"
                    value={newDislike}
                    onChange={(e) => setNewDislike(e.target.value)}
                    placeholder="Add a new dislike"
                />
                <button onClick={addDislike}>Add Dislike</button>
            </div>
            <button onClick={handleSave}>Save Settings</button>
        </div>
    );
};

export default Settings;