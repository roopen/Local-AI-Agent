import React, { useState, useEffect, useCallback } from 'react';
import UserService from "../users/UserService";
import UserSettings from '../domain/UserSettings';
import { Button, Chip } from '@progress/kendo-react-buttons';
import { InputSeparator, InputSuffix, TextArea, TextBox } from '@progress/kendo-react-inputs';

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
        // eslint-disable-next-line react-hooks/exhaustive-deps
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
            <h1 style={{ marginTop: '0px', marginBottom: '10px' }}>Settings</h1>
            <div>
                <h2>Prompt</h2>
                <TextArea
                    autoSize={true}
                    rows={2}
                    value={settings.prompt || ''}
                    onChange={(e) => {
                        const newSettings = new UserSettings(settings.likes, settings.dislikes, e.target.value);
                        setSettings(newSettings);
                    }}
                    placeholder="System prompt for the AI"
                    style={{ width: 'calc(100% - 16px)', padding: '8px', boxSizing: 'border-box', backgroundColor: '#333', color: 'white', border: '1px solid #555' }}
                />
            </div>
            <div style={{ display: 'flex' }}>
                <div style={{ flex: '1' }}>
                    <h2>Likes</h2>
                    <TextBox
                        value={newLike}
                        onChange={(e) => setNewLike(e.target.value as string)}
                        onKeyDown={(e) => {
                            if (e.key === 'Enter') {
                                e.preventDefault();
                                addLike();
                            }
                        }}
                        placeholder="Add a new like"
                        suffix={() => (
                            <>
                                <InputSeparator />
                                <InputSuffix >
                                    <Button
                                        onClick={addLike}
                                        disabled={newLike.length === 0}
                                        themeColor='tertiary'
                                        fillMode={"flat"}
                                        rounded={null} >
                                        Add
                                    </Button>
                                </InputSuffix>
                            </>
                        )}
                        style={{ width: '90%' }}
                    />
                    <ul style={{ listStyleType: 'none', padding: 0 }}>
                        {settings.likes.map(like => (
                            <Chip
                                key={like}
                                removable
                                size={'large'}
                                onClick={() => removeLike(like)}
                                style={{ marginLeft: '10px', backgroundColor: '#555', color: 'white', border: 'none', padding: '2px 5px', cursor: 'pointer' }}
                            >
                                {like}
                            </Chip>
                        ))}
                    </ul>
                </div>
                <div style={{ flex: '1' }}>
                    <h2>Dislikes</h2>
                    <TextBox
                        value={newDislike}
                        onChange={(e) => setNewDislike(e.target.value as string)}
                        onKeyDown={(e) => {
                            if (e.key === 'Enter') {
                                e.preventDefault();
                                addDislike();
                            }
                        }}
                        placeholder="Add a new dislike"
                        suffix={() => (
                            <>
                                <InputSeparator />
                                <InputSuffix >
                                    <Button
                                        onClick={addDislike}
                                        disabled={newDislike.length === 0}
                                        themeColor='primary'
                                        fillMode={"flat"}
                                        rounded={null} >
                                        Add
                                    </Button>
                                </InputSuffix>
                            </>
                        )}
                        style={{ width: '90%' }}
                    />
                    <ul style={{ listStyleType: 'none', padding: 0 }}>
                        {settings.dislikes.map(dislike => (
                            <Chip
                                key={dislike}
                                removable
                                size={'large'}
                                onClick={() => removeDislike(dislike)}
                                style={{ marginLeft: '10px', backgroundColor: '#555', color: 'white', border: 'none', padding: '2px 5px', cursor: 'pointer' }}
                            >
                                {dislike}
                            </Chip>
                        ))}
                    </ul>
                </div>
            </div>
            <Button
                themeColor={'secondary'}
                onClick={handleSave}
                disabled={isSaving}
                style={{ minWidth: '160px', textAlign: 'center', marginTop: '20px', backgroundColor: '#555', color: 'white', border: 'none', padding: '10px', cursor: 'pointer' }}>
                <span style={{ ...textStyle, display: 'inline-block' }}>{buttonText}</span>
            </Button>
        </div>
    );
};

export default SettingsComponent;