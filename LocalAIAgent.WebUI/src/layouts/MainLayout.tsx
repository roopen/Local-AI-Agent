import React, { useState, useEffect, useRef } from 'react';
import SettingsComponent from '../components/SettingsComponent';
import UserService from '../users/UserService';
import Modal from 'react-modal';
import { Button } from '@progress/kendo-react-buttons';
import { NewsStreamClient } from '../clients/NewsStreamingClient';

const newsStreamClient = NewsStreamClient.getInstance();

const Header = ({ onSettingsClick, headerRef }: { onSettingsClick: () => void; headerRef: React.RefObject<HTMLDivElement | null> }) => {
    const handleLogout = async () => {
        await UserService.getInstance().logout();
        window.location.reload();
    };

    const [isLoading, setIsLoading] = useState(false);

    useEffect(() => {
        const interval = setInterval(() => {
            const loading = newsStreamClient.isLoading;
            setIsLoading(loading);
        }, 100);
        return () => clearInterval(interval);
    }, []);

    return (
        <div ref={headerRef} style={{ width: '80%', margin: 'auto', marginBottom: 5 }}>
            <div style={{ width: '100%', display: 'flex', alignItems: 'center', gap: '1rem' }}>
                <h1 className={isLoading ? 'heading-loading' : ''} style={{ margin: 0 }}>AI Curated News</h1>
                <div style={{ display: 'flex', marginLeft: 'auto' }}>
                    <button className="header-btn header-btn--left" title="Settings" onClick={onSettingsClick}>
                        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24" style={{ display: 'block' }}><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>
                    </button>
                    <button className="header-btn header-btn--right" title="Logout" onClick={handleLogout}>⏻</button>
                </div>
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
            themeColor={'primary'}
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
            themeColor={'primary'}
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
    const [hasLoadedOnce, setHasLoadedOnce] = useState(false);
    const [articleCount, setArticleCount] = useState(0);
    const [elapsedSeconds, setElapsedSeconds] = useState(0);
    const newsStreamClient = NewsStreamClient.getInstance();

    useEffect(() => {
        const interval = setInterval(() => {
            const loading = newsStreamClient.isLoading;
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
        </div>
    );
};

const FloatingLoadingBar = () => {
    const [isLoading, setIsLoading] = useState(false);

    useEffect(() => {
        const interval = setInterval(() => {
            setIsLoading(newsStreamClient.isLoading);
        }, 100);
        return () => clearInterval(interval);
    }, []);

    if (!isLoading) return null;

    return <div className="loading-bar" />;
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
            themeColor={'primary'}
            size={'large'}
            onClick={onSettingsClick}
            style={{
                borderRadius: '50%',
                width: '3.5rem',
                height: '3.5rem',
                fontSize: '1.5rem',
                lineHeight: 1,
            }}>
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24" style={{ display: 'block' }}><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>
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
            <FloatingLoadingBar />
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
                        backgroundColor: '#121214',
                        color: 'white',
                        border: '1px solid #27272a',
                        maxHeight: '80vh',
                        overflowY: 'auto',
                        padding: 0,
                        borderRadius: 8,
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