import React, { useCallback, useEffect, useMemo, useState } from 'react';
import type {
    AiSettingsCatalogResponse,
    AiSettingsOptionResponse,
    SaveAiSettingsOptionRequest,
} from '../clients/UserApiClient';
import { ApiError } from '../clients/UserApiClient';
import UserService from '../users/UserService';
import DatasetDownloadComponent from './DatasetDownloadComponent';
import LlmOptionEditor, { type EditorState } from './LlmOptionEditor';

interface LlmOptionsProps {
    onSave?: () => Promise<void>;
    initialError?: string | null;
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
    useResultsForDataset: false,
});

const generationFromOption = (option: AiSettingsOptionResponse) => ({
    temperature: option.temperature ?? 0.2,
    topP: option.topP ?? 1,
    frequencyPenalty: option.frequencyPenalty ?? 1,
    presencePenalty: option.presencePenalty ?? 1,
});

const editorFromOption = (option: AiSettingsOptionResponse): EditorState => ({
    ...generationFromOption(option),
    name: option.name ?? '',
    modelId: option.modelId ?? '',
    endpointUrl: option.endpointUrl ?? '',
    apiKey: '',
    hasApiKey: option.hasApiKey === true,
    useResultsForDataset: option.useResultsForDataset === true,
});

const createSaveRequest = (
    editor: EditorState,
    connectionSource: AiSettingsOptionResponse | null,
    clearApiKey: boolean,
): SaveAiSettingsOptionRequest => ({
    name: editor.name.trim(),
    modelId: editor.modelId,
    endpointUrl: connectionSource == null ? editor.endpointUrl : null,
    connectionSourceSettingsId: connectionSource?.id ?? null,
    apiKey: connectionSource == null && editor.apiKey.trim().length > 0 ? editor.apiKey : null,
    clearApiKey,
    temperature: editor.temperature,
    topP: editor.topP,
    frequencyPenalty: editor.frequencyPenalty,
    presencePenalty: editor.presencePenalty,
    useResultsForDataset: editor.useResultsForDataset,
});

const getOptionLabel = (option: AiSettingsOptionResponse | undefined) =>
    option?.name ?? option?.modelId ?? 'the selected LLM';

const countHostModels = (options: AiSettingsOptionResponse[], editingId: number | null) => {
    const editingOption = options.find(option => option.id === editingId);
    if (editingOption == null) return 0;
    const hostId = editingOption.hostId ?? editingOption.id;
    return options.filter(option => (option.hostId ?? option.id) === hostId).length;
};

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
    const isOwner = catalog?.isOwner === true;
    const editingHostModelCount = countHostModels(options, editingId);

    const load = useCallback(() => userService.getLlmOptions().then(loaded => {
        setCatalog({ ...loaded, options: loaded.options ?? [] });
        return loaded;
    }), [userService]);

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
            useResultsForDataset: false,
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
                `Switched to ${getOptionLabel(selected)}. New requests in the active news stream use it immediately.`
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
            const request = createSaveRequest(editor, connectionSource, clearApiKey);
            const wasFirstOption = options.length === 0;
            const saved = await userService.saveLlmOption(request, editingId ?? undefined);
            if (wasFirstOption && saved.id != null) {
                await userService.selectLlmOption(saved.id);
            }

            await load();
            beginEdit(saved);
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
            <LlmChoiceSection catalog={catalog} options={options} isSelecting={isSelecting} selectOption={selectOption} />

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
                                                    <span>Dataset collection: {option.useResultsForDataset ? 'on' : 'off'}</span>
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

                    <LlmOptionEditor
                        editor={editor}
                        editingId={editingId}
                        connectionSource={connectionSource}
                        editingHostModelCount={editingHostModelCount}
                        clearApiKey={clearApiKey}
                        isSaving={isSaving}
                        update={update}
                        setClearApiKey={setClearApiKey}
                        submit={submit}
                    />
                    <DatasetDownloadComponent />
                </>
            )}

            {error && <div className="llm-connection-error" role="alert">{error}</div>}
            {savedMessage && <div className="llm-connection-success" role="status">{savedMessage}</div>}
        </div>
    );
};

interface LlmChoiceSectionProps {
    catalog: AiSettingsCatalogResponse | null;
    options: AiSettingsOptionResponse[];
    isSelecting: boolean;
    selectOption: (settingsId: number) => Promise<void>;
}

const LlmChoiceSection: React.FC<LlmChoiceSectionProps> = ({ catalog, options, isSelecting, selectOption }) => (
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
                {catalog?.isOwner
                    ? 'Add and test the first LLM option below.'
                    : 'The owner has not configured an LLM option yet.'}
            </p>
        )}
    </section>
);

const getErrorMessage = (error: unknown, fallback: string): string => {
    if (error instanceof ApiError) {
        const detail = error.body?.detail;
        if (typeof detail === 'string' && detail.length > 0) return detail;
    }
    return error instanceof Error && error.message ? error.message : fallback;
};

export default LlmOptionsComponent;
