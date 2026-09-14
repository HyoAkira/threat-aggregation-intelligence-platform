import { defineConfig } from 'vite'
import { devtools } from '@tanstack/devtools-vite'

import { tanstackStart } from '@tanstack/react-start/plugin/vite'

import viteReact from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

const config = defineConfig({
  resolve: { tsconfigPaths: true },
  server: {
    // Development proxy for the API: the frontend calls same-origin `/api/*`
    // and Vite forwards the requests to the local backend.
    proxy: {
      '/api': {
        target: 'http://localhost:5092',
        changeOrigin: true,
      },
    },
  },
  plugins: [devtools(), tailwindcss(), tanstackStart(), viteReact()],
})

export default config
