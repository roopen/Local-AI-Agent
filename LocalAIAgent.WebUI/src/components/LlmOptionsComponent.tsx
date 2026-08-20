import React, { useCallback, useEffect, useMemo, useState } from 'react';
import type {
    AiSettingsCatalogResponse,
    AiSettingsOptionResponse,
    SaveAiSettingsOptionRequest,
} from '../clients/UserApiClient';
import { ApiError } from '../clients/UserApiClient';
import UserService from '../users/UserService';

interface LlmOptionsProps {
    onSave?: () => Promise<void>;
    initialError?: string | null;
}

interface EditorState {
    name: string;
    modelId: string;
    endpointUrl: string;
    apiKey: string;
    hasApiKey: boolean;
    temperature: number;
    topP: number;
    frequencyPenalty: number;
    presencePenalty: number;
}

const createEmptyEditor = (): EditorState => ({
    name: '',
    modelId: 'gemma-3-27b-it-qat',
    endpointUrl: 'http://localhost:1234/v1/',
    apiKey: '',
    hasApiKey: false,
    temperature: 0.2,
    topP: 1,
    frequencyPenalty: 1,
    presencePenalty: 1,
});

const editorFromOption = (option: AiSettingsOptionResponse): EditorState => ({
    name: option.name ?? '',
    modelId: option.modelId ?? '',
    endpointUrl: option.endpointUrl ?? '',
    apiKey: '',
    hasApiKey: option.hasApiKey ?? false,
    temperature: option.temperature ?? 0.2,
    topP: option.topP ?? 1,
    frequencyPenalty: option.frequencyPenalty ?? 1,
    presencePenalty: option.presencePenalty ?? 1,
});

