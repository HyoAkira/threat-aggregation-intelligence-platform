import { devtools } from '@tanstack/devtools-vite'
import { defineConfig } from 'vite'

import { tanstackRouter } from '@tanstack/router-plugin/vite'

import tailwindcss from '@tailwindcss/vite'
import viteReact from '@vitejs/plugin-react'

const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:5092'

const config = defineConfig({
  resolve: { tsconfigPaths: true },
  server: {
    // Development proxy for the API: the frontend calls same-origin `/api/*`
    // and Vite forwards the requests to the local backend.
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
  plugins: [
    devtools(),
    // Generate the type-safe file route tree before React transforms the
    // route modules. `autoCodeSplitting` keeps the existing route-level lazy
    // chunking that the Start plugin previously provided.
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    tailwindcss(),
    viteReact(),
  ],
})

export default config
