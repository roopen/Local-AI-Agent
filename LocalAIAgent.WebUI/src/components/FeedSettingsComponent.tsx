import React, { useEffect, useState, useCallback } from 'react';
import { Switch } from '@progress/kendo-react-inputs';
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
        const newLanguage = event.target.value;
        const updated = new UserSettings(
            settings.likes,
            settings.dislikes,
            settings.prompt,
            newLanguage,
            settings.disabledFeedSources
        );
        setSettings(updated);
        try {
            await userService.saveUserPreferences(updated);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Failed to save target language');
        }
    }, [settings, userService]);

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

            <h2>News sources</h2>
            {error && <div style={{ color: '#ff6b6b', marginBottom: '12px' }}>{error}</div>}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                {feeds.map(feed => (
                    <div
                        key={feed.clientName}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            padding: '8px 12px',
                            backgroundColor: '#1e1e22',
                            borderRadius: '4px',
                        }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
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
                        </div>
                        <Switch
                            checked={feed.enabled}
                            onChange={(e) => onToggle(feed, e.value)}
                        />
                    </div>
                ))}
            </div>
        </div>
    );
};

export default FeedSettingsComponent;
