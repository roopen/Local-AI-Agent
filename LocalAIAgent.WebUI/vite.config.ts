import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import mkcert from 'vite-plugin-mkcert'

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin(), mkcert()],
    server: {
        port: parseInt(process.env.PORT || "53146"),
        proxy: {
            '/api': {
                target: 'https://localhost:7276',
                changeOrigin: true,
                secure: false
            }
        }
    }
})
