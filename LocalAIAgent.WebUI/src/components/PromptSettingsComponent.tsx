import React, { useState, useEffect, useCallback, useRef } from 'react';
import UserService from "../users/UserService";
import UserSettings from '../domain/UserSettings';

interface PromptSettingsProps {
    onSave?: () => Promise<void>;
}

const parseJsonStringArray = (value: string): string[] => {
    let parsed: unknown;
    try {
        parsed = JSON.parse(value);
    } catch {
        throw new Error('Paste a valid JSON array of quoted strings.');
    }

    if (!Array.isArray(parsed) || !parsed.every(item => typeof item === 'string')) {
        throw new Error('Every item in the pasted JSON array must be a string.');
    }

    const items = [...new Set(parsed.map(item => item.trim()).filter(Boolean))];
    if (items.length === 0) {
        throw new Error('The pasted JSON array does not contain any items.');
    }

    return items;
};

const PromptSettingsComponent: React.FC<PromptSettingsProps> = ({ onSave }) => {
    const [settings, setSettings] = useState<UserSettings>(new UserSettings());
    const [newLike, setNewLike] = useState('');
    const [newDislike, setNewDislike] = useState('');
    const [addingLike, setAddingLike] = useState(false);
    const [addingDislike, setAddingDislike] = useState(false);
    const [saveButtonText, setSaveButtonText] = useState('Save');
    const [isSaving, setIsSaving] = useState(false);

    const userService = UserService.getInstance();
    const likeInputRef = useRef<HTMLInputElement>(null);
    const dislikeInputRef = useRef<HTMLInputElement>(null);
    const lastSavedPromptRef = useRef<string>('');

    useEffect(() => {
        const user = userService.getCurrentUser();
        if (!user) return;
        userService.getUserPreferences().then(loaded => {
            if (loaded) {
                setSettings(loaded);
                lastSavedPromptRef.current = loaded.prompt || '';
            }
        });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    useEffect(() => {
        if (addingLike) likeInputRef.current?.focus();
    }, [addingLike]);

    useEffect(() => {
        if (addingDislike) dislikeInputRef.current?.focus();
    }, [addingDislike]);

    const persist = useCallback(async (next: UserSettings) => {
        setSettings(next);
        await userService.saveUserPreferences(next);
        lastSavedPromptRef.current = next.prompt || '';
        await onSave?.();
    }, [userService, onSave]);

    const handleSaveClick = useCallback(async () => {
        if (isSaving) return;
        setIsSaving(true);
        try {
            await persist(settings);
            setSaveButtonText('Saved');
        } catch {
            setSaveButtonText('Save failed');
        } finally {
            setTimeout(() => {
                setSaveButtonText('Save');
                setIsSaving(false);
            }, 1500);
        }
    }, [isSaving, persist, settings]);

    useEffect(() => {
        const onKeyDown = (event: KeyboardEvent) => {
            if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
                event.preventDefault();
                void handleSaveClick();
            }
        };
        window.addEventListener('keydown', onKeyDown);
        return () => window.removeEventListener('keydown', onKeyDown);
    }, [handleSaveClick]);

    const cloneWith = useCallback((overrides: Partial<UserSettings>): UserSettings => {
        return new UserSettings(
            overrides.likes ?? settings.likes,
            overrides.dislikes ?? settings.dislikes,
            overrides.prompt ?? settings.prompt,
            overrides.targetLanguage ?? settings.targetLanguage,
            overrides.disabledFeedSources ?? settings.disabledFeedSources,
        );
    }, [settings]);

    const commitLike = useCallback(async () => {
        const value = newLike.trim();
        if (!value) return;
        setNewLike('');
        if (settings.likes.includes(value)) return;
        await persist(cloneWith({ likes: [...settings.likes, value] }));
    }, [newLike, settings.likes, cloneWith, persist]);

    const commitDislike = useCallback(async () => {
        const value = newDislike.trim();
        if (!value) return;
        setNewDislike('');
        if (settings.dislikes.includes(value)) return;
        await persist(cloneWith({ dislikes: [...settings.dislikes, value] }));
    }, [newDislike, settings.dislikes, cloneWith, persist]);

    const pasteLikes = useCallback(async (items: string[]) => {
        const likes = [...new Set([...settings.likes, ...items])];
        await persist(cloneWith({ likes }));
    }, [settings.likes, cloneWith, persist]);

    const pasteDislikes = useCallback(async (items: string[]) => {
        const dislikes = [...new Set([...settings.dislikes, ...items])];
        await persist(cloneWith({ dislikes }));
    }, [settings.dislikes, cloneWith, persist]);

    const removeLike = useCallback((item: string) => {
        void persist(cloneWith({ likes: settings.likes.filter(l => l !== item) }));
    }, [settings.likes, cloneWith, persist]);

    const removeDislike = useCallback((item: string) => {
        void persist(cloneWith({ dislikes: settings.dislikes.filter(d => d !== item) }));
    }, [settings.dislikes, cloneWith, persist]);

    const onPromptChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
        setSettings(cloneWith({ prompt: e.target.value }));
    };

    const onPromptBlur = () => {
        if ((settings.prompt || '') === lastSavedPromptRef.current) return;
        void persist(settings);
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
                        onChange={onPromptChange}
                        onBlur={onPromptBlur}
                        placeholder="e.g. AI research papers, Rust language news, no crypto"
                    />
                    <span className="prompt-textarea-counter">{promptValue.length}</span>
                </div>
            </div>

            <div style={{ display: 'flex', gap: '24px', marginTop: '24px' }}>
                <TasteColumn
                    label="Likes"
                    items={settings.likes}
                    iconClass="taste-chip-icon--like"
                    iconSvg={<polyline points="20 6 9 17 4 12" />}
                    onRemove={removeLike}
                    adding={addingLike}
                    setAdding={setAddingLike}
                    inputValue={newLike}
                    setInputValue={setNewLike}
                    onCommit={commitLike}
                    onPasteItems={pasteLikes}
                    inputRef={likeInputRef}
                    addPlaceholder="Add a like…"
                />
                <TasteColumn
                    label="Dislikes"
                    items={settings.dislikes}
                    iconClass="taste-chip-icon--dislike"
                    iconSvg={(
                        <>
                            <line x1="18" y1="6" x2="6" y2="18" />
                            <line x1="6" y1="6" x2="18" y2="18" />
                        </>
                    )}
                    onRemove={removeDislike}
                    adding={addingDislike}
                    setAdding={setAddingDislike}
                    inputValue={newDislike}
                    setInputValue={setNewDislike}
                    onCommit={commitDislike}
                    onPasteItems={pasteDislikes}
                    inputRef={dislikeInputRef}
                    addPlaceholder="Add a dislike…"
                />
            </div>

            <div className="prompt-save-row">
                <button
                    type="button"
                    className="primary-button"
                    onClick={handleSaveClick}
                    disabled={isSaving}>
                    {saveButtonText}
                </button>
            </div>
        </div>
    );
};

