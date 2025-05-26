import reactLogo from './assets/react.svg'
import viteLogo from '/vite.svg'
import './App.css'
import ChatComponent from './components/ChatComponent'

function App() {

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
    )
}

export default App
