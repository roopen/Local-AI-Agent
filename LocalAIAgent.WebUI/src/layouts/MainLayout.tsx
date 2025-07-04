import React from 'react';

const Header = () => {
    return (
        <div style={{ marginBottom: 40}}>
            <div style={{ marginLeft: 15 }}>
                <h1 style={{ marginBottom: 1}}>AI Curated News</h1>
            </div>
            <div style={{ marginRight: 15, justifyContent: 'flex-end', padding: '10px' }}>
                <button style={{ marginRight: '10px' }}>Settings</button>
                <button>Logout</button>
            </div>
        </div>
    );
};

const MainLayout: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    return (
        <div>
            <Header />
            <main>{children}</main>
        </div>
    );
};

export default MainLayout;