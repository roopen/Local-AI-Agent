import { useCallback, useEffect, useState } from 'react';
import { downloadDataset, getDatasetModels } from '../clients/DatasetClient';

const DatasetDownloadComponent = () => {
    const [models, setModels] = useState<string[]>([]);
    const [modelId, setModelId] = useState('');
    const [isLoading, setIsLoading] = useState(true);
    const [isDownloading, setIsDownloading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [message, setMessage] = useState<string | null>(null);

    const loadModels = useCallback(() => getDatasetModels()
        .then(setModels)
        .catch(() => setError('Failed to load dataset models. Refresh to try again.'))
        .finally(() => setIsLoading(false)), []);

    useEffect(() => { void loadModels(); }, [loadModels]);

    const refresh = () => {
        setIsLoading(true);
        setError(null);
        setMessage(null);
        void loadModels();
    };

    const download = async () => {
        if (isDownloading) return;
        setIsDownloading(true);
        setError(null);
        setMessage(null);
        try {
            await downloadDataset(modelId || undefined);
            setMessage('Dataset download started.');
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : 'Failed to download the dataset.');
        } finally {
            setIsDownloading(false);
        }
    };

    const disabled = isLoading || isDownloading;
    return (
        <section className="llm-dataset-section" aria-labelledby="dataset-heading">
            <h2 className="settings-section-title" id="dataset-heading">Training dataset</h2>
            <p className="prompt-hint">Download training and evaluation files as a ZIP. Choose a model that produced the saved evaluations.</p>
            <div className="llm-dataset-controls">
                <DatasetModelSelect models={models} modelId={modelId} disabled={disabled}
                    onChange={value => { setModelId(value); setMessage(null); setError(null); }} />
                <button type="button" className="secondary-button" disabled={disabled} onClick={refresh}>
                    {isLoading ? 'Loading models...' : 'Refresh models'}
                </button>
                <button type="button" className="primary-button" disabled={disabled} onClick={() => void download()}>
                    {isDownloading ? 'Preparing download...' : 'Download dataset'}
                </button>
            </div>
            <p className="prompt-hint">
                {modelId
                    ? 'Includes evaluations from this model. Translation samples are excluded because their model was not recorded.'
                    : 'Includes saved evaluations from all models and translation samples.'}
            </p>
            <DatasetFeedback isLoading={isLoading} isEmpty={models.length === 0} error={error} message={message} />
        </section>
    );
};

interface DatasetModelSelectProps {
    models: string[];
    modelId: string;
    disabled: boolean;
    onChange: (value: string) => void;
}

const DatasetModelSelect = ({ models, modelId, disabled, onChange }: DatasetModelSelectProps) => (
    <label className="llm-field">
        <span>Dataset model</span>
        <select value={modelId} disabled={disabled} onChange={event => onChange(event.target.value)}>
            <option value="">All models</option>
            {modelId && !models.includes(modelId) && <option value={modelId}>{modelId} (no longer available)</option>}
            {models.map(model => <option key={model} value={model}>{model}</option>)}
        </select>
    </label>
);

interface DatasetFeedbackProps {
    isLoading: boolean;
    isEmpty: boolean;
    error: string | null;
    message: string | null;
}

const DatasetFeedback = ({ isLoading, isEmpty, error, message }: DatasetFeedbackProps) => {
    if (error) return <div className="llm-connection-error" role="alert">{error}</div>;
    if (message) return <div className="llm-connection-success" role="status">{message}</div>;
    if (!isLoading && isEmpty) return <p className="prompt-hint">No saved evaluations yet. All models can still include saved translations.</p>;
    return null;
};

export default DatasetDownloadComponent;
