import { useState, type FormEvent, type KeyboardEvent } from 'react';
import { Button } from '@progress/kendo-react-buttons';
import { Input } from '@progress/kendo-react-inputs';
import type { IUserService } from '../users/IUserService';

interface LoginComponentProps {
    userService: IUserService;
    onLogin: () => void;
}

const LoginComponent = ({ userService, onLogin }: LoginComponentProps) => {
    const [username, setUsername] = useState('');
    const [isRegister, setIsRegister] = useState(false);

    const handleAuth = async () => {
        if (isRegister) {
            await userService.register(username);
        } else {
            await userService.login();
        }
        onLogin();
    };

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        void handleAuth();
    };

    const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
        if (event.key === 'Enter') {
            event.preventDefault();
            void handleAuth();
        }
    };

    return (
        <main className="login-page">
            <section className="login-card" aria-labelledby="login-title">
                <div className="login-card-accent" aria-hidden="true" />
                <header className="login-header">
                    <p className="login-eyebrow">Your personal briefing</p>
                    <h1 id="login-title">AI Curated News</h1>
                    <p className="login-intro">
                        {isRegister
                            ? 'Create your profile to start shaping a news stream around your interests.'
                            : 'Sign in to continue to the stories selected and summarized for you.'}
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
                                onKeyDown={handleKeyDown}
                            />
                            <span className="login-field-hint">This name will identify your curated feed.</span>
                        </div>
                    )}

                    {!isRegister && (
                        <div className="login-security-note">
                            <span className="login-security-dot" aria-hidden="true" />
                            <span>Secure, password-free sign in with your saved passkey.</span>
                        </div>
                    )}

                    <Button
                        className="login-primary-action"
                        type="submit"
                        themeColor="primary"
                        disabled={isRegister && username.trim().length === 0}
                        size="large"
                    >
                        {isRegister ? 'Create account' : 'Login'}
                    </Button>
                </form>

                <div className="login-divider" aria-hidden="true" />

                <p className="login-switch">
                    {isRegister ? 'Already have an account?' : 'New to AI Curated News?'}
                    {' '}
                    <Button
                        className="login-switch-action"
                        fillMode="flat"
                        size="large"
                        onClick={() => setIsRegister((current) => !current)}
                    >
                        {isRegister ? 'Login' : 'Register'}
                    </Button>
                </p>
            </section>
        </main>
    );
};

export default LoginComponent;
