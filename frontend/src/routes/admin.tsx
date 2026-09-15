import {
  createFileRoute,
  Link,
  Outlet,
  useRouterState,
} from '@tanstack/react-router'
import { requireAdmin } from '../lib/routeGuards'
import { focusRingClass } from '../components/AppShell'

export const Route = createFileRoute('/admin')({
  beforeLoad: ({ location }) => {
    requireAdmin(location)
  },
  component: AdminLayout,
})

const tabBaseClass =
  'px-3 py-2 font-mono text-[11px] font-semibold uppercase tracking-wider transition-colors rounded'
const tabActiveClass = `${tabBaseClass} bg-primary text-on-primary`
const tabInactiveClass = `${tabBaseClass} text-on-surface-variant hover:bg-surface-container-low hover:text-on-surface`

const tabs = [
  { to: '/admin/providers' as const, label: 'Providers' },
  { to: '/admin/summary' as const, label: 'Summary' },
  { to: '/admin/logs' as const, label: 'Logs' },
  { to: '/admin/settings' as const, label: 'Settings' },
] as const

function AdminLayout() {
  const routerState = useRouterState()
  const currentPath = routerState.location.pathname

  return (
    <section aria-labelledby="admin-heading">
      <h1
        id="admin-heading"
        className="text-2xl font-semibold tracking-tight text-on-surface"
      >
        Admin Console
      </h1>
      <p className="mt-1 text-sm text-on-surface-variant">
        Administrative workflows for provider operations, monitoring, and runtime configuration.
      </p>

      <nav
        aria-label="Admin sections"
        className="mt-4 flex flex-wrap gap-1 border-b border-outline-variant"
      >
        {tabs.map((tab) => {
          const isActive =
            currentPath === tab.to ||
            (tab.to === '/admin/providers' && currentPath === '/admin')
          return (
            <Link
              key={tab.to}
              to={tab.to}
              className={`${isActive ? tabActiveClass : tabInactiveClass} ${focusRingClass} -mb-px`}
              aria-current={isActive ? 'page' : undefined}
            >
              {tab.label}
            </Link>
          )
        })}
      </nav>

      <div className="py-4">
        <Outlet />
      </div>
    </section>
  )
}
