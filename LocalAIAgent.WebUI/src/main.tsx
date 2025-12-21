import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import Modal from 'react-modal';
import React from 'react';
import { ThemeSwitcherProvider } from "react-css-theme-switcher";

function prefersDarkTheme() {
    console.log('dark theme: ' + window.matchMedia('(prefers-color-scheme: dark)').matches);
    setKendoTheme(window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

export function setTheme(theme: 'light' | 'dark') {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('theme', theme);
}

function setKendoTheme(theme: 'light' | 'dark') {
    const light = document.getElementById("kendo-light") as HTMLLinkElement;
    const dark = document.getElementById("kendo-dark") as HTMLLinkElement;

    if (theme === "light") {
        light.disabled = false;
        dark.disabled = true;
    } else {
        light.disabled = true;
        dark.disabled = false;
    }
}

const themes = {
    light: "https://unpkg.com/@progress/kendo-theme-default@12.3.0/dist/default-main.css",
    dark: "https://unpkg.com/@progress/kendo-theme-default@12.3.0/dist/default-main-dark.css",
};

Modal.setAppElement('#root');

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <ThemeSwitcherProvider themeMap={themes} defaultTheme="dark">
            <div style={{ '--kendo-theme-type': prefersDarkTheme() ? "default-main-dark" : "default-main" } as React.CSSProperties}>
                <App />
            </div>
        </ThemeSwitcherProvider>
    </StrictMode>,
)
