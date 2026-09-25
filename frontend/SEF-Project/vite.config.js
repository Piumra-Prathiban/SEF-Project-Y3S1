import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.js'],
    css: false,
    // Worker threads start reliably on Windows/OneDrive paths where the
    // default child-process pool times out.
    pool: 'threads',
  },
})
