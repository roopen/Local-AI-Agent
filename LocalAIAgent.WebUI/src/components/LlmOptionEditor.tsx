import React from 'react';
import type { AiSettingsOptionResponse } from '../clients/UserApiClient';

export interface EditorState {
    name: string;
    modelId: string;
    endpointUrl: string;
    apiKey: string;
    hasApiKey: boolean;
    temperature: number;
    topP: number;
    frequencyPenalty: number;
    presencePenalty: number;
    useResultsForDataset: boolean;
}

interface LlmOptionEditorProps {
    editor: EditorState;
    editingId: number | null;
    connectionSource: AiSettingsOptionResponse | null;
    editingHostModelCount: number;
    clearApiKey: boolean;
    isSaving: boolean;
    update: (changes: Partial<EditorState>) => void;
    setClearApiKey: (clear: boolean) => void;
    submit: (event: React.FormEvent) => Promise<void>;
}

const LlmOptionEditor: React.FC<LlmOptionEditorProps> = ({
    editor, editingId, connectionSource, editingHostModelCount,
    clearApiKey, isSaving, update, setClearApiKey, submit,
}) => (
    <form className="llm-option-editor" onSubmit={submit}>
        <EditorHeading connectionSource={connectionSource} editingId={editingId} editingHostModelCount={editingHostModelCount} />
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
            <ApiTokenFields
                editor={editor}
                connectionSource={connectionSource}
                editingId={editingId}
                clearApiKey={clearApiKey}
                setClearApiKey={setClearApiKey}
                update={update}
            />
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
        <div className="llm-collection-setting">
            <label className="llm-clear-token">
                <input
                    type="checkbox"
                    checked={editor.useResultsForDataset}
                    disabled={isSaving}
                    onChange={event => update({ useResultsForDataset: event.target.checked })}
                />
                <span>Save results to dataset</span>
            </label>
        </div>
        <p className="prompt-hint">
            Include new evaluations and translations from this LLM in training data. Previously collected data stays available.
        </p>
        <div className="prompt-save-row">
            <button type="submit" className="primary-button" disabled={isSaving}>
                {isSaving ? 'Testing connection...' : 'Test and save'}
            </button>
        </div>
    </form>
);

const EditorHeading: React.FC<Pick<LlmOptionEditorProps,
    'connectionSource' | 'editingId' | 'editingHostModelCount'>> = ({
    connectionSource, editingId, editingHostModelCount,
}) => (
    <>
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
    </>
);

const ApiTokenFields: React.FC<Pick<LlmOptionEditorProps,
    'editor' | 'connectionSource' | 'editingId' | 'clearApiKey' | 'setClearApiKey' | 'update'>> = ({
    editor, connectionSource, editingId, clearApiKey, setClearApiKey, update,
}) => (
    <>
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
    </>
);

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

export default LlmOptionEditor;
