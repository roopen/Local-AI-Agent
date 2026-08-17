import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import LoginComponent from './LoginComponent';
import type { IUserService } from '../users/IUserService';

describe('LoginComponent', () => {
    const mockUserService: IUserService = {
        login: jest.fn(),
        register: jest.fn(),
        getRegistrationStatus: jest.fn().mockResolvedValue({ mode: 'OwnerBootstrap', bootstrapAllowed: true }),
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
    };

    const mockOnLogin = jest.fn();

    beforeEach(() => {
        jest.clearAllMocks();
        (mockUserService.getRegistrationStatus as jest.Mock).mockResolvedValue({
            mode: 'InviteRequired',
            bootstrapAllowed: false,
        });
    });

    it('should call login when Login button is clicked', async () => {
        render(<LoginComponent userService={mockUserService} onLogin={mockOnLogin} />);
        
        const loginButton = screen.getByRole('button', { name: 'Login' });
        fireEvent.click(loginButton);
        
        expect(mockUserService.login).toHaveBeenCalledWith();
        await waitFor(() => expect(mockOnLogin).toHaveBeenCalled());
    });

    it('should bootstrap the owner when registration is allowed', async () => {
        (mockUserService.getRegistrationStatus as jest.Mock).mockResolvedValue({
            mode: 'OwnerBootstrap',
            bootstrapAllowed: true,
        });
        render(<LoginComponent userService={mockUserService} onLogin={mockOnLogin} />);

        const usernameInput = await screen.findByLabelText('Username');
        fireEvent.change(usernameInput, { target: { value: 'newuser' } });
        fireEvent.click(screen.getByRole('button', { name: 'Create account' }));
        
        expect(mockUserService.register).toHaveBeenCalledWith('newuser', undefined);
        await waitFor(() => expect(mockOnLogin).toHaveBeenCalled());
    });
});
