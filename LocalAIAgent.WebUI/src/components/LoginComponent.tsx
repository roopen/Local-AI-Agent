import { useState, type KeyboardEvent } from 'react';
import type { IUserService } from '../users/IUserService';

interface LoginComponentProps {
    userService: IUserService;
    onLogin: () => void;
}

const LoginComponent = ({ userService, onLogin }: LoginComponentProps) => {
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [isRegister, setIsRegister] = useState(false);

    const handleAuth = async () => {
        if (isRegister) {
            await userService.register({ username, password });
        } else {
            await userService.login({ username, password });
        }
        onLogin();
    };

    const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
        if (event.key === 'Enter') {
            handleAuth();
        }
    };

    return (
        <div>
            <h2>{isRegister ? 'Register' : 'Login'}</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginBottom: '10px' }}>
                <input
                    type="text"
                    placeholder="Username"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    onKeyDown={handleKeyDown}
                />
                <input
                    type="password"
                    placeholder="Password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    onKeyDown={handleKeyDown}
                />
            </div>
            <button onClick={handleAuth}>{isRegister ? 'Register' : 'Login'}</button>
            <p>
                {isRegister ? 'Already have an account? ' : "Don't have an account? "}
                <button
                    onClick={() => setIsRegister(!isRegister)}
                    style={{ background: 'none', border: 'none', color: '#007bff', textDecoration: 'underline', cursor: 'pointer', padding: '0' }}
                >
                    {isRegister ? 'Login' : 'Register'}
                </button>
            </p>
        </div>
    );
};

export default LoginComponent;