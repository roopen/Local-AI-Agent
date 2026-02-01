import { useState, type KeyboardEvent } from 'react';
import { Button } from '@progress/kendo-react-buttons';
import { Input } from '@progress/kendo-react-inputs';
import type { IUserService } from '../users/IUserService';

interface LoginComponentProps {
    userService: IUserService;
    onLogin: () => void;
}

// eslint-disable-next-line complexity
const LoginComponent = ({ userService, onLogin }: LoginComponentProps) => {
    const [username, setUsername] = useState('');
    const [isRegister, setIsRegister] = useState(false);

    const handleAuth = async () => {
        if (isRegister) {
            await userService.register({ username });
        } else {
            await userService.login();
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
            <h2>AI News Stream</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginBottom: '10px' }}>
                {isRegister ? (<Input
                    type="text"
                    label="Username"
                    value={username}
                    onChange={(e) => setUsername(e.value)}
                    onKeyDown={handleKeyDown}
                />) : null}
            </div>

            <Button
                themeColor={'primary'}
                disabled={username.length === 0 && isRegister}
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