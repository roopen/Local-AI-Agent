import { useState, type KeyboardEvent } from 'react';
import { Button } from '@progress/kendo-react-buttons';
import { Input } from '@progress/kendo-react-inputs';
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
                <Input
                    type="text"
                    label="Username"
                    value={username}
                    onChange={(e) => setUsername(e.value)}
                    onKeyDown={handleKeyDown}
                />
                <Input
                    type="password"
                    label="Password"
                    value={password}
                    onChange={(e) => setPassword(e.value)}
                    onKeyDown={handleKeyDown}
                />
            </div>

            <hr style={{ marginTop: '3vh', marginBottom: '3vh' }} />

            <Button
                themeColor={'primary'}
                disabled={username.length === 0 || password.length === 0}
                size={'large'}
                style={{ width: "100%" }}
                onClick={handleAuth}>
                {isRegister ? 'Register' : 'Login'}
            </Button>
            <p>
                {isRegister ? 'Already have an account? ' : "Don't have an account? "}
                <Button
                    fillMode={'flat'}
                    size={'large'}
                    onClick={() => setIsRegister(!isRegister)}
                    style={{ background: 'none', border: 'none', color: '#007bff', textDecoration: 'underline', cursor: 'pointer', padding: '0', marginBottom: '4px' }}
                >
                    {isRegister ? 'Login' : 'Register'}
                </Button>
            </p>
        </div>
    );
};

export default LoginComponent;