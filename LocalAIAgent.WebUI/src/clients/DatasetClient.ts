import { NewsService, OpenAPI } from './UserApiClient';

export const getDatasetModels = (): Promise<string[]> => NewsService.getDatasetModels();

export const downloadDataset = async (modelId?: string): Promise<void> => {
    const query = modelId ? `?${new URLSearchParams({ modelId })}` : '';
    const response = await fetch(`${OpenAPI.BASE}/api/News/Dataset${query}`, {
        credentials: 'include',
        headers: { Accept: 'application/zip' },
    });

    if (!response.ok) {
        if (response.status === 404) {
            throw new Error('No dataset entries were found for the selected model. Refresh the model list and try again.');
        }
        if (response.status === 401 || response.status === 403) {
            throw new Error('Sign in as the owner to download datasets.');
        }
        throw new Error('Failed to download the dataset. Please try again.');
    }

    const archive = await response.blob();
    const url = URL.createObjectURL(archive);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'dataset.zip';
    document.body.appendChild(link);
    try {
        link.click();
    } finally {
        link.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
};
