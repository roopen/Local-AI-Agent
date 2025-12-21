import React, { useState } from 'react';
import SettingsComponent from '../components/SettingsComponent';
import UserService from '../users/UserService';
import Modal from 'react-modal';
import { Button } from '@progress/kendo-react-buttons';

const Header = () => {
    const [modalIsOpen, setModalIsOpen] = useState(false);

    const openModal = () => {
        setModalIsOpen(true);
    };

    const closeModal = () => {
        setModalIsOpen(false);
    };

    const handleLogout = async () => {
        await UserService.getInstance().logout();
        window.location.reload();
    };

    return (
        <div style={{ marginBottom: 5 }}>
            <div style={{  }}>
                <h1 style={{ marginBottom: 1 }}>AI Curated News</h1>
            </div>
            <div style={{ display: 'flex', flexDirection: 'row', gap: '2vh', margin: 'auto', marginTop: '1vh', marginBottom: '1vh', width: '80%' }}>
                <Button
                    themeColor={'info'}
                    size={'large'}
                    onClick={openModal}
                    style={{ width: "50%" }}>
                    Settings
                </Button>
                <Button
                    fillMode={'outline'}
                    themeColor={'secondary'}
                    size={'large'}
                    style={{ width: "50%" }}
                    onClick={handleLogout}>
                    Logout
                </Button>
            </div>
            <Modal
                isOpen={modalIsOpen}
                onRequestClose={closeModal}
                contentLabel="Settings Modal"
                style={{
                    content: {
                        width: '50%',
                        top: '50%',
                        left: '50%',
                        right: 'auto',
                        bottom: 'auto',
                        marginRight: '-50%',
                        transform: 'translate(-50%, -50%)',
                        backgroundColor: '#282c34',
                        color: 'white',
                        border: '1px solid #ccc',
                        maxHeight: '80vh',
                        overflowY: 'auto'
                    },
                    overlay: {
                        backgroundColor: 'rgba(0, 0, 0, 0.75)'
                    }
                }}
            >
                <SettingsComponent />
                <button onClick={closeModal}>Close</button>
            </Modal>
        </div>
    );
};

const MainLayout: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    return (
        <div>
            <Header />
            <main>{children}</main>
        </div>
    );
};

export default MainLayout;