import { useState } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import reactLogo from './assets/react.svg';
import viteLogo from '/vite.svg';
import './App.css';
import ChatComponent from './components/ChatComponent';
import LoginComponent from './components/LoginComponent';
import UserService from './users/UserService';

const userService = new UserService();

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

function App() {
    const [isLoggedIn, setIsLoggedIn] = useState(userService.isLoggedIn());

    const handleLogin = () => {
        setIsLoggedIn(true);
    };

    return (
        <BrowserRouter>
            <Routes>
                <Route path="/" element={isLoggedIn ? <MainApp /> : <LoginComponent userService={userService} onLogin={handleLogin} />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App;
