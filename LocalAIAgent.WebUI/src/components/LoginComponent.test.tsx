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

    it('should call login when Login button is clicked', async () => {
        render(<LoginComponent userService={mockUserService} onLogin={mockOnLogin} />);
        
        const loginButton = screen.getByRole('button', { name: 'Login' });
        fireEvent.click(loginButton);
        
        expect(mockUserService.login).toHaveBeenCalledWith();
        await waitFor(() => expect(mockOnLogin).toHaveBeenCalled());
    });

    it('should call register when in register mode and Enter is pressed', async () => {
        render(<LoginComponent userService={mockUserService} onLogin={mockOnLogin} />);

        // Toggle to register mode
        const registerToggle = screen.getByText('Register');
        fireEvent.click(registerToggle);

        const usernameInput = screen.getByLabelText('Username');
        fireEvent.change(usernameInput, { target: { value: 'newuser' } });
        fireEvent.keyDown(usernameInput, { key: 'Enter', code: 'Enter', charCode: 13 });
        
        expect(mockUserService.register).toHaveBeenCalledWith('newuser');
        await waitFor(() => expect(mockOnLogin).toHaveBeenCalled());
    });
});