import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Dev server proxies API + SignalR + uploads to the ASP.NET Core backend.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    open: true
  }
});