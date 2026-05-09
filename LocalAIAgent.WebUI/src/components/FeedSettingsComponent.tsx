import React, { useEffect, useState, useCallback } from 'react';
import { Switch, TextBox } from '@progress/kendo-react-inputs';
import { Button } from '@progress/kendo-react-buttons';
import { FeedsService } from '../clients/UserApiClient';
import type { FeedDto, LanguageOptionDto } from '../clients/UserApiClient';
import UserService from '../users/UserService';
import UserSettings from '../domain/UserSettings';
import axios from 'axios';

// eslint-disable-next-line complexity
const FeedSettingsComponent: React.FC = () => {
    const userService = UserService.getInstance();
    const [feeds, setFeeds] = useState<FeedDto[]>([]);
    const [allLanguages, setAllLanguages] = useState<LanguageOptionDto[]>([]);
    const [settings, setSettings] = useState<UserSettings | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [urlErrors, setUrlErrors] = useState<Record<string, string> | null>(null);
    const [successMessage, setSuccessMessage] = useState<string | null>(null);

    // New-feed form state.
    const [newUrls, setNewUrls] = useState<string[]>(['']);
    const [newName, setNewName] = useState('');
    const [newLanguage, setNewLanguage] = useState('en');
    const [adding, setAdding] = useState(false);

    useEffect(() => {
        let cancelled = false;
        // eslint-disable-next-line complexity
        (async () => {
            const user = userService.getCurrentUser();
            if (!user) return;
            try {
                const [list, languages] = await Promise.all([
                    FeedsService.getApiFeeds(parseInt(user.id, 10)),
                    FeedsService.getApiFeedsLanguages(),
                ]);
                if (cancelled) return;
                setFeeds(list);
                setAllLanguages(languages);
                const prefs = await userService.getUserPreferences(user.id);
                if (cancelled) return;
                if (prefs) setSettings(prefs);
            } catch (e) {
                if (cancelled) return;
                setError(e instanceof Error ? e.message : 'Failed to load feeds');
            } finally {
                if (!cancelled) setLoading(false);
            }
        })();
        return () => { cancelled = true; };
    }, [userService]);

    // Auto-dismiss the success message after a few seconds.
    useEffect(() => {
        if (!successMessage) return;
        const timer = setTimeout(() => setSuccessMessage(null), 3500);
        return () => clearTimeout(timer);
    }, [successMessage]);

    const onToggle = useCallback(async (feed: FeedDto, enabled: boolean) => {
        const user = userService.getCurrentUser();
        if (!user || !feed.clientName) return;
        const clientName = feed.clientName;

        // Optimistic UI update.
        setFeeds(prev => prev.map(f => f.clientName === clientName ? { ...f, enabled } : f));

        try {
            await FeedsService.postApiFeedsToggle({
                userId: parseInt(user.id, 10),
                clientName,
                enabled,
            });
        } catch (e) {
            // Revert on failure.
            setFeeds(prev => prev.map(f => f.clientName === clientName ? { ...f, enabled: !enabled } : f));
            setError(e instanceof Error ? e.message : 'Failed to update feed');
        }
    }, [userService]);

    const onLanguageChange = useCallback(async (event: React.ChangeEvent<HTMLSelectElement>) => {
        if (!settings) return;
        const newLang = event.target.value;
        const updated = new UserSettings(
            settings.likes,
            settings.dislikes,
            settings.prompt,
            newLang,
            settings.disabledFeedSources
        );
        setSettings(updated);
        try {
            await userService.saveUserPreferences(updated);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Failed to save target language');
        }
    }, [settings, userService]);

    const onAddCustom = useCallback(async () => {
        const user = userService.getCurrentUser();
        if (!user || !newName.trim()) return;
        const trimmedUrls = newUrls.map(u => u.trim()).filter(u => u.length > 0);
        if (trimmedUrls.length === 0) return;

        setAdding(true);
        setError(null);
        setUrlErrors(null);
        setSuccessMessage(null);

        try {
            const created = await FeedsService.postApiFeedsCustom({
                userId: parseInt(user.id, 10),
                urls: trimmedUrls,
                displayName: newName.trim(),
                language: newLanguage,
            });
            setFeeds(prev => [...prev, created]);
            setNewUrls(['']);
            setNewName('');
            setNewLanguage('en');
            setSuccessMessage(`✓ Added "${created.displayName}" — ${trimmedUrls.length} URL${trimmedUrls.length === 1 ? '' : 's'} verified.`);
        } catch (e) {
            // The API returns AddCustomFeedErrorDto on validation failure.
            if (axios.isAxiosError(e) && e.response?.status === 400 && e.response.data) {
                const body = e.response.data as { message?: string; urlErrors?: Record<string, string> };
                setError(body.message ?? 'Failed to add feed');
                if (body.urlErrors) setUrlErrors(body.urlErrors);
            } else {
                setError(e instanceof Error ? e.message : 'Failed to add feed');
            }
        } finally {
            setAdding(false);
        }
    }, [userService, newUrls, newName, newLanguage]);

    const onDeleteCustom = useCallback(async (feed: FeedDto) => {
        const user = userService.getCurrentUser();
        if (!user || feed.customFeedId == null) return;
        const customFeedId = feed.customFeedId;

        // Optimistic remove.
        const previous = feeds;
        setFeeds(prev => prev.filter(f => f.customFeedId !== customFeedId));

        try {
            await FeedsService.deleteApiFeedsCustom(customFeedId, parseInt(user.id, 10));
        } catch (e) {
            setFeeds(previous);
            setError(e instanceof Error ? e.message : 'Failed to delete feed');
        }
    }, [userService, feeds]);

    const setUrlAtIndex = (index: number, value: string) => {
        setNewUrls(prev => prev.map((u, i) => i === index ? value : u));
    };

    const addUrlInput = () => setNewUrls(prev => [...prev, '']);

    const removeUrlInput = (index: number) => {
        setNewUrls(prev => prev.length === 1 ? prev : prev.filter((_, i) => i !== index));
    };

    // The dropdown lists every ISO 639-1 / BCP-47 language the server recognises.
    // Falls back to a minimal {en} list during the initial load before the API responds.
    const languageOptions = React.useMemo<[string, string][]>(() => {
        if (allLanguages.length === 0) return [['en', 'English']];
        return allLanguages
            .filter((l): l is LanguageOptionDto & { code: string; name: string } =>
                Boolean(l.code) && Boolean(l.name))
            .map(l => [l.code, l.name] as [string, string]);
    }, [allLanguages]);

    if (loading) return <div>Loading feeds...</div>;

    const builtInFeeds = feeds.filter(f => !f.isCustom);
    const customFeeds = feeds.filter(f => f.isCustom);
    const canSubmit = newName.trim().length > 0 && newUrls.some(u => u.trim().length > 0);

    return (
        <div>
            <div style={{ marginBottom: '24px' }}>
                <h2>Translate articles into</h2>
                <select
                    value={settings?.targetLanguage || 'en'}
                    onChange={onLanguageChange}
                    style={{
                        padding: '8px',
                        backgroundColor: '#333',
                        color: 'white',
                        border: '1px solid #555',
                        borderRadius: '4px',
                        minWidth: '200px',
                    }}>
                    {languageOptions.map(([code, name]) => (
                        <option key={code} value={code}>{name}</option>
                    ))}
                </select>
            </div>

            {successMessage && (
                <div style={{ color: '#10b981', marginBottom: '12px', padding: '8px 12px', backgroundColor: 'rgba(16, 185, 129, 0.12)', borderRadius: '4px' }}>
                    {successMessage}
                </div>
            )}
            {error && (
                <div style={{ color: '#ff6b6b', marginBottom: '12px', padding: '8px 12px', backgroundColor: 'rgba(255, 107, 107, 0.08)', borderRadius: '4px' }}>
                    {error}
                    {urlErrors && (
                        <ul style={{ margin: '8px 0 0', paddingLeft: '20px' }}>
                            {Object.entries(urlErrors).map(([url, msg]) => (
                                <li key={url} style={{ fontSize: '0.85em' }}>
                                    <code style={{ wordBreak: 'break-all' }}>{url}</code>: {msg}
                                </li>
                            ))}
                        </ul>
                    )}
                </div>
            )}

            <h2>News sources</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                {builtInFeeds.map(feed => (
                    <FeedRow key={feed.clientName} feed={feed} onToggle={onToggle} />
                ))}
            </div>

            <h2 style={{ marginTop: '24px' }}>Your custom feeds</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                {customFeeds.length === 0 && (
                    <div style={{ color: '#888', fontSize: '0.9em' }}>No custom feeds yet — add one below.</div>
                )}
                {customFeeds.map(feed => (
                    <FeedRow
                        key={feed.clientName}
                        feed={feed}
                        onToggle={onToggle}
                        onDelete={() => onDeleteCustom(feed)}
                    />
                ))}
            </div>

            <div style={{ marginTop: '16px', display: 'flex', flexDirection: 'column', gap: '8px', padding: '12px', backgroundColor: '#1e1e22', borderRadius: '4px' }}>
                <h3 style={{ margin: 0 }}>Add a custom feed</h3>
                <TextBox
                    placeholder="Display name"
                    value={newName}
                    onChange={(e) => setNewName(String(e.value ?? ''))}
                />
                <select
                    value={newLanguage}
                    onChange={(e) => setNewLanguage(e.target.value)}
                    style={{
                        padding: '8px',
                        backgroundColor: '#333',
                        color: 'white',
                        border: '1px solid #555',
                        borderRadius: '4px',
                    }}>
                    {languageOptions.map(([code, name]) => (
                        <option key={code} value={code}>{name}</option>
                    ))}
                </select>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                    <span style={{ fontSize: '0.85em', color: '#aaa' }}>RSS URLs (one per line — useful for sources with multiple sub-feeds):</span>
                    {newUrls.map((url, index) => (
                        <div key={index} style={{ display: 'flex', gap: '6px' }}>
                            <div style={{ flex: 1 }}>
                                <TextBox
                                    placeholder="https://..."
                                    value={url}
                                    onChange={(e) => setUrlAtIndex(index, String(e.value ?? ''))}
                                />
                            </div>
                            {newUrls.length > 1 && (
                                <Button fillMode="flat" onClick={() => removeUrlInput(index)} title="Remove URL">✕</Button>
                            )}
                        </div>
                    ))}
                    <Button fillMode="flat" themeColor="tertiary" onClick={addUrlInput}>+ Add another URL</Button>
                </div>
                <Button
                    themeColor="primary"
                    onClick={onAddCustom}
                    disabled={adding || !canSubmit}>
                    {adding ? 'Validating feed...' : 'Add feed'}
                </Button>
            </div>
        </div>
    );
};

