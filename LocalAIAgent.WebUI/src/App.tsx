import { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import reactLogo from './assets/react.svg';
import viteLogo from '/vite.svg';
import './App.css';
import LoginComponent from './components/LoginComponent';
import UserService from './users/UserService';
import NewsComponent from './components/NewsComponent';

const userService = UserService.getInstance();

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
            <NewsComponent />
        </>
    );
};

function App() {
    const [isLoggedIn, setIsLoggedIn] = useState(false);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        const checkLoginStatus = async () => {
            try {
                const loginStatus = await userService.isLoggedIn();
                setIsLoggedIn(loginStatus);
            } finally {
                setIsLoading(false);
            }
        };
        checkLoginStatus();
    }, []);

    const handleLogin = async () => {
        setIsLoggedIn(true);
    };

    if (isLoading) {
        return <div>Loading...</div>;
    }

    return (
        <BrowserRouter>
            <Routes>
                <Route path="/" element={isLoggedIn ? <MainApp /> : <LoginComponent userService={userService} onLogin={handleLogin} />} />
                <Route path="/news" element={<NewsComponent />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App;
