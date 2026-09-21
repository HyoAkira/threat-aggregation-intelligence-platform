import { lazy, useState } from 'react'
import { ClientOnly, createFileRoute } from '@tanstack/react-router'
import { supportedLogLevels } from '../lib/adminSchemas'
import { focusRingClass } from '../components/AppShell'

export const Route = createFileRoute('/admin/logs')({
  component: AdminLogsPage,
})

const LazyAdminLogsDataTable = lazy(
  () => import('../components/AdminLogsDataTable'),
)

function AdminLogsFallback() {
  return (
    <div
      role="status"
      className="mt-4 rounded border border-outline-variant bg-surface-container-lowest px-4 py-6 font-mono text-xs uppercase tracking-wider text-on-surface-variant"
    >
      Preparing the log table…
    </div>
  )
}

function AdminLogsPage() {
  const [level, setLevel] = useState('')
  const [source, setSource] = useState<'api' | 'worker' | 'all'>('api')
  const [correlationId, setCorrelationId] = useState('')
  const [refreshKey, setRefreshKey] = useState(0)

  return (
    <div>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold text-on-surface">System Logs</h2>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            Structured application logs. Sort the timestamp column and narrow
            the dataset by date range.
          </p>
        </div>
        <button
          type="button"
          onClick={() => setRefreshKey((value) => value + 1)}
          className={`flex items-center gap-1.5 rounded bg-surface-container px-3 py-1.5 font-mono text-[11px] font-semibold text-primary hover:bg-surface-container-high ${focusRingClass}`}
        >
          <span className="material-symbols-outlined text-[16px]">refresh</span>
          Refresh
        </button>
      </div>

      <div className="mt-4 flex flex-wrap items-end gap-4">
        <div className="flex flex-col gap-1">
          <label
            htmlFor="log-source"
            className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
          >
            Source
          </label>
          <select
            id="log-source"
            value={source}
            onChange={(event) => setSource(event.target.value as typeof source)}
            className="rounded border border-outline-variant bg-surface-container-lowest px-3 py-1.5 font-body text-[13px] text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          >
            <option value="api">API logs</option>
            <option value="worker">Worker logs</option>
            <option value="all">API + Worker</option>
          </select>
        </div>
        <div className="flex flex-col gap-1">
          <label
            htmlFor="log-level"
            className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
          >
            Level
          </label>
          <select
            id="log-level"
            value={level}
            onChange={(event) => setLevel(event.target.value)}
            className="rounded border border-outline-variant bg-surface-container-lowest px-3 py-1.5 font-body text-[13px] text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          >
            <option value="">All levels</option>
            {supportedLogLevels.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1">
          <label
            htmlFor="correlation-id"
            className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
          >
            Correlation ID
          </label>
          <input
            id="correlation-id"
            value={correlationId}
            onChange={(event) => setCorrelationId(event.target.value)}
            placeholder="Optional"
            className="w-52 rounded border border-outline-variant bg-surface-container-lowest px-3 py-1.5 font-mono text-[13px] text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          />
        </div>
      </div>

      <ClientOnly fallback={<AdminLogsFallback />}>
        <LazyAdminLogsDataTable
          level={level || null}
          source={source}
          correlationId={correlationId.trim() || null}
          refreshKey={refreshKey}
        />
      </ClientOnly>
    </div>
  )
}
