import { useState, useCallback, useEffect, useRef } from 'react'
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { z } from 'zod'
import {
  getAdminProviders,
  getAdminProviderHistory,
  triggerDefaultFetch,
  triggerFullFetch,
  getFetchJobStatus,
  ApiError,
} from '../lib/apiClient'
import type { AdminProviderDashboardItem } from '../lib/adminSchemas'
import {
  adminProviderListKey,
  adminHistoryKey,
  adminSummaryKey,
} from '../lib/queryKeys'
import { LoadingState, EmptyState, ErrorState } from '../components/uiStates'
import { focusRingClass } from '../components/AppShell'
import { parseUtcDateTime, formatLocalMonthDayTime } from '../lib/format'

const providerSearchSchema = z.object({
  providerId: z.coerce.number().int().positive().optional(),
  page: z.coerce.number().int().positive().optional(),
})

export const Route = createFileRoute('/admin/providers')({
  validateSearch: (search) => providerSearchSchema.parse(search),
  component: AdminProvidersPage,
})

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                  */
/* -------------------------------------------------------------------------- */

function relativeTime(iso: string | null): string {
  if (!iso) return 'never'
  const diff = Date.now() - parseUtcDateTime(iso).getTime()
  if (diff < 0) return 'in the future'
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs}h ago`
  const days = Math.floor(hrs / 24)
  return `${days}d ago`
}

function isTerminal(status: string): boolean {
  return status === 'success' || status === 'failed'
}

function formatDuration(startedAt: string, finishedAt: string | null): string {
  if (!finishedAt) return '—'
  const ms =
    parseUtcDateTime(finishedAt).getTime() -
    parseUtcDateTime(startedAt).getTime()
  if (ms < 1000) return `${ms}ms`
  const secs = Math.floor(ms / 1000)
  if (secs < 60) return `${secs}s`
  const mins = Math.floor(secs / 60)
  return `${mins}m ${secs % 60}s`
}

/* -------------------------------------------------------------------------- */
/*  Status badge                                                             */
/* -------------------------------------------------------------------------- */

function StatusBadge({ status }: { status: string | null }) {
  if (!status) {
    return (
      <span className="inline-flex items-center gap-1 rounded bg-surface-container px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
        Never Run
      </span>
    )
  }
  const normalized = status.toLowerCase()
  const isRateLimit = status === '429' || normalized.includes('rate')
  const isFailed =
    normalized === 'failed' ||
    status.startsWith('5') ||
    normalized.includes('error')

  if (isRateLimit) {
    return (
      <span className="inline-flex items-center gap-1 rounded bg-error-container px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-on-error-container">
        <span className="h-1.5 w-1.5 rounded-full bg-error" />
        {status}
      </span>
    )
  }
  if (isFailed) {
    return (
      <span className="inline-flex items-center gap-1 rounded bg-error-container px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-on-error-container">
        <span className="h-1.5 w-1.5 rounded-full bg-error" />
        {status}
      </span>
    )
  }
  return (
    <span className="inline-flex items-center gap-1 rounded bg-tertiary/10 px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-tertiary">
      <span className="h-1.5 w-1.5 rounded-full bg-tertiary" />
      {status}
    </span>
  )
}

/* -------------------------------------------------------------------------- */
/*  Fetch mutation hook                                                      */
/* -------------------------------------------------------------------------- */

function useFetchMutation(providerId: number) {
  const queryClient = useQueryClient()
  const [activeJob, setActiveJob] = useState<{
    jobId: string
    status: string
  } | null>(null)
  const [fetchError, setFetchError] = useState<string | null>(null)
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const invalidateAfterFetch = useCallback(
    (pId: number) => {
      queryClient.invalidateQueries({ queryKey: adminProviderListKey() })
      queryClient.invalidateQueries({
        queryKey: ['admin', 'providers', pId, 'history'],
      })
      queryClient.invalidateQueries({ queryKey: adminSummaryKey() })
    },
    [queryClient],
  )

  const stopPolling = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current)
      pollRef.current = null
    }
  }, [])

  const pollJob = useCallback(
    (jobId: string, pId: number) => {
      stopPolling()
      pollRef.current = setInterval(async () => {
        try {
          const job = await getFetchJobStatus(jobId)
          setActiveJob({ jobId: job.jobId, status: job.status })
          if (isTerminal(job.status)) {
            stopPolling()
            setActiveJob(null)
            invalidateAfterFetch(pId)
          }
        } catch (error) {
          stopPolling()
          setActiveJob(null)
          setFetchError(
            error instanceof ApiError
              ? error.message
              : 'Unable to refresh the fetch job status.',
          )
        }
      }, 2000)
    },
    [stopPolling, invalidateAfterFetch],
  )

  const defaultMutation = useMutation({
    mutationFn: () => triggerDefaultFetch(providerId),
    onSuccess: (data) => {
      setFetchError(null)
      setActiveJob(data)
      pollJob(data.jobId, providerId)
    },
    onError: (error: unknown) => {
      if (error instanceof ApiError && error.status === 409) {
        void queryClient.invalidateQueries({
          queryKey: adminProviderListKey(),
        })
      }
      if (error instanceof ApiError) {
        setFetchError(error.message)
      } else {
        setFetchError('An unexpected error occurred.')
      }
    },
  })

  const fullMutation = useMutation({
    mutationFn: () => triggerFullFetch(providerId),
    onSuccess: (data) => {
      setFetchError(null)
      setActiveJob(data)
      pollJob(data.jobId, providerId)
    },
    onError: (error: unknown) => {
      if (error instanceof ApiError && error.status === 409) {
        void queryClient.invalidateQueries({
          queryKey: adminProviderListKey(),
        })
      }
      if (error instanceof ApiError) {
        setFetchError(error.message)
      } else {
        setFetchError('An unexpected error occurred.')
      }
    },
  })

  useEffect(() => {
    return () => stopPolling()
  }, [stopPolling])

  return {
    triggerDefault: defaultMutation.mutate,
    triggerFull: fullMutation.mutate,
    isPending: defaultMutation.isPending || fullMutation.isPending,
    activeJob,
    fetchError,
    clearError: () => setFetchError(null),
  }
}

/* -------------------------------------------------------------------------- */
/*  Error detail dialog                                                      */
/* -------------------------------------------------------------------------- */

function ErrorDetailDialog({
  error,
  onClose,
}: {
  error: string
  onClose: () => void
}) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-inverse-surface/40"
      role="dialog"
      aria-modal="true"
      aria-label="Error detail"
    >
      <div className="mx-4 flex max-h-[80vh] w-full max-w-2xl flex-col rounded border border-outline-variant bg-surface-container-lowest shadow-lg">
        <div className="flex items-center justify-between border-b border-outline-variant px-4 py-3">
          <h3 className="text-sm font-semibold text-on-surface">
            Error Detail
          </h3>
          <button
            type="button"
            onClick={onClose}
            className={`rounded p-1 text-on-surface-variant hover:bg-surface-container-low ${focusRingClass}`}
            aria-label="Close"
          >
            <span className="material-symbols-outlined text-[18px]">close</span>
          </button>
        </div>
        <div className="flex-1 overflow-auto p-4">
          <pre className="whitespace-pre-wrap break-words font-mono text-[12px] leading-relaxed text-on-surface">
            {error}
          </pre>
        </div>
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  History panel                                                            */
/* -------------------------------------------------------------------------- */

function HistoryPanel({
  providerId,
  providerName,
  page,
  onPageChange,
  onClose,
}: {
  providerId: number
  providerName: string
  page: number
  onPageChange: (page: number) => void
  onClose: () => void
}) {
  const [inspectingError, setInspectingError] = useState<string | null>(null)
  const pageSize = 20

  const { data, isLoading, isError, error } = useQuery({
    queryKey: adminHistoryKey(providerId, page, pageSize),
    queryFn: ({ signal }) =>
      getAdminProviderHistory(providerId, page, pageSize, signal),
  })

  const totalPages = data
    ? Math.max(1, Math.ceil(data.totalCount / pageSize))
    : 0

  return (
    <div className="mt-6 rounded border border-outline-variant bg-surface-container-lowest">
      <div className="flex items-center justify-between border-b border-outline-variant px-4 py-3">
        <h3 className="font-semibold text-on-surface">
          Fetch History — {providerName}
        </h3>
        <button
          type="button"
          onClick={onClose}
          className={`rounded p-1 text-on-surface-variant hover:bg-surface-container-low ${focusRingClass}`}
          aria-label="Close history"
        >
          <span className="material-symbols-outlined text-[18px]">close</span>
        </button>
      </div>

      {isLoading ? (
        <div className="p-4">
          <LoadingState label="Loading history…" />
        </div>
      ) : isError ? (
        <div className="p-4">
          <ErrorState
            title="Failed to load history"
            message={error instanceof ApiError ? error.message : undefined}
            correlationId={
              error instanceof ApiError ? error.correlationId : undefined
            }
          />
        </div>
      ) : !data || data.items.length === 0 ? (
        <div className="p-4">
          <EmptyState
            title="No fetch history"
            message="This provider has not been fetched yet."
          />
        </div>
      ) : (
        <>
          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-outline-variant bg-surface-container-low font-mono text-[10px] uppercase tracking-wider text-on-surface-variant">
                  <th className="px-4 py-2 font-medium">Job</th>
                  <th className="px-4 py-2 font-medium">Trigger</th>
                  <th className="px-4 py-2 font-medium">Mode</th>
                  <th className="px-4 py-2 font-medium">Status</th>
                  <th className="px-4 py-2 font-medium">Started</th>
                  <th className="px-4 py-2 font-medium">Finished</th>
                  <th className="px-4 py-2 font-medium">Duration</th>
                  <th className="px-4 py-2 font-medium text-right">Added</th>
                  <th className="px-4 py-2 font-medium text-right">Updated</th>
                  <th className="px-4 py-2 font-medium">Error</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-surface-container-low font-body text-[12px]">
                {data.items.map((item) => (
                  <tr
                    key={item.jobId}
                    className="hover:bg-surface-container-low/50"
                  >
                    <td
                      className="px-4 py-2 font-mono text-[11px] text-on-surface-variant"
                      title={item.jobId}
                    >
                      {item.jobId.slice(0, 8)}…
                    </td>
                    <td className="px-4 py-2">
                      <span className="rounded bg-surface-container px-1.5 py-0.5 font-mono text-[10px] font-semibold uppercase text-on-surface-variant">
                        {item.trigger}
                      </span>
                    </td>
                    <td className="px-4 py-2">
                      <span className="rounded bg-surface-container px-1.5 py-0.5 font-mono text-[10px] font-semibold uppercase text-primary">
                        {item.mode}
                      </span>
                    </td>
                    <td className="px-4 py-2">
                      <StatusBadge status={item.status} />
                    </td>
                    <td className="px-4 py-2 font-mono text-[11px] text-on-surface-variant">
                      {formatLocalMonthDayTime(item.startedAt) ?? '—'}
                    </td>
                    <td className="px-4 py-2 font-mono text-[11px] text-on-surface-variant">
                      {formatLocalMonthDayTime(item.finishedAt) ?? '—'}
                    </td>
                    <td className="px-4 py-2 font-mono text-[11px] text-on-surface-variant">
                      {formatDuration(item.startedAt, item.finishedAt)}
                    </td>
                    <td className="px-4 py-2 text-right font-mono text-[11px] text-tertiary">
                      +{item.recordsAdded}
                    </td>
                    <td className="px-4 py-2 text-right font-mono text-[11px] text-tertiary">
                      +{item.recordsUpdated}
                    </td>
                    <td className="px-4 py-2">
                      {item.errorDetail ? (
                        <button
                          type="button"
                          onClick={() => setInspectingError(item.errorDetail)}
                          className={`rounded bg-error/10 px-1.5 py-0.5 font-mono text-[10px] font-semibold text-error transition-colors hover:bg-error/20 ${focusRingClass}`}
                        >
                          Inspect
                        </button>
                      ) : (
                        <span className="text-on-surface-variant">—</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between border-t border-outline-variant px-4 py-2">
              <span className="font-mono text-[10px] text-on-surface-variant">
                Page {data.page} of {totalPages} ({data.totalCount} entries)
              </span>
              <div className="flex gap-1">
                <button
                  type="button"
                  onClick={() => onPageChange(Math.max(1, page - 1))}
                  disabled={page <= 1}
                  className={`rounded border border-outline-variant bg-surface-container-lowest px-2 py-1 font-mono text-[10px] font-semibold text-on-surface transition-colors hover:bg-surface-container-low disabled:cursor-not-allowed disabled:opacity-50 ${focusRingClass}`}
                >
                  Prev
                </button>
                <button
                  type="button"
                  onClick={() => onPageChange(Math.min(totalPages, page + 1))}
                  disabled={page >= totalPages}
                  className={`rounded border border-outline-variant bg-surface-container-lowest px-2 py-1 font-mono text-[10px] font-semibold text-on-surface transition-colors hover:bg-surface-container-low disabled:cursor-not-allowed disabled:opacity-50 ${focusRingClass}`}
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {/* Error inspection dialog */}
      {inspectingError && (
        <ErrorDetailDialog
          error={inspectingError}
          onClose={() => setInspectingError(null)}
        />
      )}
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*  Provider row                                                             */
/* -------------------------------------------------------------------------- */

function ProviderRow({
  provider,
  isSelected,
}: {
  provider: AdminProviderDashboardItem
  isSelected: boolean
}) {
  const [showFullConfirm, setShowFullConfirm] = useState(false)
  const {
    triggerDefault,
    triggerFull,
    isPending,
    activeJob,
    fetchError,
    clearError,
  } = useFetchMutation(provider.id)

  const hasFull = provider.supportedModes.includes('full')
  const isNeverRun = !provider.lastFetchStatus && !provider.lastFetchAt

  return (
    <>
      <tr
        className={`transition-colors ${
          !provider.isEnabled
            ? 'opacity-60'
            : isSelected
              ? 'bg-primary/5'
              : 'hover:bg-surface-container-low/50'
        }`}
      >
        {/* Provider name */}
        <td className="px-4 py-3">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[18px] text-primary">
              security
            </span>
            <div>
              <div className="font-semibold text-on-surface">
                {provider.name}
              </div>
              <div className="font-mono text-[11px] text-on-surface-variant">
                {provider.baseUrl}
              </div>
            </div>
          </div>
        </td>

        {/* Schedule */}
        <td className="px-4 py-3 font-mono text-[12px] text-on-surface">
          <div>
            Every {provider.intervalMinutes}m
            <span className="ml-1 text-on-surface-variant">
              ({provider.isEnabled ? 'active' : 'paused'})
            </span>
          </div>
          <div className="mt-1 text-[10px] text-on-surface-variant">
            Next:{' '}
            {provider.nextFetchTime
              ? formatLocalMonthDayTime(provider.nextFetchTime)
              : 'not scheduled'}
          </div>
          <div className="mt-1 text-[10px] text-on-surface-variant">
            Minimum: {provider.minimumIntervalMinutes}m
          </div>
        </td>

        {/* Last fetch */}
        <td className="px-4 py-3 font-mono text-[12px] text-on-surface">
          {isNeverRun ? (
            <span className="text-on-surface-variant">Never run</span>
          ) : (
            <>
              <div>{relativeTime(provider.lastFetchAt)}</div>
              <div className="text-[10px] text-on-surface-variant">
                {formatLocalMonthDayTime(provider.lastFetchAt) ?? '—'}
              </div>
            </>
          )}
        </td>

        {/* Status */}
        <td className="px-4 py-3">
          <StatusBadge status={provider.lastFetchStatus} />
          <div className="mt-2 flex flex-wrap gap-1">
            {provider.supportedModes.map((mode) => (
              <span
                key={mode}
                className={`rounded px-1.5 py-0.5 font-mono text-[10px] ${
                  mode === provider.defaultMode
                    ? 'bg-primary/10 font-semibold text-primary'
                    : 'bg-surface-container text-on-surface-variant'
                }`}
                title={
                  mode === 'recent'
                    ? provider.recentCooldownMinutes
                      ? `${provider.recentCooldownMinutes}-minute cooldown`
                      : 'No cooldown'
                    : provider.fullCooldownMinutes
                      ? `${provider.fullCooldownMinutes}-minute cooldown`
                      : 'No cooldown'
                }
              >
                {mode}
                {mode === provider.defaultMode ? ' · default' : ''}
              </span>
            ))}
          </div>
          <div className="mt-1 font-mono text-[10px] text-on-surface-variant">
            Cooldowns:{' '}
            {provider.supportedModes
              .map((mode) => {
                const minutes =
                  mode === 'recent'
                    ? provider.recentCooldownMinutes
                    : provider.fullCooldownMinutes
                return `${mode} ${minutes ? `${minutes}m` : 'none'}`
              })
              .join(' · ')}
          </div>
        </td>

        {/* Enabled toggle */}
        <td className="px-4 py-3 text-center">
          <span
            className={`inline-block h-5 w-9 rounded-full transition-colors ${
              provider.isEnabled ? 'bg-primary' : 'bg-surface-container-high'
            }`}
            aria-label={provider.isEnabled ? 'Enabled' : 'Disabled'}
            role="switch"
            aria-checked={provider.isEnabled}
          >
            <span
              className={`block h-4 w-4 rounded-full bg-white transition-transform ${
                provider.isEnabled ? 'translate-x-4' : 'translate-x-0.5'
              } mt-0.5`}
            />
          </span>
        </td>

        {/* Operations */}
        <td className="px-4 py-3 text-right">
          <div className="flex items-center justify-end gap-2">
            {hasFull && (
              <button
                type="button"
                onClick={() => setShowFullConfirm(true)}
                disabled={isPending || !provider.isEnabled}
                className={`rounded bg-surface-container px-2.5 py-1 font-mono text-[11px] font-semibold text-primary transition-colors hover:bg-surface-container-high disabled:cursor-not-allowed disabled:opacity-50 ${focusRingClass}`}
              >
                Full Fetch
              </button>
            )}
            <button
              type="button"
              onClick={() => triggerDefault()}
              disabled={isPending || !provider.isEnabled}
              className={`rounded bg-primary px-2.5 py-1 font-mono text-[11px] font-semibold text-on-primary transition-colors hover:bg-primary-container disabled:cursor-not-allowed disabled:opacity-50 ${focusRingClass}`}
            >
              {isPending ? 'Fetching…' : 'Fetch Now'}
            </button>
            <Link
              to="/admin/providers"
              search={{ providerId: provider.id, page: 1 }}
              className={`rounded bg-surface-container px-2.5 py-1 font-mono text-[11px] font-semibold text-on-surface-variant transition-colors hover:bg-surface-container-high ${focusRingClass}`}
            >
              History
            </Link>
          </div>

          {activeJob && (
            <div className="mt-2 text-right font-mono text-[10px] text-primary">
              <span className="inline-block h-1.5 w-1.5 animate-pulse rounded-full bg-primary" />
              Job {activeJob.jobId.slice(0, 8)}… {activeJob.status}
            </div>
          )}

          {fetchError && (
            <div className="mt-2 text-right">
              <span className="font-mono text-[10px] text-error">
                {fetchError}
              </span>
              <button
                type="button"
                onClick={clearError}
                className="ml-1 text-[10px] text-on-surface-variant underline"
              >
                dismiss
              </button>
            </div>
          )}

          {showFullConfirm && (
            <div
              className="fixed inset-0 z-50 flex items-center justify-center bg-inverse-surface/40"
              role="dialog"
              aria-modal="true"
              aria-label="Confirm full fetch"
            >
              <div className="mx-4 w-full max-w-md rounded border border-outline-variant bg-surface-container-lowest p-6 shadow-lg">
                <h3 className="text-lg font-semibold text-on-surface">
                  Confirm Full Fetch
                </h3>
                <p className="mt-2 text-sm text-on-surface-variant">
                  This will trigger a <strong>full</strong> fetch for{' '}
                  <strong>{provider.name}</strong>. This operation may take
                  longer than a default fetch
                  {provider.fullCooldownMinutes
                    ? ` and has a ${provider.fullCooldownMinutes}-minute cooldown.`
                    : '.'}
                </p>
                <div className="mt-4 flex justify-end gap-2">
                  <button
                    type="button"
                    onClick={() => setShowFullConfirm(false)}
                    className={`rounded border border-outline-variant bg-surface-container-lowest px-3 py-1.5 text-sm font-medium text-on-surface transition-colors hover:bg-surface-container-low ${focusRingClass}`}
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setShowFullConfirm(false)
                      triggerFull()
                    }}
                    className={`rounded bg-primary px-3 py-1.5 text-sm font-medium text-on-primary transition-colors hover:bg-primary-container ${focusRingClass}`}
                  >
                    Confirm Full Fetch
                  </button>
                </div>
              </div>
            </div>
          )}
        </td>
      </tr>
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*  Page component                                                           */
/* -------------------------------------------------------------------------- */

function AdminProvidersPage() {
  const { providerId, page = 1 } = Route.useSearch()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [isStale, setIsStale] = useState(false)

  const {
    data: providers,
    isLoading,
    isError,
    error,
    isFetching,
    dataUpdatedAt,
  } = useQuery({
    queryKey: adminProviderListKey(),
    queryFn: ({ signal }) => getAdminProviders(signal),
    refetchInterval: 15000,
    refetchIntervalInBackground: false,
  })

  useEffect(() => {
    if (!isFetching && providers && isError) {
      setIsStale(true)
    }
    if (!isFetching && !isError) {
      setIsStale(false)
    }
  }, [isFetching, isError, providers])

  const handleRefresh = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: adminProviderListKey() })
  }, [queryClient])

  const selectedProvider = providers?.find((p) => p.id === providerId)

  if (isLoading) return <LoadingState label="Loading providers…" />

  if (isError && !providers) {
    return (
      <ErrorState
        title="Failed to load providers"
        message={error instanceof ApiError ? error.message : undefined}
        correlationId={
          error instanceof ApiError ? error.correlationId : undefined
        }
        onRetry={handleRefresh}
      />
    )
  }

  if (!providers || providers.length === 0) {
    return (
      <EmptyState
        title="No providers configured"
        message="Add intelligence feed providers to begin ingestion."
      />
    )
  }

  return (
    <div>
      {isStale && (
        <div
          role="alert"
          className="mb-4 rounded border border-warning/40 bg-warning-container px-4 py-2 text-sm text-on-warning-container"
        >
          <span className="font-semibold">Stale data</span> — showing last
          successful fetch.{' '}
          <button
            type="button"
            onClick={handleRefresh}
            className="font-semibold underline"
          >
            Retry
          </button>
        </div>
      )}

      <div className="overflow-x-auto rounded border border-outline-variant bg-surface-container-lowest">
        <table className="w-full text-left">
          <thead>
            <tr className="border-b border-outline-variant bg-surface-container-low font-mono text-[10px] uppercase tracking-wider text-on-surface-variant">
              <th className="px-4 py-2 font-medium">Provider</th>
              <th className="px-4 py-2 font-medium">Schedule</th>
              <th className="px-4 py-2 font-medium">Last Fetch</th>
              <th className="px-4 py-2 font-medium">Status</th>
              <th className="px-4 py-2 font-medium text-center">Enabled</th>
              <th className="px-4 py-2 font-medium text-right">Operations</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-surface-container-low font-body text-[13px]">
            {providers.map((p) => (
              <ProviderRow
                key={p.id}
                provider={p}
                isSelected={p.id === providerId}
              />
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-3 flex items-center justify-between font-mono text-[10px] text-on-surface-variant">
        <span>
          {providers.length} provider{providers.length !== 1 ? 's' : ''}{' '}
          configured
        </span>
        <span>
          {dataUpdatedAt > 0
            ? `Last updated ${new Date(dataUpdatedAt).toLocaleTimeString()}`
            : ''}
          {isFetching ? ' (refreshing…)' : ''}
        </span>
      </div>

      {/* History panel */}
      {selectedProvider && (
        <HistoryPanel
          providerId={selectedProvider.id}
          providerName={selectedProvider.name}
          page={page}
          onPageChange={(nextPage) => {
            void navigate({
              to: '/admin/providers',
              search: { providerId: selectedProvider.id, page: nextPage },
            })
          }}
          onClose={() => {
            void navigate({ to: '/admin/providers', search: {} })
          }}
        />
      )}
    </div>
  )
}
