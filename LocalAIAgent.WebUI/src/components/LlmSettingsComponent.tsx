import React, { useEffect, useState } from 'react';
import { ApiError } from '../clients/UserApiClient';
import AISettings from '../domain/AISettings';
import UserService from '../users/UserService';

interface LlmSettingsProps {
    onSave?: () => Promise<void>;
    initialError?: string | null;
}

const LlmSettingsComponent: React.FC<LlmSettingsProps> = ({ onSave, initialError }) => {
    const [settings, setSettings] = useState<AISettings>(new AISettings());
    const [clearApiKey, setClearApiKey] = useState(false);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState<string | null>(initialError ?? null);
    const [savedMessage, setSavedMessage] = useState<string | null>(null);
    const userService = UserService.getInstance();

    useEffect(() => {
        userService.getAiSettings()
            .then(setSettings)
            .catch(e => setError(getErrorMessage(e, 'Failed to load LLM settings.')))
            .finally(() => setIsLoading(false));
    }, [userService]);

    const update = (changes: Partial<AISettings>) => {
        setSettings(current => Object.assign(new AISettings(), current, changes));
        setSavedMessage(null);
    };

    const submit = async (event: React.FormEvent) => {
        event.preventDefault();
        if (isSaving) return;

        setIsSaving(true);
        setError(null);
        setSavedMessage(null);
        try {
            const saved = await userService.saveAiSettings(settings, clearApiKey);
            saved.apiKey = '';
            setSettings(saved);
            setClearApiKey(false);
            setSavedMessage('Connected and applied immediately.');
            await onSave?.();
        } catch (e) {
            setError(getErrorMessage(e, 'The LLM connection test failed.'));
        } finally {
            setIsSaving(false);
        }
    };

    if (isLoading) return <p className="prompt-hint">Loading LLM settings...</p>;

    return (
        <form className="llm-settings-form" onSubmit={submit}>
            <div>
                <h2 className="settings-section-title">Connection</h2>
                <p className="prompt-hint">
                    Use an OpenAI-compatible chat endpoint. Saving sends a one-token “hi” request before anything is changed.
                </p>
            </div>

            <div className="llm-settings-grid">
                <label className="llm-field llm-field--wide">
                    <span>Endpoint URL</span>
                    <input
                        type="url"
                        required
                        value={settings.endpointUrl}
                        onChange={e => update({ endpointUrl: e.target.value })}
                        placeholder="http://localhost:1234/v1/"
                    />
                </label>
                <label className="llm-field llm-field--wide">
                    <span>Model ID</span>
                    <input
                        type="text"
                        required
                        value={settings.modelId}
                        onChange={e => update({ modelId: e.target.value })}
                        placeholder="gemma-3-27b-it-qat"
                    />
                </label>
                <label className="llm-field llm-field--wide">
                    <span>API token</span>
                    <input
                        type="password"
                        autoComplete="new-password"
                        value={settings.apiKey}
                        disabled={clearApiKey}
                        onChange={e => update({ apiKey: e.target.value })}
                        placeholder={settings.hasApiKey ? 'Saved token (leave blank to keep)' : 'Optional for unsecured local APIs'}
                    />
                </label>
                <label className="llm-clear-token llm-field--wide">
                    <input
                        type="checkbox"
                        checked={clearApiKey}
                        onChange={e => setClearApiKey(e.target.checked)}
                    />
                    <span>Remove the saved token and test without authentication</span>
                </label>
            </div>

            <div className="llm-generation-section">
                <h2 className="settings-section-title">Generation</h2>
                <div className="llm-settings-grid llm-settings-grid--numbers">
                    <NumberField label="Temperature" value={settings.temperature} min={0} max={2} step={0.1} onChange={temperature => update({ temperature })} />
                    <NumberField label="Top P" value={settings.topP} min={0} max={1} step={0.05} onChange={topP => update({ topP })} />
                    <NumberField label="Frequency penalty" value={settings.frequencyPenalty} min={-2} max={2} step={0.1} onChange={frequencyPenalty => update({ frequencyPenalty })} />
                    <NumberField label="Presence penalty" value={settings.presencePenalty} min={-2} max={2} step={0.1} onChange={presencePenalty => update({ presencePenalty })} />
                </div>
            </div>

            {error && <div className="llm-connection-error" role="alert">{error}</div>}
            {savedMessage && <div className="llm-connection-success" role="status">{savedMessage}</div>}

            <div className="prompt-save-row">
                <button type="submit" className="primary-button" disabled={isSaving}>
                    {isSaving ? 'Testing connection...' : 'Test and save'}
                </button>
            </div>
        </form>
    );
};

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
        <input
            type="number"
            required
            value={value}
            min={min}
            max={max}
            step={step}
            onChange={e => onChange(e.target.valueAsNumber)}
        />
    </label>
);

const getErrorMessage = (error: unknown, fallback: string): string => {
    if (error instanceof ApiError) {
        const detail = error.body?.detail;
        if (typeof detail === 'string' && detail.length > 0) return detail;
    }
    return error instanceof Error && error.message ? error.message : fallback;
};

export default LlmSettingsComponent;
