import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@progress/kendo-react-buttons';
import { Input } from '@progress/kendo-react-inputs';
import type { RegistrationStatusDto } from '../clients/UserApiClient';
import type { IUserService } from '../users/IUserService';

interface LoginComponentProps {
    userService: IUserService;
    onLogin: () => void;
}

const readInviteToken = (): string | undefined => {
    const hash = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash;
    return new URLSearchParams(hash).get('invite') ?? undefined;
};

// The conditional copy and controls represent the bootstrap, invite, and login states.
// eslint-disable-next-line complexity
const LoginComponent = ({ userService, onLogin }: LoginComponentProps) => {
    const [username, setUsername] = useState('');
    const [inviteToken] = useState(readInviteToken);
    const [registrationStatus, setRegistrationStatus] = useState<RegistrationStatusDto | null>(null);
    const [isRegister, setIsRegister] = useState(Boolean(inviteToken));
    const [isWorking, setIsWorking] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (window.location.hash) {
            window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}`);
        }

        userService.getRegistrationStatus()
            .then(status => {
                setRegistrationStatus(status);
                if (!inviteToken && status.mode === 'OwnerBootstrap') {
                    setIsRegister(true);
                }
            })
            .catch(() => setRegistrationStatus({ mode: 'InviteRequired' }));
    }, [inviteToken, userService]);

    const canRegister = Boolean(inviteToken || registrationStatus?.mode === 'OwnerBootstrap');

    const handleAuth = async () => {
        setError(null);
        setIsWorking(true);
        try {
            if (isRegister) {
                await userService.register(username.trim(), inviteToken);
            } else {
                await userService.login();
            }
            onLogin();
        } catch (failure) {
            setError(failure instanceof Error ? failure.message : 'Authentication failed.');
        } finally {
            setIsWorking(false);
        }
    };

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        void handleAuth();
    };

    const registrationKind = inviteToken ? 'invited account' : 'owner account';

    return (
        <main className="login-page">
            <section className="login-card" aria-labelledby="login-title">
                <div className="login-card-accent" aria-hidden="true" />
                <header className="login-header">
                    <p className="login-eyebrow">Your personal briefing</p>
                    <h1 id="login-title">AI Curated News</h1>
                    <p className="login-intro">
                        {isRegister
                            ? `Create your ${registrationKind} and secure it with a passkey.`
                            : 'Sign in with your saved passkey to continue.'}
                    </p>
                </header>

                <form className="login-form" onSubmit={handleSubmit}>
                    {isRegister && (
                        <div className="login-field">
                            <Input
                                id="login-username"
                                name="username"
                                type="text"
                                label="Username"
                                autoComplete="username"
                                value={username}
                                onChange={(event) => setUsername(event.value)}
                            />
                            <span className="login-field-hint">This name identifies your private curated feed.</span>
                        </div>
                    )}

                    {!isRegister && (
                        <div className="login-security-note">
                            <span className="login-security-dot" aria-hidden="true" />
                            <span>Secure, password-free sign in with your saved passkey.</span>
                        </div>
                    )}

                    {error && <p role="alert" className="login-error">{error}</p>}

                    <Button
                        className="login-primary-action"
                        type="submit"
                        themeColor="primary"
                        disabled={isWorking || (isRegister && username.trim().length === 0)}
                        size="large"
                    >
                        {isWorking ? 'Please wait…' : isRegister ? 'Create account' : 'Login'}
                    </Button>
                </form>

                {canRegister && (
                    <>
                        <div className="login-divider" aria-hidden="true" />
                        <p className="login-switch">
                            {isRegister ? 'Already have an account?' : 'Have an invitation or setting up the owner?'}{' '}
                            <Button
                                className="login-switch-action"
                                fillMode="flat"
                                size="large"
                                onClick={() => {
                                    setError(null);
                                    setIsRegister(current => !current);
                                }}
                            >
                                {isRegister ? 'Login' : 'Register'}
                            </Button>
                        </p>
                    </>
                )}

                {!canRegister && registrationStatus?.mode === 'InviteRequired' && (
                    <p className="login-field-hint">New accounts require an invitation from the owner.</p>
                )}
            </section>
        </main>
    );
};

export default LoginComponent;
