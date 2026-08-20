import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import kendoDarkThemeUrl from '@progress/kendo-theme-default/dist/default-main-dark.css?url'
import kendoLightThemeUrl from '@progress/kendo-theme-default/dist/default-main.css?url'
import './index.css'
import App from './App.tsx'
import Modal from 'react-modal';

const prefersDarkTheme = window.matchMedia('(prefers-color-scheme: dark)').matches;
const kendoTheme = document.createElement('link');
kendoTheme.rel = 'stylesheet';
kendoTheme.href = prefersDarkTheme ? kendoDarkThemeUrl : kendoLightThemeUrl;
document.head.appendChild(kendoTheme);

Modal.setAppElement('#root');

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <App />
    </StrictMode>,
)
