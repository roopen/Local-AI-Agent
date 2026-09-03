import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { downloadDataset, getDatasetModels } from '../clients/DatasetClient';
import DatasetDownloadComponent from './DatasetDownloadComponent';

jest.mock('../clients/DatasetClient', () => ({ getDatasetModels: jest.fn(), downloadDataset: jest.fn() }));
const loadModels = jest.mocked(getDatasetModels);
const download = jest.mocked(downloadDataset);

describe('DatasetDownloadComponent', () => {
    beforeEach(() => {
        jest.resetAllMocks();
        loadModels.mockResolvedValue(['publisher/model:4b+q8', 'retired-model']);
        download.mockResolvedValue(undefined);
    });

    it('downloads all models by default, then the selected historical model', async () => {
        render(<DatasetDownloadComponent />);
        await screen.findByRole('option', { name: 'retired-model' });
        fireEvent.click(screen.getByRole('button', { name: 'Download dataset' }));
        await waitFor(() => expect(download).toHaveBeenCalledWith(undefined));
        await screen.findByRole('status');
        fireEvent.change(screen.getByLabelText('Dataset model'), { target: { value: 'retired-model' } });
        expect(screen.getByText(/Translation samples are excluded/)).toBeTruthy();
        fireEvent.click(screen.getByRole('button', { name: 'Download dataset' }));
        await waitFor(() => expect(download).toHaveBeenLastCalledWith('retired-model'));
    });

    it('disables controls while downloading and reports a failure', async () => {
        let rejectDownload: (reason: Error) => void = () => {};
        download.mockImplementation(() => new Promise((_, reject) => { rejectDownload = reject; }));
        render(<DatasetDownloadComponent />);
        await screen.findByRole('option', { name: 'retired-model' });
        fireEvent.click(screen.getByRole('button', { name: 'Download dataset' }));
        expect((screen.getByRole('button', { name: 'Preparing download...' }) as HTMLButtonElement).disabled).toBe(true);
        expect((screen.getByLabelText('Dataset model') as HTMLSelectElement).disabled).toBe(true);
        await act(async () => rejectDownload(new Error('No matching data.')));
        expect((await screen.findByRole('alert')).textContent).toBe('No matching data.');
        expect((screen.getByRole('button', { name: 'Download dataset' }) as HTMLButtonElement).disabled).toBe(false);
    });

    it('retries model loading and keeps all-model downloads available when there are only translations', async () => {
        loadModels.mockRejectedValueOnce(new Error('offline')).mockResolvedValue([]);
        render(<DatasetDownloadComponent />);
        expect((await screen.findByRole('alert')).textContent).toContain('Failed to load dataset models');
        fireEvent.click(screen.getByRole('button', { name: 'Refresh models' }));
        await screen.findByText(/No saved evaluations yet/);
        expect((screen.getByRole('button', { name: 'Download dataset' }) as HTMLButtonElement).disabled).toBe(false);
    });
});
