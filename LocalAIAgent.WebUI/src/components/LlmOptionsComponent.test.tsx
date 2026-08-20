import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import LlmOptionsComponent from './LlmOptionsComponent';

const getLlmOptions = jest.fn();
const selectLlmOption = jest.fn();

jest.mock('../users/UserService', () => ({
    __esModule: true,
    default: {
        getInstance: () => ({
            getLlmOptions,
            selectLlmOption,
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
    });

    it('lets a member switch the LLM used by an active stream', async () => {
        render(<LlmOptionsComponent />);

        const select = await screen.findByRole('combobox', { name: 'Active LLM' });
        expect((select as HTMLSelectElement).value).toBe('1');
        expect(screen.queryByRole('button', { name: 'Add LLM' })).toBeNull();

        fireEvent.change(select, { target: { value: '2' } });

        await waitFor(() => expect(selectLlmOption).toHaveBeenCalledWith(2));
        expect((await screen.findByRole('status')).textContent).toContain(
            'New requests in the active news stream use it immediately.'
        );
    });
});
