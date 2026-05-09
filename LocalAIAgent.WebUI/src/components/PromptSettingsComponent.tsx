import React, { useState, useEffect, useCallback } from 'react';
import UserService from "../users/UserService";
import UserSettings from '../domain/UserSettings';
import { Button } from '@progress/kendo-react-buttons';
import { InputSeparator, InputSuffix, TextBox } from '@progress/kendo-react-inputs';

interface PromptSettingsProps {
    onSave?: () => Promise<void>;
}

const PromptSettingsComponent: React.FC<PromptSettingsProps> = ({ onSave }) => {
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
            // Show "Saved!" state at the moment of completion; the effect-driven timers
            // then drive the fade out / restore.
            setTextStyle({ opacity: 1 });
            setButtonText('Settings Saved!');
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

    const promptValue = settings.prompt || '';

    return (
        <div>
            <div>
                <h2 className="settings-section-title">Prompt</h2>
                <p className="prompt-hint">
                    Describe what you want to see. The AI uses this to filter every article.
                </p>
                <div className="prompt-textarea-wrapper">
                    <textarea
                        className="prompt-textarea"
                        rows={4}
                        value={promptValue}
                        onChange={(e) => {
                            const newSettings = new UserSettings(settings.likes, settings.dislikes, e.target.value);
                            setSettings(newSettings);
                        }}
                        placeholder="e.g. AI research papers, Rust language news, no crypto"
                    />
                    <span className="prompt-textarea-counter">{promptValue.length}</span>
                </div>
            </div>
            <div style={{ display: 'flex', gap: '24px', marginTop: '24px' }}>
                <div style={{ flex: '1', minWidth: 0 }}>
                    <h2 className="settings-section-title">Likes ({settings.likes.length})</h2>
                    <TextBox
                        value={newLike}
                        onChange={(e) => setNewLike(e.target.value as string)}
                        onKeyDown={(e) => {
                            if (e.key === 'Enter') {
                                e.preventDefault();
                                addLike();
                            }
                        }}
                        placeholder="Add a like and press Enter"
                        suffix={() => (
                            <>
                                <InputSeparator />
                                <InputSuffix >
                                    <Button
                                        onClick={addLike}
                                        disabled={newLike.length === 0}
                                        themeColor='tertiary'
                                        fillMode={"flat"}
                                        rounded={undefined} >
                                        Add
                                    </Button>
                                </InputSuffix>
                            </>
                        )}
                        style={{ width: '100%' }}
                    />
                    <ul className="taste-chip-list">
                        {settings.likes.map(like => (
                            <li key={like} className="taste-chip">
                                <svg className="taste-chip-icon taste-chip-icon--like" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <polyline points="20 6 9 17 4 12" />
                                </svg>
                                <span className="taste-chip-label">{like}</span>
                                <button
                                    type="button"
                                    className="taste-chip-x"
                                    onClick={() => removeLike(like)}
                                    aria-label={`Remove ${like}`}
                                    title="Remove">
                                    ✕
                                </button>
                            </li>
                        ))}
                    </ul>
                </div>
                <div style={{ flex: '1', minWidth: 0 }}>
                    <h2 className="settings-section-title">Dislikes ({settings.dislikes.length})</h2>
                    <TextBox
                        value={newDislike}
                        onChange={(e) => setNewDislike(e.target.value as string)}
                        onKeyDown={(e) => {
                            if (e.key === 'Enter') {
                                e.preventDefault();
                                addDislike();
                            }
                        }}
                        placeholder="Add a dislike and press Enter"
                        suffix={() => (
                            <>
                                <InputSeparator />
                                <InputSuffix >
                                    <Button
                                        onClick={addDislike}
                                        disabled={newDislike.length === 0}
                                        themeColor='primary'
                                        fillMode={"flat"}
                                        rounded={undefined} >
                                        Add
                                    </Button>
                                </InputSuffix>
                            </>
                        )}
                        style={{ width: '100%' }}
                    />
                    <ul className="taste-chip-list">
                        {settings.dislikes.map(dislike => (
                            <li key={dislike} className="taste-chip">
                                <svg className="taste-chip-icon taste-chip-icon--dislike" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                    <line x1="18" y1="6" x2="6" y2="18" />
                                    <line x1="6" y1="6" x2="18" y2="18" />
                                </svg>
                                <span className="taste-chip-label">{dislike}</span>
                                <button
                                    type="button"
                                    className="taste-chip-x"
                                    onClick={() => removeDislike(dislike)}
                                    aria-label={`Remove ${dislike}`}
                                    title="Remove">
                                    ✕
                                </button>
                            </li>
                        ))}
                    </ul>
                </div>
            </div>
            <Button
                themeColor={'primary'}
                onClick={handleSave}
                disabled={isSaving}
                style={{ minWidth: '160px', textAlign: 'center', marginTop: '20px', padding: '10px', cursor: 'pointer' }}>
                <span style={{ ...textStyle, display: 'inline-block' }}>{buttonText}</span>
            </Button>
        </div>
    );
};

export default PromptSettingsComponent;
