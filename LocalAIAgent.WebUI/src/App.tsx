import { useState } from 'react';
import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import reactLogo from './assets/react.svg';
import viteLogo from '/vite.svg';
import './App.css';
import ChatComponent from './components/ChatComponent';
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
            <nav>
                <Link to="/news">News</Link>
            </nav>
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
                <Route path="/news" element={<NewsComponent />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App;
