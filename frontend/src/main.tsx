import { createRoot } from 'react-dom/client'
import { RouterProvider } from '@tanstack/react-router'

import './styles.css'
import { getRouter } from './router'

const rootElement = document.getElementById('root')
if (!rootElement) {
  throw new Error('The #root mount element is missing from index.html.')
}

const router = getRouter()

createRoot(rootElement).render(<RouterProvider router={router} />)
