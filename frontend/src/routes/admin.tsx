import { createFileRoute } from '@tanstack/react-router'
import { requireAdmin } from '../lib/routeGuards'

/**
 * Admin destination entry point. The dedicated admin workflows arrive with the
 * admin frontend change; this route establishes the guarded Admin destination
 * and its role-aware navigation.
 */
export const Route = createFileRoute('/admin')({
  beforeLoad: ({ location }) => {
    requireAdmin(location)
  },
  component: AdminPage,
})

function AdminPage() {
  return (
    <section aria-labelledby="admin-heading">
      <h1 id="admin-heading" className="text-2xl font-bold text-slate-900">
        Admin
      </h1>
      <p className="mt-2 text-slate-700">
        Administrative workflows: provider management, monitoring, and
        operations.
      </p>
    </section>
  )
}
