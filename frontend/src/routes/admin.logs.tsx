import { useState, useCallback, useEffect } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getAdminLogs, ApiError } from '../lib/apiClient'
import type { AdminLogEntry } from '../lib/adminSchemas'
import { adminLogsKey } from '../lib/queryKeys'
import { supportedLogLevels } from '../lib/adminSchemas'
import { LoadingState, EmptyState, ErrorState } from '../components/uiStates'
import { focusRingClass } from '../components/AppShell'

export const Route = createFileRoute('/admin/logs')({
  component: AdminLogsPage,
})

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                  */
/* -------------------------------------------------------------------------- */

function formatTimestamp(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      timeZone: 'UTC',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    })
  } catch {
    return iso
  }
}

function levelBadgeClass(level: string): string {
  switch (level) {
    case 'Error':
    case 'Fatal':
      return 'bg-error-container text-on-error-container'
    case 'Warning':
      return 'bg-warning-container text-on-warning-container'
    case 'Information':
      return 'bg-tertiary/10 text-tertiary'
    default:
      return 'bg-surface-container text-on-surface-variant'
  }
}

/* -------------------------------------------------------------------------- */
/*  Log row                                                                  */
/* -------------------------------------------------------------------------- */

function LogRow({ entry }: { entry: AdminLogEntry }) {
  const [expanded, setExpanded] = useState(false)
  const hasException = !!entry.exception

  return (
    <>
      <tr className="hover:bg-surface-container-low/50">
        <td className="whitespace-nowrap px-3 py-2 font-mono text-[11px] text-on-surface-variant">
          {formatTimestamp(entry.timestamp)}
        </td>
        <td className="px-3 py-2">
          <span
            className={`inline-block rounded px-1.5 py-0.5 font-mono text-[10px] font-semibold uppercase ${levelBadgeClass(entry.level)}`}
          >
            {entry.level}
          </span>
        </td>
        <td className="px-3 py-2 font-body text-[12px] text-on-surface">
          <span className="whitespace-pre-wrap break-words">
            {entry.message}
          </span>
        </td>
        <td className="px-3 py-2 font-mono text-[11px] text-on-surface-variant">
          {entry.correlationId ?? '—'}
        </td>
        <td className="px-3 py-2 text-center">
          {hasException ? (
            <button
              type="button"
              onClick={() => setExpanded(!expanded)}
              className={`rounded bg-error/10 px-1.5 py-0.5 font-mono text-[10px] font-semibold text-error transition-colors hover:bg-error/20 ${focusRingClass}`}
              aria-expanded={expanded}
              aria-label={`${expanded ? 'Hide' : 'Show'} exception for ${entry.message}`}
            >
              {expanded ? 'Hide' : 'Show'}
            </button>
          ) : (
            <span className="text-on-surface-variant">—</span>
          )}
        </td>
      </tr>
      {expanded && entry.exception && (
        <tr className="bg-error-container/30">
          <td colSpan={5} className="px-3 py-3">
            <pre className="max-w-[calc(100vw-3rem)] whitespace-pre-wrap break-all rounded bg-surface-container-low p-3 font-mono text-[11px] leading-relaxed text-on-surface">
              {entry.exception}
            </pre>
          </td>
        </tr>
      )}
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*  Page component                                                           */
/* -------------------------------------------------------------------------- */

function AdminLogsPage() {
  const queryClient = useQueryClient()
  const [count, setCount] = useState(100)
  const [countInput, setCountInput] = useState('100')
  const [level, setLevel] = useState<string>('')
  const [isStale, setIsStale] = useState(false)
  const [countError, setCountError] = useState<string | null>(null)

  const effectiveCount = Math.min(500, Math.max(1, count))
  const effectiveLevel = level || null

  const {
    data: logs,
    isLoading,
    isError,
    error,
    isFetching,
    dataUpdatedAt,
  } = useQuery({
    queryKey: adminLogsKey(effectiveCount, effectiveLevel),
    queryFn: ({ signal }) =>
      getAdminLogs(effectiveCount, effectiveLevel, signal),
  })

  useEffect(() => {
    if (!isFetching && logs && isError) setIsStale(true)
    if (!isFetching && !isError) setIsStale(false)
  }, [isFetching, isError, logs])

  const handleRefresh = useCallback(() => {
    void queryClient.invalidateQueries({
      queryKey: adminLogsKey(effectiveCount, effectiveLevel),
    })
  }, [queryClient, effectiveCount, effectiveLevel])

  const handleCountChange = (value: string) => {
    setCountInput(value)
    const parsed = parseInt(value, 10)
    if (isNaN(parsed) || parsed < 1) {
      setCountError('Minimum count is 1')
      return
    }
    if (parsed > 500) {
      setCountError('Maximum count is 500')
      return
    }
    setCountError(null)
    setCount(parsed)
  }

  return (
    <div>
      {isStale && (
        <div
          role="alert"
          className="mb-4 rounded border border-warning/40 bg-warning-container px-4 py-2 text-sm text-on-warning-container"
        >
          <span className="font-semibold">Stale data</span> — showing last
          successful retrieval.{' '}
          <button
            type="button"
            onClick={handleRefresh}
            className="font-semibold underline"
          >
            Retry
          </button>
        </div>
      )}

      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold text-on-surface">System Logs</h2>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            Structured application logs with level filtering. Newest first.
          </p>
        </div>
        <button
          type="button"
          onClick={handleRefresh}
          disabled={isFetching}
          className={`flex items-center gap-1.5 rounded bg-surface-container px-3 py-1.5 font-mono text-[11px] font-semibold text-primary transition-colors hover:bg-surface-container-high disabled:opacity-50 ${focusRingClass}`}
        >
          <span
            className={`material-symbols-outlined text-[16px] ${isFetching ? 'animate-spin' : ''}`}
          >
            refresh
          </span>
          Refresh
        </button>
      </div>

      {/* Controls */}
      <div className="mt-4 flex flex-wrap items-end gap-4">
        {/* Level filter */}
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
            onChange={(e) => setLevel(e.target.value)}
            className="rounded border border-outline-variant bg-surface-container-lowest px-3 py-1.5 font-body text-[13px] text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          >
            <option value="">All levels</option>
            {supportedLogLevels.map((l) => (
              <option key={l} value={l}>
                {l}
              </option>
            ))}
          </select>
        </div>

        {/* Count */}
        <div className="flex flex-col gap-1">
          <label
            htmlFor="log-count"
            className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
          >
            Count
          </label>
          <input
            id="log-count"
            type="number"
            min={1}
            max={500}
            value={countInput}
            onChange={(e) => handleCountChange(e.target.value)}
            className={`w-24 rounded border bg-surface-container-lowest px-3 py-1.5 font-mono text-[13px] text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary ${
              countError ? 'border-error' : 'border-outline-variant'
            }`}
          />
          {countError && (
            <span className="font-mono text-[10px] text-error">
              {countError}
            </span>
          )}
        </div>
      </div>

      {/* Log table */}
      <div className="mt-4 overflow-x-auto rounded border border-outline-variant bg-surface-container-lowest">
        {isLoading ? (
          <div className="p-4">
            <LoadingState label="Loading logs…" />
          </div>
        ) : isError && !logs ? (
          <div className="p-4">
            <ErrorState
              title="Failed to load logs"
              message={error instanceof ApiError ? error.message : undefined}
              correlationId={
                error instanceof ApiError ? error.correlationId : undefined
              }
              onRetry={handleRefresh}
            />
          </div>
        ) : !logs || logs.length === 0 ? (
          <div className="p-4">
            <EmptyState
              title="No log entries"
              message="No logs match the current filter criteria."
            />
          </div>
        ) : (
          <table className="w-full text-left">
            <thead>
              <tr className="border-b border-outline-variant bg-surface-container-low font-mono text-[10px] uppercase tracking-wider text-on-surface-variant">
                <th className="px-3 py-2 font-medium">Timestamp (UTC)</th>
                <th className="px-3 py-2 font-medium">Level</th>
                <th className="px-3 py-2 font-medium">Message</th>
                <th className="px-3 py-2 font-medium">Correlation ID</th>
                <th className="px-3 py-2 font-medium text-center">Exception</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-container-low">
              {logs.map((entry, i) => (
                <LogRow key={`${entry.timestamp}-${i}`} entry={entry} />
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Metadata */}
      <div className="mt-3 flex items-center justify-between font-mono text-[10px] text-on-surface-variant">
        <span>
          {logs ? logs.length : 0} entries
          {effectiveLevel ? ` at ${effectiveLevel} level` : ''}
        </span>
        <span>
          {dataUpdatedAt > 0
            ? `Retrieved ${new Date(dataUpdatedAt).toLocaleTimeString()}`
            : ''}
          {isFetching && !isLoading ? ' (refreshing…)' : ''}
        </span>
      </div>
    </div>
  )
}
