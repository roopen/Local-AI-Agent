import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { TextDecoder, TextEncoder } from 'util';

Object.defineProperties(globalThis, {
    TextDecoder: { value: TextDecoder },
    TextEncoder: { value: TextEncoder },
});

const mockUserService = {
    isLoggedIn: jest.fn().mockResolvedValue(true),
    getCurrentUser: jest.fn().mockReturnValue({ id: '1', name: 'test-user' }),
    getUserPreferences: jest.fn().mockResolvedValue({ isEmpty: () => false }),
    getAiSettings: jest.fn().mockResolvedValue({ isConfigured: true }),
};

jest.mock('./users/UserService', () => ({
    __esModule: true,
    default: {
        getInstance: () => mockUserService,
    },
}));
jest.mock('./components/LoginComponent', () => ({
    __esModule: true,
    default: () => <div>Login</div>,
}));
jest.mock('./layouts/MainLayout', () => ({
    __esModule: true,
    default: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));
jest.mock('./components/NewsComponent', () => ({
    __esModule: true,
    default: ({
        onLlmConnectionFailure,
    }: {
        onLlmConnectionFailure: (message: string) => void;
    }) => (
        <button onClick={() => onLlmConnectionFailure('The AI service host could not be resolved.')}>
            Simulate LLM failure
        </button>
    ),
}));
jest.mock('./components/SetupComponent', () => ({
    __esModule: true,
    default: ({ llmConnectionError }: { llmConnectionError?: string }) => (
        <div>Setup: {llmConnectionError}</div>
    ),
}));

let App!: typeof import('./App').default;

beforeAll(async () => {
    App = (await import('./App')).default;
});

describe('App LLM recovery routing', () => {
    beforeEach(() => {
        window.history.replaceState({}, '', '/');
    });

    it('forces the user back to setup when the active LLM connection fails', async () => {
        render(<App />);

        fireEvent.click(await screen.findByRole('button', { name: 'Simulate LLM failure' }));

        await waitFor(() => {
            expect(screen.getByText(
                'Setup: The AI service host could not be resolved.'
            )).toBeTruthy();
        });
        expect(window.location.pathname).toBe('/setup');
    });
});
