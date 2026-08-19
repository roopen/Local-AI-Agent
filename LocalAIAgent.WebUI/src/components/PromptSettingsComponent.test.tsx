import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import UserSettings from '../domain/UserSettings';
import PromptSettingsComponent from './PromptSettingsComponent';

const saveUserPreferences = jest.fn().mockResolvedValue(undefined);
const getUserPreferences = jest.fn();

jest.mock('../users/UserService', () => ({
    __esModule: true,
    default: {
        getInstance: () => ({
            getCurrentUser: () => ({ id: '1', name: 'owner', role: 'Owner' }),
            getUserPreferences,
            saveUserPreferences,
        }),
    },
}));

describe('PromptSettingsComponent', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        getUserPreferences.mockResolvedValue(new UserSettings(['existing'], ['spam']));
    });

    it('merges a pasted JSON string array into likes', async () => {
        render(<PromptSettingsComponent />);

        await screen.findByText('Likes (1)');
        fireEvent.click(screen.getByRole('button', { name: 'Add likes' }));
        fireEvent.paste(screen.getByPlaceholderText('Add a like…'), {
            clipboardData: {
                getData: () => '["war","geopolitics","science",".NET","C#","existing"]',
            },
        });

        await waitFor(() => expect(saveUserPreferences).toHaveBeenCalled());
        const saved = saveUserPreferences.mock.calls[saveUserPreferences.mock.calls.length - 1][0] as UserSettings;
        expect(saved.likes).toEqual(['existing', 'war', 'geopolitics', 'science', '.NET', 'C#']);
        expect(saved.dislikes).toEqual(['spam']);
    });

    it('merges a pasted JSON string array into dislikes', async () => {
        render(<PromptSettingsComponent />);

        await screen.findByText('Dislikes (1)');
        fireEvent.click(screen.getByRole('button', { name: 'Add dislikes' }));
        fireEvent.paste(screen.getByPlaceholderText('Add a dislike…'), {
            clipboardData: {
                getData: () => '[" clickbait ","crypto","crypto",""]',
            },
        });

        await waitFor(() => expect(saveUserPreferences).toHaveBeenCalled());
        const saved = saveUserPreferences.mock.calls[saveUserPreferences.mock.calls.length - 1][0] as UserSettings;
        expect(saved.likes).toEqual(['existing']);
        expect(saved.dislikes).toEqual(['spam', 'clickbait', 'crypto']);
    });

    it('shows an error when a pasted JSON array contains non-string items', async () => {
        render(<PromptSettingsComponent />);

        await screen.findByText('Likes (1)');
        fireEvent.click(screen.getByRole('button', { name: 'Add likes' }));
        fireEvent.paste(screen.getByPlaceholderText('Add a like…'), {
            clipboardData: {
                getData: () => '["science",42]',
            },
        });

        expect((await screen.findByRole('alert')).textContent).toBe(
            'Every item in the pasted JSON array must be a string.'
        );
        expect(saveUserPreferences).not.toHaveBeenCalled();
    });
});
