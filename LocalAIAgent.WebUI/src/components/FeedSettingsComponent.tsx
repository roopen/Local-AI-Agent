import React, { useEffect, useState, useCallback } from 'react';
import { Switch, TextBox } from '@progress/kendo-react-inputs';
import { Button } from '@progress/kendo-react-buttons';
import { FeedsService } from '../clients/UserApiClient';
import type { FeedDto, LanguageOptionDto } from '../clients/UserApiClient';
import UserService from '../users/UserService';
import UserSettings from '../domain/UserSettings';
import axios from 'axios';

interface AddFeedFailure {
    message: string;
    urlErrors?: Record<string, string>;
}

function extractAddFeedError(e: unknown): AddFeedFailure {
    if (axios.isAxiosError(e) && e.response?.status === 400 && e.response.data) {
        const body = e.response.data as { message?: string; urlErrors?: Record<string, string> };
        return { message: body.message ?? 'Failed to add feed', urlErrors: body.urlErrors };
    }
    return { message: e instanceof Error ? e.message : 'Failed to add feed' };
}

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

    // Built-in feed list state.
    const [searchQuery, setSearchQuery] = useState('');
    const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(new Set());
    const [bulkBusy, setBulkBusy] = useState(false);

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
                    FeedsService.getApiFeeds(),
                    FeedsService.getApiFeedsLanguages(),
                ]);
                if (cancelled) return;
                setFeeds(list);
                setAllLanguages(languages);
                const prefs = await userService.getUserPreferences();
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
                clientName,
                enabled,
            });
        } catch (e) {
            // Revert on failure.
            setFeeds(prev => prev.map(f => f.clientName === clientName ? { ...f, enabled: !enabled } : f));
            setError(e instanceof Error ? e.message : 'Failed to update feed');
        }
    }, [userService]);

    const persistDisabledList = useCallback(async (disabledFeedSources: string[]) => {
        if (!settings) return;
        const updated = new UserSettings(
            settings.likes,
            settings.dislikes,
            settings.prompt,
            settings.targetLanguage,
            disabledFeedSources,
        );
        setSettings(updated);
        await userService.saveUserPreferences(updated);
    }, [settings, userService]);

    const onEnableAllBuiltIn = useCallback(async () => {
        setBulkBusy(true);
        const previous = feeds;
        // Optimistic flip on built-ins only.
        setFeeds(prev => prev.map(f => f.isCustom ? f : { ...f, enabled: true }));
        try {
            await persistDisabledList([]);
        } catch (e) {
            setFeeds(previous);
            setError(e instanceof Error ? e.message : 'Failed to enable feeds');
        } finally {
            setBulkBusy(false);
        }
    }, [feeds, persistDisabledList]);

    const onDisableAllBuiltIn = useCallback(async () => {
        setBulkBusy(true);
        const previous = feeds;
        const builtInClientNames = feeds.filter(f => !f.isCustom && f.clientName).map(f => f.clientName as string);
        setFeeds(prev => prev.map(f => f.isCustom ? f : { ...f, enabled: false }));
        try {
            await persistDisabledList(builtInClientNames);
        } catch (e) {
            setFeeds(previous);
            setError(e instanceof Error ? e.message : 'Failed to disable feeds');
        } finally {
            setBulkBusy(false);
        }
    }, [feeds, persistDisabledList]);

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
        const trimmedName = newName.trim();
        const trimmedUrls = newUrls.map(u => u.trim()).filter(u => u.length > 0);
        if (!user || !trimmedName || trimmedUrls.length === 0) return;

        setAdding(true);
        setError(null);
        setUrlErrors(null);
        setSuccessMessage(null);

        try {
            const created = await FeedsService.postApiFeedsCustom({
                urls: trimmedUrls,
                displayName: trimmedName,
                language: newLanguage,
            });
            setFeeds(prev => [...prev, created]);
            setNewUrls(['']);
            setNewName('');
            setNewLanguage('en');
            const plural = trimmedUrls.length === 1 ? '' : 's';
            setSuccessMessage(`✓ Added "${created.displayName}" — ${trimmedUrls.length} URL${plural} verified.`);
        } catch (e) {
            const failure = extractAddFeedError(e);
            setError(failure.message);
            if (failure.urlErrors) setUrlErrors(failure.urlErrors);
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
            await FeedsService.deleteApiFeedsCustom(customFeedId);
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

    const toggleGroupCollapsed = (groupKey: string) => {
        setCollapsedGroups(prev => {
            const next = new Set(prev);
            if (next.has(groupKey)) next.delete(groupKey); else next.add(groupKey);
            return next;
        });
    };

    // The dropdown lists every ISO 639-1 / BCP-47 language the server recognises.
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

    // Filter built-ins by search query, then group by language name.
    const query = searchQuery.trim().toLowerCase();
    const filteredBuiltIns = query
        ? builtInFeeds.filter(f => (f.displayName ?? '').toLowerCase().includes(query))
        : builtInFeeds;

    const groupedByLanguage = new Map<string, FeedDto[]>();
    for (const feed of filteredBuiltIns) {
        const groupKey = feed.languageName ?? feed.language ?? 'Unknown';
        const list = groupedByLanguage.get(groupKey) ?? [];
        list.push(feed);
        groupedByLanguage.set(groupKey, list);
    }
    // Sort groups: English first, then alphabetical.
    const sortedGroups = [...groupedByLanguage.entries()]
        .map(([name, list]) => [name, [...list].sort((a, b) => (a.displayName ?? '').localeCompare(b.displayName ?? ''))] as const)
        .sort(([a], [b]) => {
            if (a === 'English') return -1;
            if (b === 'English') return 1;
            return a.localeCompare(b);
        });

    const enabledBuiltIns = builtInFeeds.filter(f => f.enabled).length;

    return (
        <div>
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

            {/* ---- Translation ---- */}
            <h2 className="settings-section-title">Translation</h2>
            <label style={{ display: 'block', marginBottom: '6px', color: 'var(--muted-foreground)' }}>
                Translate articles into
            </label>
            <select
                value={settings?.targetLanguage || 'en'}
                onChange={onLanguageChange}
                style={{
                    padding: '8px',
                    backgroundColor: '#18181b',
                    color: 'var(--foreground)',
                    border: '1px solid var(--border)',
                    borderRadius: '6px',
                    minWidth: '220px',
                }}>
                {languageOptions.map(([code, name]) => (
                    <option key={code} value={code}>{name}</option>
                ))}
            </select>

            <hr style={{ border: 'none', borderTop: '1px solid var(--border)', margin: '24px 0' }} />

            {/* ---- News sources ---- */}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '12px', marginBottom: '8px' }}>
                <h2 className="settings-section-title" style={{ margin: 0 }}>News sources</h2>
                <input
                    type="search"
                    className="feed-search"
                    placeholder="Search…"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                />
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '12px', color: 'var(--muted-foreground)', fontSize: '0.9em' }}>
                <span>{enabledBuiltIns} of {builtInFeeds.length} enabled</span>
                <span>·</span>
                <button className="feed-bulk-link" onClick={onEnableAllBuiltIn} disabled={bulkBusy || enabledBuiltIns === builtInFeeds.length}>
                    Enable all
                </button>
                <button className="feed-bulk-link" onClick={onDisableAllBuiltIn} disabled={bulkBusy || enabledBuiltIns === 0}>
                    Disable all
                </button>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                {sortedGroups.length === 0 && (
                    <div style={{ color: '#888', fontSize: '0.9em' }}>No feeds match your search.</div>
                )}
                {sortedGroups.map(([groupName, groupFeeds]) => {
                    const collapsed = collapsedGroups.has(groupName);
                    return (
                        <div key={groupName}>
                            <div className="feed-group-header" onClick={() => toggleGroupCollapsed(groupName)}>
                                <span style={{ width: '12px', display: 'inline-block' }}>{collapsed ? '▸' : '▾'}</span>
                                <span style={{ flex: 1 }}>{groupName}</span>
                                <span style={{ textTransform: 'none', letterSpacing: 0, fontWeight: 400 }}>
                                    {groupFeeds.filter(f => f.enabled).length}/{groupFeeds.length}
                                </span>
                            </div>
                            {!collapsed && (
                                <div className="feed-group-body">
                                    {groupFeeds.map(feed => (
                                        <FeedRow key={feed.clientName} feed={feed} onToggle={onToggle} variant="grouped" />
                                    ))}
                                </div>
                            )}
                        </div>
                    );
                })}
            </div>

            <hr style={{ border: 'none', borderTop: '1px solid var(--border)', margin: '24px 0' }} />

            {/* ---- Custom feeds ---- */}
            <h2 className="settings-section-title">Your custom feeds</h2>
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
                <h3 className="settings-section-title" style={{ margin: 0 }}>Add a custom feed</h3>
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
                        backgroundColor: '#18181b',
                        color: 'var(--foreground)',
                        border: '1px solid var(--border)',
                        borderRadius: '6px',
                    }}>
                    {languageOptions.map(([code, name]) => (
                        <option key={code} value={code}>{name}</option>
                    ))}
                </select>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                    <span style={{ fontSize: '0.85em', color: 'var(--muted-foreground)' }}>RSS URLs (one per line — useful for sources with multiple sub-feeds):</span>
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
    /** "card" (default) renders a standalone rounded row; "grouped" renders inside a group panel. */
    variant?: 'card' | 'grouped';
}

const FeedRow: React.FC<FeedRowProps> = ({ feed, onToggle, onDelete, variant = 'card' }) => {
    const className = variant === 'grouped' ? 'feed-group-row' : 'feed-card-row';
    const urls = feed.urls ?? [];
    return (
        <div className={className}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flex: 1, minWidth: 0 }}>
                <span style={{ fontWeight: 500 }}>{feed.displayName}</span>
                {urls.length > 1 && (
                    <span style={{
                        fontSize: '0.8em',
                        color: 'var(--muted-foreground)',
                        backgroundColor: '#2a2a30',
                        padding: '2px 6px',
                        borderRadius: '3px',
                    }}>
                        {urls.length} URLs
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
};

export default FeedSettingsComponent;
