import React, { useState, useEffect } from 'react';
import type { IUserService } from '../users/IUserService';
import UserSettings from '../domain/UserSettings';

interface SettingsComponentProps {
    userService: IUserService;
}

const SettingsComponent: React.FC<SettingsComponentProps> = ({ userService }) => {
    const [settings, setSettings] = useState<UserSettings>(new UserSettings());
    const [newLike, setNewLike] = useState('');
    const [newDislike, setNewDislike] = useState('');
    const [buttonText, setButtonText] = useState('Save Settings');
    const [isSaving, setIsSaving] = useState(false);
    const [textStyle, setTextStyle] = useState<React.CSSProperties>({ opacity: 1 });

    useEffect(() => {
        const user = userService.getCurrentUser();
        if (user) {
            userService.getUserPreferences(user.id).then(loadedSettings => {
                if (loadedSettings) {
                    setSettings(loadedSettings);
                }
            });
        }
    }, []);

    useEffect(() => {
        if (!isSaving) return;

        setTextStyle({ opacity: 1 });
        setButtonText('Settings Saved!');

        const fadeOutTimer = setTimeout(() => {
            setTextStyle({ opacity: 0, transition: 'opacity 0.5s ease-out' });
        }, 1500);

        const fadeInTimer = setTimeout(() => {
            setButtonText('Save Settings');
            setTextStyle({ opacity: 1, transition: 'opacity 0.5s ease-in' });
        }, 2000);

        const endSaveTimer = setTimeout(() => {
            setIsSaving(false);
        }, 2500);

        return () => {
            clearTimeout(fadeOutTimer);
            clearTimeout(fadeInTimer);
            clearTimeout(endSaveTimer);
        };
    }, [isSaving]);

    const handleSave = () => {
        if (isSaving) return;
        userService.saveUserPreferences(settings).then(() => {
            setIsSaving(true);
        });
    };

    const addLike = () => {
        if (newLike) {
            const newSettings = new UserSettings(settings.likes, settings.dislikes, settings.prompt);
            newSettings.addLike(newLike);
            setSettings(newSettings);
            setNewLike('');
        }
    };

    const addDislike = () => {
        if (newDislike) {
            const newSettings = new UserSettings(settings.likes, settings.dislikes, settings.prompt);
            newSettings.addDislike(newDislike);
            setSettings(newSettings);
            setNewDislike('');
        }
    };

    const removeLike = (item: string) => {
        const newSettings = new UserSettings(settings.likes, settings.dislikes, settings.prompt);
        newSettings.removeLike(item);
        setSettings(newSettings);
    };

    const removeDislike = (item: string) => {
        const newSettings = new UserSettings(settings.likes, settings.dislikes, settings.prompt);
        newSettings.removeDislike(item);
        setSettings(newSettings);
    };

    return (
        <div>
            <h1>Settings</h1>
            <div>
                <h2>Prompt</h2>
                <textarea
                    value={settings.prompt || ''}
                    onChange={(e) => {
                        const newSettings = new UserSettings(settings.likes, settings.dislikes, e.target.value);
                        setSettings(newSettings);
                    }}
                    placeholder="System prompt for the AI"
                    rows={5}
                    style={{ width: 'calc(100% - 16px)', padding: '8px', boxSizing: 'border-box' }}
                />
            </div>
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
                    onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                            e.preventDefault();
                            addLike();
                        }
                    }}
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
                    onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                            e.preventDefault();
                            addDislike();
                        }
                    }}
                    placeholder="Add a new dislike"
                />
                <button onClick={addDislike}>Add Dislike</button>
            </div>
            <button onClick={handleSave} disabled={isSaving} style={{ minWidth: '160px', textAlign: 'center' }}>
                <span style={{...textStyle, display: 'inline-block'}}>{buttonText}</span>
            </button>
        </div>
    );
};

export default SettingsComponent;