import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import LlmOptionsComponent from './LlmOptionsComponent';

const getLlmOptions = jest.fn();
const selectLlmOption = jest.fn();
const saveLlmOption = jest.fn();
const deleteLlmOption = jest.fn();

jest.mock('../users/UserService', () => ({
    __esModule: true,
    default: {
        getInstance: () => ({
            getLlmOptions,
            selectLlmOption,
            saveLlmOption,
            deleteLlmOption,
        }),
    },
}));

describe('LlmOptionsComponent', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        getLlmOptions.mockResolvedValue({
            isConfigured: true,
            isOwner: false,
            selectedSettingsId: 1,
            options: [
                {
                    id: 1,
                    name: 'Local',
                    modelId: 'local-model',
                    isAvailable: true,
                },
                {
                    id: 2,
                    name: 'Remote',
                    modelId: 'remote-model',
                    isAvailable: true,
                },
            ],
        });
        selectLlmOption.mockResolvedValue(undefined);
        saveLlmOption.mockResolvedValue({
            id: 3,
            name: 'Local large',
            modelId: 'large-model',
            endpointUrl: 'http://localhost:1234/v1/',
            isAvailable: true,
        });
    });

    it('lets a member switch the LLM used by an active stream', async () => {
        render(<LlmOptionsComponent />);

        const select = await screen.findByRole('combobox', { name: 'Active LLM' });
        expect((select as HTMLSelectElement).value).toBe('1');
        expect(screen.queryByRole('button', { name: 'Add separate host' })).toBeNull();

        fireEvent.change(select, { target: { value: '2' } });

        await waitFor(() => expect(selectLlmOption).toHaveBeenCalledWith(2));
        expect((await screen.findByRole('status')).textContent).toContain(
            'New requests in the active news stream use it immediately.'
        );
    });

    it('lets the owner add another model by reusing a saved host connection', async () => {
        getLlmOptions.mockResolvedValue({
            isConfigured: true,
            isOwner: true,
            selectedSettingsId: 1,
            options: [
                {
                    id: 1,
                    name: 'Local',
                    modelId: 'small-model',
                    endpointUrl: 'http://localhost:1234/v1/',
                    hasApiKey: true,
                    isAvailable: true,
                    temperature: 0.2,
                    topP: 1,
                    frequencyPenalty: 0,
                    presencePenalty: 0,
                },
            ],
        });

        render(<LlmOptionsComponent />);

        fireEvent.click(await screen.findByRole('button', { name: 'Add model on Local host' }));

        const endpoint = screen.getByLabelText('Endpoint URL') as HTMLInputElement;
        expect(endpoint.value).toBe('http://localhost:1234/v1/');
        expect(endpoint.disabled).toBe(true);
        expect((screen.getByLabelText('API token') as HTMLInputElement).disabled).toBe(true);

        fireEvent.change(screen.getByLabelText('Display name'), { target: { value: 'Local large' } });
        fireEvent.change(screen.getByLabelText('Model ID'), { target: { value: 'large-model' } });
        fireEvent.click(screen.getByRole('button', { name: 'Test and save' }));

        await waitFor(() => expect(saveLlmOption).toHaveBeenCalledWith(
            expect.objectContaining({
                name: 'Local large',
                modelId: 'large-model',
                endpointUrl: null,
                connectionSourceSettingsId: 1,
                apiKey: null,
            }),
            undefined
        ));
    });
});
