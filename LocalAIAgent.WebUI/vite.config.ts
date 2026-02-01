import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import mkcert from 'vite-plugin-mkcert'

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin(), mkcert()],
    server: {
        host: 'ainews.dev.localhost',
        port: 8888,
        strictPort: true,
        allowedHosts: ['ainews.dev.localhost'],
        hmr: {
            host: 'ainews.dev.localhost'
        },
        proxy: {
            '/api': {
                target: 'https://apiainews.dev.localhost:7276',
                changeOrigin: true,
                secure: false, // Set to false if using self-signed certs
                ws: true,
            }
        }
    }
})