interface TasteColumnProps {
    label: string;
    items: string[];
    iconClass: string;
    iconSvg: React.ReactNode;
    onRemove: (item: string) => void;
    adding: boolean;
    setAdding: React.Dispatch<React.SetStateAction<boolean>>;
    inputValue: string;
    setInputValue: React.Dispatch<React.SetStateAction<string>>;
    onCommit: () => void | Promise<void>;
    onPasteItems: (items: string[]) => Promise<void>;
    inputRef: React.RefObject<HTMLInputElement | null>;
    addPlaceholder: string;
}

const TasteColumn: React.FC<TasteColumnProps> = ({
    label, items, iconClass, iconSvg, onRemove,
    adding, setAdding, inputValue, setInputValue, onCommit, onPasteItems, inputRef, addPlaceholder,
}) => {
    const [pasteError, setPasteError] = useState<string | null>(null);

    const onInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            void onCommit();
        } else if (e.key === 'Escape') {
            e.preventDefault();
            setInputValue('');
            setAdding(false);
        }
    };

    const onInputPaste = (event: React.ClipboardEvent<HTMLInputElement>) => {
        const pastedValue = event.clipboardData.getData('text');
        if (!pastedValue.trimStart().startsWith('[')) return;

        event.preventDefault();
        try {
            const pastedItems = parseJsonStringArray(pastedValue);
            setPasteError(null);
            setInputValue('');
            setAdding(false);
            void onPasteItems(pastedItems).catch(() => {
                setPasteError('The pasted items could not be saved.');
            });
        } catch (error) {
            setPasteError(error instanceof Error ? error.message : 'The pasted JSON array is invalid.');
        }
    };

    return (
        <div style={{ flex: '1', minWidth: 0 }}>
            <h2 className="settings-section-title">{label} ({items.length})</h2>
            <p className="taste-paste-hint">Add one item, or paste a JSON string array.</p>
            <ul className="taste-chip-list">
                {items.map(item => (
                    <li key={item} className="taste-chip">
                        <svg className={`taste-chip-icon ${iconClass}`} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            {iconSvg}
                        </svg>
                        <span className="taste-chip-label">{item}</span>
                        <button
                            type="button"
                            className="taste-chip-x"
                            onClick={() => onRemove(item)}
                            aria-label={`Remove ${item}`}
                            title="Remove">
                            ✕
                        </button>
                    </li>
                ))}
                {adding ? (
                    <li className="taste-chip taste-chip--input">
                        <svg className={`taste-chip-icon ${iconClass}`} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            {iconSvg}
                        </svg>
                        <input
                            ref={inputRef}
                            className="taste-chip-input"
                            value={inputValue}
                            onChange={(e) => setInputValue(e.target.value)}
                            onKeyDown={onInputKeyDown}
                            onPaste={onInputPaste}
                            onBlur={() => {
                                setInputValue('');
                                setAdding(false);
                            }}
                            placeholder={addPlaceholder}
                        />
                    </li>
                ) : (
                    <li
                        className="taste-chip taste-chip--ghost"
                        role="button"
                        tabIndex={0}
                        aria-label={`Add ${label.toLowerCase()}`}
                        onClick={() => setAdding(true)}
                        onKeyDown={(e) => {
                            if (e.key === 'Enter' || e.key === ' ') {
                                e.preventDefault();
                                setAdding(true);
                            }
                        }}>
                        <svg className="taste-chip-icon taste-chip-icon--add" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                            <line x1="12" y1="5" x2="12" y2="19" />
                            <line x1="5" y1="12" x2="19" y2="12" />
                        </svg>
                        <span className="taste-chip-label">add</span>
                    </li>
                )}
            </ul>
            {pasteError && <p className="taste-paste-error" role="alert">{pasteError}</p>}
        </div>
    );
};

export default PromptSettingsComponent;
