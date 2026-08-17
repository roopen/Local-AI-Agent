import { useCallback, useEffect, useState } from 'react';
import { Button } from '@progress/kendo-react-buttons';
import { AdminService, type InvitationDto, type ManagedUserDto } from '../clients/UserApiClient';

const inviteStatus = (invite: InvitationDto): string => {
    if (invite.redeemedAt) return `Redeemed${invite.redeemedByUsername ? ` by ${invite.redeemedByUsername}` : ''}`;
    if (invite.revokedAt) return 'Revoked';
    if (invite.expiresAt && new Date(invite.expiresAt) <= new Date()) return 'Expired';
    return 'Active';
};

const AdministrationSettingsComponent = () => {
    const [invitations, setInvitations] = useState<InvitationDto[]>([]);
    const [users, setUsers] = useState<ManagedUserDto[]>([]);
    const [newInviteUrl, setNewInviteUrl] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);

    const refresh = useCallback(async () => {
        try {
            const [loadedInvitations, loadedUsers] = await Promise.all([
                AdminService.getApiAdminInvitations(),
                AdminService.getApiAdminUsers(),
            ]);
            setInvitations(loadedInvitations);
            setUsers(loadedUsers);
            setError(null);
        } catch (failure) {
            setError(failure instanceof Error ? failure.message : 'Unable to load administration settings.');
        } finally {
            setLoading(false);
        }
    }, []);

    // Loading remote administration state is the synchronization performed here.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    useEffect(() => { void refresh(); }, [refresh]);

    const createInvite = async () => {
        try {
            const created = await AdminService.postApiAdminInvitations();
            setNewInviteUrl(created.inviteUrl ?? null);
            await refresh();
        } catch (failure) {
            setError(failure instanceof Error ? failure.message : 'Unable to create invitation.');
        }
    };

    const revokeInvite = async (id: number) => {
        await AdminService.deleteApiAdminInvitations(id);
        await refresh();
    };

    const setDisabled = async (user: ManagedUserDto, isDisabled: boolean) => {
        if (user.id == null) return;
        await AdminService.patchApiAdminUsers(user.id, { isDisabled });
        await refresh();
    };

    if (loading) return <p>Loading administration settings…</p>;

    return (
        <div className="settings-section-stack">
            <section>
                <h2 className="settings-section-title">Invitations</h2>
                <p>Create a single-use invitation that expires after seven days.</p>
                <Button themeColor="primary" onClick={() => void createInvite()}>Create invitation</Button>
                {newInviteUrl && (
                    <div className="admin-invite-result">
                        <label htmlFor="new-invite-url">Share this link once</label>
                        <input id="new-invite-url" readOnly value={newInviteUrl} />
                        <Button onClick={() => void navigator.clipboard.writeText(newInviteUrl)}>Copy link</Button>
                    </div>
                )}
                <div className="admin-list">
                    {invitations.map(invite => {
                        const status = inviteStatus(invite);
                        return (
                            <div className="admin-row" key={invite.id}>
                                <span>Invitation #{invite.id} — {status}</span>
                                <span>Expires {invite.expiresAt ? new Date(invite.expiresAt).toLocaleString() : 'unknown'}</span>
                                {status === 'Active' && invite.id != null && (
                                    <Button fillMode="flat" onClick={() => void revokeInvite(invite.id!)}>Revoke</Button>
                                )}
                            </div>
                        );
                    })}
                    {invitations.length === 0 && <p>No invitations have been created.</p>}
                </div>
            </section>

            <section>
                <h2 className="settings-section-title">Members</h2>
                <div className="admin-list">
                    {users.map(user => (
                        <div className="admin-row" key={user.id}>
                            <span>{user.username} · {user.role}</span>
                            {user.role === 'Owner' ? (
                                <span>Owner</span>
                            ) : (
                                <Button
                                    fillMode="flat"
                                    onClick={() => void setDisabled(user, !user.isDisabled)}
                                >
                                    {user.isDisabled ? 'Enable' : 'Disable'}
                                </Button>
                            )}
                        </div>
                    ))}
                </div>
            </section>

            {error && <p role="alert" className="login-error">{error}</p>}
        </div>
    );
};

export default AdministrationSettingsComponent;
