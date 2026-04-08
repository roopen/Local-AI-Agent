import React, { useState, useEffect, useRef } from 'react';
import SettingsComponent from '../components/SettingsComponent';
import UserService from '../users/UserService';
import Modal from 'react-modal';
import { Button } from '@progress/kendo-react-buttons';
import { NewsStreamClient } from '../clients/NewsStreamingClient';

const Header = ({ onSettingsClick, headerRef }: { onSettingsClick: () => void; headerRef: React.RefObject<HTMLDivElement | null> }) => {
    const handleLogout = async () => {
        await UserService.getInstance().logout();
        window.location.reload();
    };

    return (
        <div ref={headerRef} style={{ marginBottom: 5 }}>
            <div style={{  }}>
                <h1 style={{ marginBottom: 1 }}>AI Curated News</h1>
            </div>
            <div style={{
                display: 'flex', flexDirection: 'row', gap: '2vh', margin: 'auto', marginTop: '3vh', marginBottom: '3vh', width: '80%', position: 'sticky', top: '0'}}>
                <Button
                    themeColor={'info'}
                    size={'large'}
                    onClick={onSettingsClick}
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
        </div>
    );
};

const ScrollToTopButton = () => {
    const [visible, setVisible] = useState(false);

    useEffect(() => {
        const handleScroll = () => setVisible(window.scrollY > 200);
        window.addEventListener('scroll', handleScroll);
        return () => window.removeEventListener('scroll', handleScroll);
    }, []);

    if (!visible) return null;

    return (
        <Button
            themeColor={'info'}
            size={'large'}
            onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
            style={{
                borderRadius: '50%',
                width: '3.5rem',
                height: '3.5rem',
                fontSize: '1.5rem',
                lineHeight: 1,
            }}>
            ↑
        </Button>
    );
};

const ScrollToBottomButton = () => {
    const [visible, setVisible] = useState(false);

    useEffect(() => {
        const handleScroll = () => {
            const atBottom = window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 10;
            setVisible(!atBottom);
        };
        handleScroll();
        window.addEventListener('scroll', handleScroll);
        return () => window.removeEventListener('scroll', handleScroll);
    }, []);

    if (!visible) return null;

    return (
        <Button
            themeColor={'info'}
            size={'large'}
            onClick={() => window.scrollTo({ top: document.documentElement.scrollHeight, behavior: 'smooth' })}
            style={{
                borderRadius: '50%',
                width: '3.5rem',
                height: '3.5rem',
                fontSize: '1.5rem',
                lineHeight: 1,
            }}>
            ↓
        </Button>
    );
};

const LoadingSpinner = () => {
    const [isLoading, setIsLoading] = useState(false);
    const [hasLoadedOnce, setHasLoadedOnce] = useState(false);
    const [articleCount, setArticleCount] = useState(0);
    const [elapsedSeconds, setElapsedSeconds] = useState(0);
    const newsStreamClient = NewsStreamClient.getInstance();

    useEffect(() => {
        const interval = setInterval(() => {
            const loading = newsStreamClient.isLoading;
            setIsLoading(loading);
            if (loading) setHasLoadedOnce(true);
            setArticleCount(newsStreamClient.articleCount);

            const start = newsStreamClient.loadStartTime;
            const end = newsStreamClient.loadEndTime;
            if (start) {
                const elapsed = ((end ?? new Date()).getTime() - start.getTime()) / 1000;
                setElapsedSeconds(Math.floor(elapsed));
            }
        }, 100);
        return () => clearInterval(interval);
    }, [newsStreamClient]);

    if (!hasLoadedOnce) return null;

    return (
        <div
            style={{
                position: 'fixed',
                bottom: '2rem',
                right: '6.5rem',
                zIndex: 1000,
                width: '3.5rem',
                height: '3.5rem',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                gap: '0.2rem',
            }}>
            <div style={{ fontSize: '0.6rem', color: '#aaa', textAlign: 'center', lineHeight: 1.2, whiteSpace: 'nowrap', marginBottom: '0.2rem' }}>
                <div>{articleCount} articles</div>
                <div>{elapsedSeconds > 60 ? `${Math.floor(elapsedSeconds / 60)}m ${elapsedSeconds % 60}s` : `${elapsedSeconds}s`}</div>
            </div>
            {isLoading && (
                <>
                    <div
                        style={{
                            width: '2rem',
                            height: '2rem',
                            border: '3px solid rgba(255,255,255,0.3)',
                            borderTop: '3px solid #007bff',
                            borderRadius: '50%',
                            animation: 'spin 0.8s linear infinite',
                        }}
                    />
                    <style>{`
                        @keyframes spin {
                            0% { transform: rotate(0deg); }
                            100% { transform: rotate(360deg); }
                        }
                    `}</style>
                </>
            )}
        </div>
    );
};

const FloatingSettingsButton = ({ onSettingsClick, headerRef }: { onSettingsClick: () => void; headerRef: React.RefObject<HTMLDivElement | null> }) => {
    const [visible, setVisible] = useState(false);

    useEffect(() => {
        const observer = new IntersectionObserver(
            ([entry]) => setVisible(!entry.isIntersecting),
            { threshold: 0 }
        );
        if (headerRef.current) observer.observe(headerRef.current);
        return () => observer.disconnect();
    }, [headerRef]);

    if (!visible) return null;

    return (
        <Button
            themeColor={'info'}
            size={'large'}
            onClick={onSettingsClick}
            style={{
                borderRadius: '50%',
                width: '3.5rem',
                height: '3.5rem',
                fontSize: '1.5rem',
                lineHeight: 1,
            }}>
            ⚙️
        </Button>
    );
};

const MainLayout: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [modalIsOpen, setModalIsOpen] = useState(false);
    const headerRef = useRef<HTMLDivElement>(null);

    const openModal = () => setModalIsOpen(true);
    const closeModal = () => setModalIsOpen(false);

    return (
        <div>
            <Header onSettingsClick={openModal} headerRef={headerRef} />
            <main>{children}</main>
            <LoadingSpinner />
            <div
                style={{
                    position: 'fixed',
                    bottom: '2rem',
                    right: '2rem',
                    zIndex: 1000,
                    display: 'flex',
                    flexDirection: 'column-reverse',
                    gap: '1rem',
                }}
            >
                <ScrollToBottomButton />
                <ScrollToTopButton />
                <FloatingSettingsButton onSettingsClick={openModal} headerRef={headerRef} />
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
            </Modal>
        </div>
    );
};

export default MainLayout;