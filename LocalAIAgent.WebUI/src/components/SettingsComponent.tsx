import React, { useState, useEffect, useCallback } from 'react';
import UserService from "../users/UserService";
import UserSettings from '../domain/UserSettings';

interface ChildComponentProps {
  onSave?: () => Promise<void>;
}

const SettingsComponent: React.FC<ChildComponentProps> = ({ onSave }) => {
    const [settings, setSettings] = useState<UserSettings>(new UserSettings());
    const [newLike, setNewLike] = useState('');
    const [newDislike, setNewDislike] = useState('');
    const [buttonText, setButtonText] = useState('Save Settings');
    const [isSaving, setIsSaving] = useState(false);
    const [textStyle, setTextStyle] = useState<React.CSSProperties>({ opacity: 1 });

    const userService = UserService.getInstance();

    useEffect(() => {
        const user = userService.getCurrentUser();
        if (user) {
            userService.getUserPreferences(user.id).then(loadedSettings => {
                if (loadedSettings) {
                    setSettings(loadedSettings);
                }
            });
        }
    // eslint-disable-next-line react-hooks/exhaustive-deps
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

    const handleSave = useCallback(() => {
        if (isSaving) return;
        userService.saveUserPreferences(settings).then(() => {
            onSave?.();
            setIsSaving(true);
        });
    }, [isSaving, settings, userService]);

    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if ((event.ctrlKey || event.metaKey) && event.key === 's') {
                event.preventDefault();
                handleSave();
            }
        };

        window.addEventListener('keydown', handleKeyDown);

        return () => {
            window.removeEventListener('keydown', handleKeyDown);
        };
    }, [handleSave]);

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
        <div style={{ backgroundColor: '#282c34', color: 'white', padding: '20px' }}>
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
                    style={{ width: 'calc(100% - 16px)', padding: '8px', boxSizing: 'border-box', backgroundColor: '#333', color: 'white', border: '1px solid #555' }}
                />
            </div>
            <div>
                <h2>Likes</h2>
                <ul style={{ listStyleType: 'none', padding: 0 }}>
                    {settings.likes.map(like => (
                        <li key={like} style={{ marginBottom: '5px' }}>
                            {like} <button onClick={() => removeLike(like)} style={{ marginLeft: '10px', backgroundColor: '#555', color: 'white', border: 'none', padding: '2px 5px', cursor: 'pointer' }}>Remove</button>
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
                    style={{ backgroundColor: '#333', color: 'white', border: '1px solid #555', padding: '5px' }}
                />
                <button onClick={addLike} style={{ marginLeft: '10px', backgroundColor: '#555', color: 'white', border: 'none', padding: '5px 10px', cursor: 'pointer' }}>Add Like</button>
            </div>
            <div>
                <h2>Dislikes</h2>
                <ul style={{ listStyleType: 'none', padding: 0 }}>
                    {settings.dislikes.map(dislike => (
                        <li key={dislike} style={{ marginBottom: '5px' }}>
                            {dislike} <button onClick={() => removeDislike(dislike)} style={{ marginLeft: '10px', backgroundColor: '#555', color: 'white', border: 'none', padding: '2px 5px', cursor: 'pointer' }}>Remove</button>
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
                    style={{ backgroundColor: '#333', color: 'white', border: '1px solid #555', padding: '5px' }}
                />
                <button onClick={addDislike} style={{ marginLeft: '10px', backgroundColor: '#555', color: 'white', border: 'none', padding: '5px 10px', cursor: 'pointer' }}>Add Dislike</button>
            </div>
            <button onClick={handleSave} disabled={isSaving} style={{ minWidth: '160px', textAlign: 'center', marginTop: '20px', backgroundColor: '#555', color: 'white', border: 'none', padding: '10px', cursor: 'pointer' }}>
                <span style={{...textStyle, display: 'inline-block'}}>{buttonText}</span>
            </button>
        </div>
    );
};

export default SettingsComponent;