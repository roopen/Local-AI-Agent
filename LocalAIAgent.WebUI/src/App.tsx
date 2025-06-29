import { useEffect } from 'react';
import { BrowserRouter, Routes, Route, useNavigate } from 'react-router-dom';
import reactLogo from './assets/react.svg';
import viteLogo from '/vite.svg';
import './App.css';
import ChatComponent from './components/ChatComponent';
import Settings from './components/Settings';
import { SettingsPageUrl } from './components/Settings';
import { userSettingsService } from './services/UserSettingsService';

const MainApp = () => {
    return (
        <>
            <div>
                <a href="https://vite.dev" target="_blank">
                    <img src={viteLogo} className="logo" alt="Vite logo" />
                </a>
                <a href="https://react.dev" target="_blank">
                    <img src={reactLogo} className="logo react" alt="React logo" />
                </a>
            </div>
            <ChatComponent />
        </>
    );
};

const RedirectManager = () => {
    const navigate = useNavigate();

    useEffect(() => {
        if (!userSettingsService.settingsExist()) {
            navigate(SettingsPageUrl());
        }
    }, [navigate]);

    return null;
};


function App() {
    return (
        <BrowserRouter>
            <RedirectManager />
            <Routes>
                <Route path="/" element={<MainApp />} />
                <Route path="/settings" element={<Settings />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App;
