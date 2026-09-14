import { createFileRoute, redirect } from '@tanstack/react-router'

/** The application entry redirects to the public search experience. */
export const Route = createFileRoute('/')({
  beforeLoad: () => {
    throw redirect({ to: '/search' })
  },
})
