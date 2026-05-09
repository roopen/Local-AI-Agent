import React, { useEffect, useState, useCallback } from 'react';
import { Switch, TextBox } from '@progress/kendo-react-inputs';
import { Button } from '@progress/kendo-react-buttons';
import { FeedsService } from '../clients/UserApiClient';
import type { FeedDto } from '../clients/UserApiClient';
import UserService from '../users/UserService';
import UserSettings from '../domain/UserSettings';

const FeedSettingsComponent: React.FC = () => {
    const userService = UserService.getInstance();
    const [feeds, setFeeds] = useState<FeedDto[]>([]);
    const [settings, setSettings] = useState<UserSettings | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // New-feed form state.
    const [newUrl, setNewUrl] = useState('');
    const [newName, setNewName] = useState('');
    const [newLanguage, setNewLanguage] = useState('en');
    const [adding, setAdding] = useState(false);

    const loadFeeds = useCallback(async () => {
        const user = userService.getCurrentUser();
        if (!user) return;
        try {
            const list = await FeedsService.getApiFeeds(parseInt(user.id, 10));
            setFeeds(list);
            const prefs = await userService.getUserPreferences(user.id);
            if (prefs) setSettings(prefs);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Failed to load feeds');
        } finally {
            setLoading(false);
        }
    }, [userService]);

    useEffect(() => {
        loadFeeds();
    }, [loadFeeds]);

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
        if (!user || !newUrl.trim() || !newName.trim()) return;
        setAdding(true);
        setError(null);
        try {
            const created = await FeedsService.postApiFeedsCustom({
                userId: parseInt(user.id, 10),
                url: newUrl.trim(),
                displayName: newName.trim(),
                language: newLanguage,
            });
            setFeeds(prev => [...prev, created]);
            setNewUrl('');
            setNewName('');
            setNewLanguage('en');
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Failed to add feed');
        } finally {
            setAdding(false);
        }
    }, [userService, newUrl, newName, newLanguage]);

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

    // Build the language dropdown from the union of feed languages plus English (always offered).
    const languageOptions = React.useMemo(() => {
        const seen = new Map<string, string>();
        seen.set('en', 'English');
        for (const feed of feeds) {
            const code = feed.language ?? '';
            const name = feed.languageName ?? code;
            if (code && !seen.has(code)) seen.set(code, name);
        }
        return [...seen.entries()].sort((a, b) => a[1].localeCompare(b[1]));
    }, [feeds]);

    if (loading) return <div>Loading feeds...</div>;

    const builtInFeeds = feeds.filter(f => !f.isCustom);
    const customFeeds = feeds.filter(f => f.isCustom);

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

            {error && <div style={{ color: '#ff6b6b', marginBottom: '12px' }}>{error}</div>}

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
                    placeholder="RSS feed URL (https://...)"
                    value={newUrl}
                    onChange={(e) => setNewUrl(String(e.value ?? ''))}
                />
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
                <Button
                    themeColor="primary"
                    onClick={onAddCustom}
                    disabled={adding || !newUrl.trim() || !newName.trim()}>
                    {adding ? 'Adding...' : 'Add feed'}
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
            />
            {onDelete && (
                <Button fillMode="flat" onClick={onDelete} title="Remove feed">✕</Button>
            )}
        </div>
    </div>
);

export default FeedSettingsComponent;
