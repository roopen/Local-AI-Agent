import { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import './App.css';
import LoginComponent from './components/LoginComponent';
import UserService from './users/UserService';
import NewsComponent from './components/NewsComponent';
import MainLayout from './layouts/MainLayout';
import ProtectedRoute from "./ProtectedRoute";
import SetupComponent from './components/SetupComponent'

const userService = UserService.getInstance();

const MainApp = () => {
    return (
        <MainLayout>
            <>
                <NewsComponent />
            </>
        </MainLayout>
    );
};

function App() {
    const [isLoggedIn, setIsLoggedIn] = useState(false);
    const [isUserPreferencesSet, setIsUserPreferencesSet] = useState(false);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        const checkLoginStatus = async () => {
            try {
                const loginStatus = await userService.isLoggedIn();
                setIsLoggedIn(loginStatus);
                if (loginStatus) {
                const currentUser = userService.getCurrentUser();
                const userPreferences = await userService.getUserPreferences(currentUser!.id);

                if (!userPreferences || userPreferences.isEmpty()) {
                    setIsUserPreferencesSet(false);
                }
                else {
                    setIsUserPreferencesSet(true);
                }
            }
            } finally {
                setIsLoading(false);
            }
        };
        checkLoginStatus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
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
                <Route path="/" element={
                <ProtectedRoute condition={isLoggedIn} redirectTo="/login">
                    <ProtectedRoute condition={isUserPreferencesSet} redirectTo="/setup">
                        <MainApp />
                    </ProtectedRoute>
                </ProtectedRoute>}
                />
                <Route path="/news" element={
                    <ProtectedRoute condition={isUserPreferencesSet} redirectTo="/setup">
                        <MainApp />
                    </ProtectedRoute>
                } />
                <Route path="/setup" element={
                <ProtectedRoute condition={(isLoggedIn && !isUserPreferencesSet)} redirectTo="/login">
                    <SetupComponent />
                </ProtectedRoute>
                } />
                <Route path="/login" element={<LoginComponent userService={userService} onLogin={handleLogin} />}/>
            </Routes>
        </BrowserRouter>
    );
}

export default App;