const LlmOptionsComponent: React.FC<LlmOptionsProps> = ({ onSave, initialError }) => {
    const userService = UserService.getInstance();
    const [catalog, setCatalog] = useState<AiSettingsCatalogResponse | null>(null);
    const [editor, setEditor] = useState<EditorState>(createEmptyEditor);
    const [editingId, setEditingId] = useState<number | null>(null);
    const [connectionSource, setConnectionSource] = useState<AiSettingsOptionResponse | null>(null);
    const [clearApiKey, setClearApiKey] = useState(false);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [isSelecting, setIsSelecting] = useState(false);
    const [error, setError] = useState<string | null>(initialError ?? null);
    const [savedMessage, setSavedMessage] = useState<string | null>(null);

    const options = useMemo(() => catalog?.options ?? [], [catalog]);
    const hostGroups = useMemo(() => {
        const groups = new Map<string, AiSettingsOptionResponse[]>();
        for (const option of options) {
            const hostKey = String(option.hostId ?? option.id ?? option.endpointUrl);
            groups.set(hostKey, [...(groups.get(hostKey) ?? []), option]);
        }
        return [...groups.entries()].map(([key, models]) => ({ key, models }));
    }, [options]);
    const isOwner = catalog?.isOwner ?? false;
    const editingOption = options.find(option => option.id === editingId);
    const editingHostModelCount = editingOption == null
        ? 0
        : options.filter(option =>
            (option.hostId ?? option.id) === (editingOption.hostId ?? editingOption.id)).length;

    const load = useCallback(async () => {
        const loaded = await userService.getLlmOptions();
        setCatalog({ ...loaded, options: loaded.options ?? [] });
        return loaded;
    }, [userService]);

    useEffect(() => {
        load()
            .catch(reason => setError(getErrorMessage(reason, 'Failed to load LLM options.')))
            .finally(() => setIsLoading(false));
    }, [load]);

    const update = (changes: Partial<EditorState>) => {
        setEditor(current => ({ ...current, ...changes }));
        setSavedMessage(null);
    };

    const beginCreate = () => {
        setEditingId(null);
        setConnectionSource(null);
        setEditor(createEmptyEditor());
        setClearApiKey(false);
        setError(null);
        setSavedMessage(null);
    };

    const beginCreateOnHost = (option: AiSettingsOptionResponse) => {
        setEditingId(null);
        setConnectionSource(option);
        setEditor({
            ...editorFromOption(option),
            name: '',
            modelId: '',
        });
        setClearApiKey(false);
        setError(null);
        setSavedMessage(null);
    };

    const beginEdit = (option: AiSettingsOptionResponse) => {
        setEditingId(option.id ?? null);
        setConnectionSource(null);
        setEditor(editorFromOption(option));
        setClearApiKey(false);
        setError(null);
        setSavedMessage(null);
    };

    const selectOption = async (settingsId: number) => {
        if (isSelecting || settingsId === catalog?.selectedSettingsId) return;

        setIsSelecting(true);
        setError(null);
        setSavedMessage(null);
        try {
            await userService.selectLlmOption(settingsId);
            const selected = options.find(option => option.id === settingsId);
            setCatalog(current => current
                ? { ...current, selectedSettingsId: settingsId }
                : current);
            setSavedMessage(
                `Switched to ${selected?.name ?? selected?.modelId ?? 'the selected LLM'}. New requests in the active news stream use it immediately.`
            );
        } catch (reason) {
            setError(getErrorMessage(reason, 'Failed to select the LLM.'));
        } finally {
            setIsSelecting(false);
        }
    };

    const submit = async (event: React.FormEvent) => {
        event.preventDefault();
        if (isSaving) return;

        setIsSaving(true);
        setError(null);
        setSavedMessage(null);
        try {
            const request: SaveAiSettingsOptionRequest = {
                name: editor.name.trim(),
                modelId: editor.modelId,
                endpointUrl: connectionSource == null ? editor.endpointUrl : null,
                connectionSourceSettingsId: connectionSource?.id ?? null,
                apiKey: connectionSource == null && editor.apiKey.trim().length > 0
                    ? editor.apiKey
                    : null,
                clearApiKey,
                temperature: editor.temperature,
                topP: editor.topP,
                frequencyPenalty: editor.frequencyPenalty,
                presencePenalty: editor.presencePenalty,
            };
            const wasFirstOption = options.length === 0;
            const saved = await userService.saveLlmOption(request, editingId ?? undefined);
            if (wasFirstOption && saved.id != null) {
                await userService.selectLlmOption(saved.id);
            }

            await load();
            setEditingId(saved.id ?? null);
            setConnectionSource(null);
            setEditor(editorFromOption(saved));
            setClearApiKey(false);
            setSavedMessage('Connection tested and option saved.');
            await onSave?.();
        } catch (reason) {
            setError(getErrorMessage(reason, 'The LLM connection test failed.'));
        } finally {
            setIsSaving(false);
        }
    };

    const deleteOption = async (option: AiSettingsOptionResponse) => {
        if (option.id == null || !window.confirm(`Delete "${option.name ?? option.modelId}"?`)) return;

        setError(null);
        setSavedMessage(null);
        try {
            await userService.deleteLlmOption(option.id);
            if (editingId === option.id) beginCreate();
            await load();
            setSavedMessage('LLM option deleted.');
            await onSave?.();
        } catch (reason) {
            setError(getErrorMessage(reason, 'Failed to delete the LLM option.'));
        }
    };

    if (isLoading) return <p className="prompt-hint">Loading LLM options...</p>;

    return (
        <div className="llm-settings-form">
            <section className="llm-choice-section">
                <h2 className="settings-section-title">Your LLM</h2>
                <p className="prompt-hint">
                    Choose which tested LLM handles your news. A change applies to the next AI request, including an active news stream.
                </p>
                {options.length > 0 ? (
                    <label className="llm-field llm-field--wide">
                        <span>Active LLM</span>
                        <select
                            aria-label="Active LLM"
                            value={catalog?.selectedSettingsId ?? ''}
                            disabled={isSelecting}
                            onChange={event => void selectOption(Number(event.target.value))}
                        >
                            {options.map(option => (
                                <option
                                    key={option.id}
                                    value={option.id}
                                    disabled={!option.isAvailable}
                                >
                                    {option.name} ({option.modelId}){option.isAvailable ? '' : ' - unavailable'}
                                </option>
                            ))}
                        </select>
                    </label>
                ) : (
                    <p className="prompt-hint">
                        {isOwner
                            ? 'Add and test the first LLM option below.'
                            : 'The owner has not configured an LLM option yet.'}
                    </p>
                )}
            </section>

            {isOwner && (
                <>
                    <section>
                        <div className="llm-options-heading">
                            <div>
                                <h2 className="settings-section-title">Available options</h2>
                                <p className="prompt-hint">Every option is connection-tested before it is saved.</p>
                            </div>
                            <button type="button" className="secondary-button" onClick={beginCreate}>
                                Add separate host
                            </button>
                        </div>
                        <div className="llm-option-list">
                            {hostGroups.map(host => {
                                const connectionSourceOption = host.models[0];
                                return (
                                    <div className="llm-host-group" key={host.key}>
                                        <div className="llm-host-header">
                                            <div>
                                                <strong>{connectionSourceOption.endpointUrl}</strong>
                                                <span>{host.models.length} {host.models.length === 1 ? 'model' : 'models'}</span>
                                            </div>
                                            <button
                                                type="button"
                                                aria-label={`Add model on ${connectionSourceOption.name} host`}
                                                onClick={() => beginCreateOnHost(connectionSourceOption)}
                                            >
                                                Add model
                                            </button>
                                        </div>
                                        {host.models.map(option => (
                                            <div className="llm-option-row" key={option.id}>
                                                <div>
                                                    <strong>{option.name}</strong>
                                                    <span>{option.modelId}</span>
                                                </div>
                                                <div className="llm-option-actions">
                                                    <button type="button" onClick={() => beginEdit(option)}>Edit</button>
                                                    <button type="button" onClick={() => void deleteOption(option)}>Delete</button>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                );
                            })}
                        </div>
                    </section>

                    <form className="llm-option-editor" onSubmit={submit}>
                        <h2 className="settings-section-title">
                            {connectionSource != null
                                ? `Add model on ${connectionSource.name} host`
                                : editingId == null
                                    ? 'Add separate host and model'
                                    : 'Edit LLM option'}
                        </h2>
                        {connectionSource != null && (
                            <p className="llm-shared-host-note">
                                Reusing the saved endpoint and API token from {connectionSource.name}. The new model is tested independently before saving.
                            </p>
                        )}
                        {connectionSource == null && editingHostModelCount > 1 && (
                            <p className="llm-shared-host-note">
                                This host has {editingHostModelCount} models. Endpoint or API token changes apply to every model on the host, and all of them are retested before saving.
                            </p>
                        )}
                        <div className="llm-settings-grid">
                            <TextField label="Display name" value={editor.name} onChange={name => update({ name })} />
                            <TextField
                                label="Endpoint URL"
                                type="url"
                                value={editor.endpointUrl}
                                disabled={connectionSource != null}
                                onChange={endpointUrl => update({ endpointUrl })}
                            />
                            <TextField label="Model ID" value={editor.modelId} onChange={modelId => update({ modelId })} />
                            <label className="llm-field llm-field--wide">
                                <span>API token</span>
                                <input
                                    type="password"
                                    autoComplete="new-password"
                                    value={editor.apiKey}
                                    disabled={clearApiKey || connectionSource != null}
                                    onChange={event => update({ apiKey: event.target.value })}
                                    placeholder={connectionSource != null
                                        ? 'Reusing the saved host token'
                                        : editor.hasApiKey
                                            ? 'Saved token (leave blank to keep)'
                                            : 'Optional for unsecured local APIs'}
                                />
                            </label>
                            {editingId != null && (
                                <label className="llm-clear-token llm-field--wide">
                                    <input
                                        type="checkbox"
                                        checked={clearApiKey}
                                        onChange={event => setClearApiKey(event.target.checked)}
                                    />
                                    <span>Remove the saved token and test without authentication</span>
                                </label>
                            )}
                        </div>
                        <div className="llm-generation-section">
                            <h2 className="settings-section-title">Generation</h2>
                            <div className="llm-settings-grid llm-settings-grid--numbers">
                                <NumberField label="Temperature" value={editor.temperature} min={0} max={2} step={0.1} onChange={temperature => update({ temperature })} />
                                <NumberField label="Top P" value={editor.topP} min={0} max={1} step={0.05} onChange={topP => update({ topP })} />
                                <NumberField label="Frequency penalty" value={editor.frequencyPenalty} min={-2} max={2} step={0.1} onChange={frequencyPenalty => update({ frequencyPenalty })} />
                                <NumberField label="Presence penalty" value={editor.presencePenalty} min={-2} max={2} step={0.1} onChange={presencePenalty => update({ presencePenalty })} />
                            </div>
                        </div>
                        <div className="prompt-save-row">
                            <button type="submit" className="primary-button" disabled={isSaving}>
                                {isSaving ? 'Testing connection...' : 'Test and save'}
                            </button>
                        </div>
                    </form>
                </>
            )}

            {error && <div className="llm-connection-error" role="alert">{error}</div>}
            {savedMessage && <div className="llm-connection-success" role="status">{savedMessage}</div>}
        </div>
    );
};

interface TextFieldProps {
    label: string;
    value: string;
    type?: 'text' | 'url';
    disabled?: boolean;
    onChange: (value: string) => void;
}

const TextField: React.FC<TextFieldProps> = ({ label, value, type = 'text', disabled = false, onChange }) => (
    <label className="llm-field llm-field--wide">
        <span>{label}</span>
        <input type={type} required value={value} disabled={disabled} onChange={event => onChange(event.target.value)} />
    </label>
);

interface NumberFieldProps {
    label: string;
    value: number;
    min: number;
    max: number;
    step: number;
    onChange: (value: number) => void;
}

const NumberField: React.FC<NumberFieldProps> = ({ label, value, min, max, step, onChange }) => (
    <label className="llm-field">
        <span>{label}</span>
        <input type="number" required value={value} min={min} max={max} step={step} onChange={event => onChange(event.target.valueAsNumber)} />
    </label>
);

const getErrorMessage = (error: unknown, fallback: string): string => {
    if (error instanceof ApiError) {
        const detail = error.body?.detail;
        if (typeof detail === 'string' && detail.length > 0) return detail;
    }
    return error instanceof Error && error.message ? error.message : fallback;
};

export default LlmOptionsComponent;
