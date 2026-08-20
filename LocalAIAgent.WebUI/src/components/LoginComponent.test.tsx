import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import LoginComponent from './LoginComponent';
import type { IUserService } from '../users/IUserService';

describe('LoginComponent', () => {
    const mockUserService: IUserService = {
        login: jest.fn(),
        register: jest.fn(),
        getRegistrationStatus: jest.fn().mockResolvedValue({ mode: 'OwnerBootstrap' }),
        logout: jest.fn(),
        getCurrentUser: jest.fn(),
        isLoggedIn: jest.fn(),
        getUserPreferences: jest.fn(),
        saveUserPreferences: jest.fn(),
        getCredentials: jest.fn(),
        removeCredential: jest.fn(),
        addCredential: jest.fn(),
        getAiSettings: jest.fn(),
        saveAiSettings: jest.fn(),
        getLlmOptions: jest.fn(),
        saveLlmOption: jest.fn(),
        deleteLlmOption: jest.fn(),
        selectLlmOption: jest.fn(),
    };

    const mockOnLogin = jest.fn();

    beforeEach(() => {
        jest.clearAllMocks();
        (mockUserService.getRegistrationStatus as jest.Mock).mockResolvedValue({
            mode: 'InviteRequired',
        });
    });

    it('should call login when Login button is clicked', async () => {
        render(<LoginComponent userService={mockUserService} onLogin={mockOnLogin} />);
        
        const loginButton = screen.getByRole('button', { name: 'Login' });
        fireEvent.click(loginButton);
        
        expect(mockUserService.login).toHaveBeenCalledWith();
        await waitFor(() => expect(mockOnLogin).toHaveBeenCalled());
    });

    it('should bootstrap the owner when there are no users', async () => {
        (mockUserService.getRegistrationStatus as jest.Mock).mockResolvedValue({
            mode: 'OwnerBootstrap',
        });
        render(<LoginComponent userService={mockUserService} onLogin={mockOnLogin} />);

        const usernameInput = await screen.findByLabelText('Username');
        fireEvent.change(usernameInput, { target: { value: 'newuser' } });
        fireEvent.click(screen.getByRole('button', { name: 'Create account' }));
        
        expect(mockUserService.register).toHaveBeenCalledWith('newuser', undefined);
        await waitFor(() => expect(mockOnLogin).toHaveBeenCalled());
    });

    it('should require an invitation once an owner exists', async () => {
        render(<LoginComponent userService={mockUserService} onLogin={mockOnLogin} />);

        expect(await screen.findByText('New accounts require an invitation from the owner.')).toBeTruthy();
        expect(screen.queryByRole('button', { name: 'Register' })).toBeNull();
    });
});
