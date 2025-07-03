import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import LoginComponent from './LoginComponent';
import type { IUserService } from '../users/IUserService';

describe('LoginComponent', () => {
    const mockUserService: IUserService = {
        login: jest.fn(),
        register: jest.fn(),
        logout: jest.fn(),
        getCurrentUser: jest.fn(),
        isLoggedIn: jest.fn(),
        getUserPreferences: jest.fn(),
        saveUserPreferences: jest.fn(),
    };

    const mockOnLogin = jest.fn();

    beforeEach(() => {
        jest.clearAllMocks();
    });

    it('should call onLogin when Enter is pressed in username field', async () => {
        render(<LoginComponent userService={mockUserService} onLogin={mockOnLogin} />);
        const usernameInput = screen.getByPlaceholderText('Username');
        fireEvent.keyDown(usernameInput, { key: 'Enter', code: 'Enter', charCode: 13 });
        expect(mockUserService.login).toHaveBeenCalled();
        await waitFor(() => expect(mockOnLogin).toHaveBeenCalled());
    });

    it('should call onLogin when Enter is pressed in password field', async () => {
        render(<LoginComponent userService={mockUserService} onLogin={mockOnLogin} />);
        const passwordInput = screen.getByPlaceholderText('Password');
        fireEvent.keyDown(passwordInput, { key: 'Enter', code: 'Enter', charCode: 13 });
        expect(mockUserService.login).toHaveBeenCalled();
        await waitFor(() => expect(mockOnLogin).toHaveBeenCalled());
    });

    it('should call register when in register mode and Enter is pressed', async () => {
        render(<LoginComponent userService={mockUserService} onLogin={mockOnLogin} />);
        const registerButton = screen.getByText('Register');
        fireEvent.click(registerButton);

        const usernameInput = screen.getByPlaceholderText('Username');
        fireEvent.keyDown(usernameInput, { key: 'Enter', code: 'Enter', charCode: 13 });
        expect(mockUserService.register).toHaveBeenCalled();
        await waitFor(() => expect(mockOnLogin).toHaveBeenCalled());
    });
});