import React, { useEffect, useState } from 'react';
import UserService from '../users/UserService';
import type { CredentialInfo } from '../clients/UserApiClient';
import { Button } from '@progress/kendo-react-buttons';
import { Icon } from '@progress/kendo-react-common';

const AuthenticationSettingsComponent: React.FC = () => {
    
    const [credentials, setCredentials] = useState<CredentialInfo[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const userService = UserService.getInstance();

    const loadCredentials = async () => {
        setIsLoading(true);
        const creds = await userService.getCredentials();
        setCredentials(creds || []);
        setIsLoading(false);
    };

    useEffect(() => {
        loadCredentials();
    }, []);

    const handleRemove = async (id: string) => {
            try {
                await userService.removeCredential(id);
                loadCredentials();
            } catch (error) {
                console.error(error);
            }
    };

    const handleAdd = async () => {
        const user = userService.getCurrentUser();
        if (user) {
            try {
                await userService.addCredential();
                loadCredentials();
            } catch (error) {
                console.error(error);
            }
        }
    };

    return (
        <div>
            <Button themeColor={'info'} onClick={handleAdd} icon='plus-circle'>
                Add Authenticator
                <Icon name="plus-circle" />
                <span className='k-icon k-i-plus-circle'></span>
            </Button>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                <h2>Registered authenticators</h2>
            </div>
            
            {isLoading && credentials.length === 0 ? (
                 <div>Loading...</div>
            ) : credentials.length === 0 ? (
                <p>No authenticators registered.</p>
            ) : (
                <ul style={{ listStyle: 'none', padding: 0 }}>
                    {credentials.map(cred => (
                        <li key={cred.id} style={{ 
                            backgroundColor: '#333', 
                            padding: '15px', 
                            marginBottom: '10px', 
                            borderRadius: '5px',
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center'
                        }}>
                            <div>
                                <div style={{ fontWeight: 'bold', marginBottom: '5px' }}>ID: <span title={cred.id || ''} style={{fontFamily: 'monospace'}}>{cred.id ? (cred.id.length > 20 ? cred.id.substring(0, 20) + '...' : cred.id) : 'Unknown'}</span></div>
                                <div style={{ fontSize: '0.9em', color: '#ccc' }}>Name: {cred.name}</div>
                                {cred.regDate && <div style={{ fontSize: '0.9em', color: '#ccc' }}>Registered: {new Date(cred.regDate).toLocaleDateString()}</div>}
                            </div>
                            <Button themeColor={'error'} onClick={() => cred.id && handleRemove(cred.id)}>
                                Remove
                            </Button>
                        </li>
                    ))}
                </ul>
            )}
        </div>
    );
};

export default AuthenticationSettingsComponent;
