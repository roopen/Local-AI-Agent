import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import AdministrationSettingsComponent from './AdministrationSettingsComponent';
import { AdminService } from '../clients/UserApiClient';

jest.mock('../clients/UserApiClient', () => ({
    AdminService: {
        getApiAdminInvitations: jest.fn(),
        getApiAdminUsers: jest.fn(),
        postApiAdminInvitations: jest.fn(),
        deleteApiAdminInvitations: jest.fn(),
        patchApiAdminUsers: jest.fn(),
    },
}));

const adminApi = AdminService as jest.Mocked<typeof AdminService>;

describe('AdministrationSettingsComponent', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        adminApi.getApiAdminInvitations.mockResolvedValue([{
            id: 1,
            createdAt: '2026-08-17T00:00:00Z',
            expiresAt: '2099-08-24T00:00:00Z',
        }]);
        adminApi.getApiAdminUsers.mockResolvedValue([
            { id: 1, username: 'owner', role: 'Owner', isDisabled: false },
            { id: 2, username: 'member', role: 'Member', isDisabled: false },
        ]);
        adminApi.postApiAdminInvitations.mockResolvedValue({
            id: 2,
            inviteUrl: 'https://news.example.com/register#invite=raw-token',
            expiresAt: '2099-08-24T00:00:00Z',
        });
        adminApi.patchApiAdminUsers.mockResolvedValue(undefined);
    });

    it('creates an invite and manages members without displaying stored tokens', async () => {
        render(<AdministrationSettingsComponent />);

        expect(await screen.findByText('Invitation #1 — Active')).toBeTruthy();
        expect(screen.queryByDisplayValue(/raw-token/)).toBeNull();

        fireEvent.click(screen.getByRole('button', { name: 'Create invitation' }));
        expect(await screen.findByDisplayValue('https://news.example.com/register#invite=raw-token')).toBeTruthy();
        expect(adminApi.postApiAdminInvitations).toHaveBeenCalledTimes(1);

        fireEvent.click(screen.getByRole('button', { name: 'Disable' }));
        await waitFor(() => expect(adminApi.patchApiAdminUsers).toHaveBeenCalledWith(2, { isDisabled: true }));
    });
});
