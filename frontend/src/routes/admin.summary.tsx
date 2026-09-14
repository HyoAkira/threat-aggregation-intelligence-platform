import { useCallback, useEffect, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getAdminSummary, ApiError } from '../lib/apiClient'
import type { AdminSummary } from '../lib/adminSchemas'
import { adminSummaryKey } from '../lib/queryKeys'
import { LoadingState, ErrorState } from '../components/uiStates'
import { focusRingClass } from '../components/AppShell'

export const Route = createFileRoute('/admin/summary')({
  component: AdminSummaryPage,
})

/* -------------------------------------------------------------------------- */
/*  Metric card                                                              */
/* -------------------------------------------------------------------------- */

function MetricCard({
  label,
  value,
  percentage,
  sublabel,
}: {
  label: string
  value: number
  percentage?: number
  sublabel?: string
}) {
  return (
    <div className="flex flex-col justify-between rounded border border-outline-variant bg-surface-container-lowest p-4">
      <div>
        <span className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
          {label}
        </span>
        <div className="mt-1 text-2xl font-semibold tracking-tight text-on-surface">
          {value.toLocaleString()}
        </div>
      </div>
      {percentage !== undefined && (
        <div className="mt-3">
          <div className="h-1.5 w-full overflow-hidden rounded-full bg-surface-container-low">
            <div
              className="h-full rounded-full bg-primary"
              style={{ width: `${Math.min(100, percentage)}%` }}
            />
          </div>
          <div className="mt-1 flex items-center justify-between">
            <span className="font-mono text-[10px] text-on-surface-variant">
              {sublabel ?? 'of total'}
            </span>
            <span className="font-mono text-[10px] font-bold text-primary">
              {percentage.toFixed(1)}%
            </span>
          </div>
        </div>
      )}
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Page component                                                           */
/* -------------------------------------------------------------------------- */

function AdminSummaryPage() {
  const queryClient = useQueryClient()
  const [isStale, setIsStale] = useState(false)

  const {
    data: summary,
    isLoading,
    isError,
    error,
    isFetching,
    dataUpdatedAt,
  } = useQuery({
    queryKey: adminSummaryKey(),
    queryFn: ({ signal }) => getAdminSummary(signal),
    refetchInterval: 30000,
    refetchIntervalInBackground: false,
  })

  useEffect(() => {
    if (!isFetching && summary && isError) setIsStale(true)
    if (!isFetching && !isError) setIsStale(false)
  }, [isFetching, isError, summary])

  const handleRefresh = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: adminSummaryKey() })
  }, [queryClient])

  if (isLoading) return <LoadingState label="Loading summary…" />

  if (isError && !summary) {
    return (
      <ErrorState
        title="Failed to load summary"
        message={error instanceof ApiError ? error.message : undefined}
        correlationId={
          error instanceof ApiError ? error.correlationId : undefined
        }
        onRetry={handleRefresh}
      />
    )
  }

  if (!summary) return null

  const total = summary.total || 1

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
          <h2 className="text-lg font-semibold text-on-surface">
            Database Summary
          </h2>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            Grouped IOC counts across all configured intelligence providers.
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

      <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricCard
          label="IPv4 Threat Vectors"
          value={summary.ipv4}
          percentage={(summary.ipv4 / total) * 100}
          sublabel="of total DB"
        />
        <MetricCard
          label="IPv6 Threat Vectors"
          value={summary.ipv6}
          percentage={(summary.ipv6 / total) * 100}
          sublabel="of total DB"
        />
        <MetricCard
          label="Host & Domain IOCs"
          value={summary.domain}
          percentage={(summary.domain / total) * 100}
          sublabel="of total DB"
        />
        <MetricCard
          label="Payload URLs"
          value={summary.url}
          percentage={(summary.url / total) * 100}
          sublabel="of total DB"
        />
      </div>

      {/* Total row */}
      <div className="mt-4 rounded border border-outline-variant bg-surface-container-lowest p-4">
        <div className="flex items-center justify-between">
          <span className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
            Total Indicators
          </span>
          <span className="text-2xl font-semibold tracking-tight text-on-surface">
            {summary.total.toLocaleString()}
          </span>
        </div>
      </div>

      {/* Metadata */}
      <div className="mt-3 flex items-center justify-between font-mono text-[10px] text-on-surface-variant">
        <span>
          Data represents exact API counts without client-side recomputation.
        </span>
        <span>
          {dataUpdatedAt > 0
            ? `Retrieved ${new Date(dataUpdatedAt).toLocaleTimeString()}`
            : ''}
          {isFetching ? ' (refreshing…)' : ''}
        </span>
      </div>
    </div>
  )
}
