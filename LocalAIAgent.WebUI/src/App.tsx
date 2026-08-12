import { useState, useEffect, useCallback } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
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
    const [isSetupComplete, setIsSetupComplete] = useState(false);
    const [isLoading, setIsLoading] = useState(true);

    const checkLoginStatus = useCallback(async () => {
        try {
            const loginStatus = await userService.isLoggedIn();
            setIsLoggedIn(loginStatus);
            if (loginStatus) {
                const currentUser = userService.getCurrentUser();
                const [userPreferences, aiSettings] = await Promise.all([
                    userService.getUserPreferences(currentUser!.id),
                    userService.getAiSettings(),
                ]);

                setIsSetupComplete(Boolean(
                    userPreferences
                    && !userPreferences.isEmpty()
                    && aiSettings.isConfigured));
            } else {
                setIsSetupComplete(false);
            }
        } finally {
            setIsLoading(false);
        }
    }, []);

    useEffect(() => {
        checkLoginStatus();
    }, [checkLoginStatus]);

    const handleLogin = async () => {
        await checkLoginStatus();
    };

    if (isLoading) {
        return <div>Loading...</div>;
    }

    return (
        <BrowserRouter>
            <Routes>
                <Route path="/" element={
                    <ProtectedRoute condition={isLoggedIn && !isLoading} redirectTo="/login">
                        <ProtectedRoute condition={isSetupComplete} redirectTo="/setup">
                            <MainApp />
                        </ProtectedRoute>
                    </ProtectedRoute>}
                />
                <Route path="/news" element={
                    <ProtectedRoute condition={isSetupComplete && !isLoading} redirectTo="/setup">
                        <MainApp />
                    </ProtectedRoute>
                } />
                <Route path="/setup" element={
                    <ProtectedRoute condition={(isLoggedIn && !isSetupComplete && !isLoading)} redirectTo="/login">
                        <SetupComponent />
                    </ProtectedRoute>
                } />
                <Route
                    path="/login"
                    element={
                        isLoggedIn
                            ? <Navigate to="/" replace />
                            : <LoginComponent userService={userService} onLogin={handleLogin} />
                    }
                />
            </Routes>
        </BrowserRouter>
    );
}

export default App;