interface FeedRowProps {
    feed: FeedDto;
    onToggle: (feed: FeedDto, enabled: boolean) => void;
    onDelete?: () => void;
}

const FeedRow: React.FC<FeedRowProps> = ({ feed, onToggle, onDelete }) => (
    <div
        style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '8px 12px',
            backgroundColor: '#1e1e22',
            borderRadius: '4px',
        }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flex: 1, minWidth: 0 }}>
            <span style={{ fontWeight: 500 }}>{feed.displayName}</span>
            <span style={{
                fontSize: '0.8em',
                color: '#aaa',
                backgroundColor: '#2a2a30',
                padding: '2px 6px',
                borderRadius: '3px',
            }}>
                {feed.languageName}
            </span>
            {feed.urls && feed.urls.length > 1 && (
                <span style={{
                    fontSize: '0.8em',
                    color: '#aaa',
                    backgroundColor: '#2a2a30',
                    padding: '2px 6px',
                    borderRadius: '3px',
                }}>
                    {feed.urls.length} URLs
                </span>
            )}
            {feed.lastFetchErrorMessage && (
                <span style={{ fontSize: '0.8em', color: '#ff6b6b' }} title={feed.lastFetchErrorMessage}>
                    ⚠ fetch failed
                </span>
            )}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Switch
                checked={feed.enabled}
                onChange={(e) => onToggle(feed, e.value)}
                onLabel=""
                offLabel=""
            />
            {onDelete && (
                <Button fillMode="flat" onClick={onDelete} title="Remove feed">✕</Button>
            )}
        </div>
    </div>
);

export default FeedSettingsComponent;
