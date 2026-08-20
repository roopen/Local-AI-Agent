import { fireEvent, render, screen } from '@testing-library/react';
import SettingsComponent from './SettingsComponent';

jest.mock('../users/UserService', () => ({
    __esModule: true,
    default: {
        getInstance: () => ({
            getCurrentUser: () => ({ id: '1', name: 'owner', role: 'Owner' }),
        }),
    },
}));

jest.mock('./PromptSettingsComponent', () => ({
    __esModule: true,
    default: () => <div>Prompt settings</div>,
}));
jest.mock('./FeedSettingsComponent', () => ({
    __esModule: true,
    default: () => <div>Feed settings</div>,
}));
jest.mock('./AuthenticationSettingsComponent', () => ({
    __esModule: true,
    default: () => <div>Authentication settings</div>,
}));
jest.mock('./LlmOptionsComponent', () => ({
    __esModule: true,
    default: ({ initialError }: { initialError?: string }) => (
        <div>LLM settings: {initialError}</div>
    ),
}));

describe('SettingsComponent', () => {
    it('opens the LLM tab with the connection failure reason', () => {
        render(
            <SettingsComponent
                initialTab="llm"
                initialLlmError="The AI service host could not be resolved."
            />
        );

        expect(screen.getByText(
            'LLM settings: The AI service host could not be resolved.'
        )).toBeTruthy();
        expect(screen.queryByText('Prompt settings')).toBeNull();

        fireEvent.click(screen.getByRole('button', { name: 'Prompts' }));
        expect(screen.getByText('Prompt settings')).toBeTruthy();
    });
});
