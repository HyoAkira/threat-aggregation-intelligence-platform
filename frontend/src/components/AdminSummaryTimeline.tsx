import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { ApiError, getAdminTimeline } from '../lib/apiClient'
import { adminTimelineKey, adminTimelineKeyPrefix } from '../lib/queryKeys'
import {
  computePresetRange,
  detectTimeZone,
  formatUtcOffset,
  rangeFromLocalDates,
  validateCustomRange,
} from '../lib/adminTimeline'
import type { TimelinePreset, TimelineRange } from '../lib/adminTimeline'
import { ErrorState, LoadingState } from './uiStates'
import { focusRingClass } from './AppShell'

/**
 * Re-detects the browser IANA timezone on focus, visibility, and a slow interval so a timezone
 * change while the page is open produces a new query key instead of reusing the old points.
 */
export function useBrowserTimeZone(): string {
  const [timeZone, setTimeZone] = useState(() => detectTimeZone())

  useEffect(() => {
    const update = () => {
      const next = detectTimeZone()
      setTimeZone((current) => (current === next ? current : next))
    }

    window.addEventListener('focus', update)
    document.addEventListener('visibilitychange', update)
    const interval = window.setInterval(update, 60000)
    return () => {
      window.removeEventListener('focus', update)
      document.removeEventListener('visibilitychange', update)
      window.clearInterval(interval)
    }
  }, [])

  return timeZone
}

const presets: { value: TimelinePreset; label: string }[] = [
  { value: '7d', label: '7 days' },
  { value: '1m', label: '1 month' },
  { value: 'custom', label: 'Custom' },
]

type TooltipEntry = { name?: string; value?: number }

function TimelineTooltip({
  active,
  payload,
  label,
  timeZone,
}: {
  active?: boolean
  payload?: TooltipEntry[]
  label?: string
  timeZone: string
}) {
  if (!active || !payload || payload.length === 0) return null
  return (
    <div className="rounded border border-outline-variant bg-surface-container-lowest px-3 py-2 text-xs shadow">
      <div className="font-semibold text-on-surface">
        {label} <span className="text-on-surface-variant">({timeZone})</span>
      </div>
      {payload.map((entry) => (
        <div key={entry.name} className="mt-0.5 text-on-surface-variant">
          {entry.name}: <span className="font-semibold text-on-surface">{entry.value}</span>
        </div>
      ))}
    </div>
  )
}

export function AdminSummaryTimeline() {
  const queryClient = useQueryClient()
  const timeZone = useBrowserTimeZone()
  const [preset, setPreset] = useState<TimelinePreset>('7d')
  const [customStart, setCustomStart] = useState('')
  const [customEnd, setCustomEnd] = useState('')
  const [isStale, setIsStale] = useState(false)

  const customValidation = useMemo(
    () => validateCustomRange(customStart, customEnd),
    [customStart, customEnd],
  )

  const range: TimelineRange | null = useMemo(() => {
    if (preset === 'custom') {
      return customValidation.valid
        ? rangeFromLocalDates(customStart, customEnd, timeZone)
        : null
    }
    return computePresetRange(preset, timeZone)
  }, [preset, customValidation.valid, customStart, customEnd, timeZone])

  const {
    data: timeline,
    isLoading,
    isError,
    error,
    isFetching,
    dataUpdatedAt,
  } = useQuery({
    queryKey: adminTimelineKey(
      range?.from ?? 'invalid',
      range?.to ?? 'invalid',
      timeZone,
    ),
    queryFn: ({ signal }) =>
      getAdminTimeline(
        { from: range!.from, to: range!.to, timeZone },
        signal,
      ),
    enabled: range !== null,
    refetchInterval: 30000,
    refetchIntervalInBackground: false,
  })

  useEffect(() => {
    if (!isFetching && timeline && isError) setIsStale(true)
    if (!isFetching && !isError) setIsStale(false)
  }, [isFetching, isError, timeline])

  const handleRetry = () => {
    void queryClient.invalidateQueries({ queryKey: adminTimelineKeyPrefix() })
  }

  const offset = formatUtcOffset(timeZone)
  const customInvalid = preset === 'custom' && !customValidation.valid
  const points = timeline?.points ?? []

  return (
    <section
      className="mt-6 rounded border border-outline-variant bg-surface-container-lowest p-4"
      aria-labelledby="timeline-heading"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3
            id="timeline-heading"
            className="text-base font-semibold text-on-surface"
          >
            IOC Activity Timeline
          </h3>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            Added and updated indicators from completed provider fetches.
          </p>
        </div>
        <button
          type="button"
          onClick={handleRetry}
          disabled={isFetching || range === null}
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

      <fieldset className="mt-4">
        <legend className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
          Time range
        </legend>
        <div className="mt-1.5 flex flex-wrap items-center gap-4">
          {presets.map((option) => (
            <label
              key={option.value}
              className="flex items-center gap-1.5 text-sm text-on-surface"
            >
              <input
                type="radio"
                name="timeline-preset"
                value={option.value}
                checked={preset === option.value}
                onChange={() => setPreset(option.value)}
                className={focusRingClass}
              />
              {option.label}
            </label>
          ))}
        </div>

        {preset === 'custom' && (
          <div className="mt-3 flex flex-wrap items-start gap-4">
            <div className="flex flex-col gap-1">
              <label
                htmlFor="timeline-start"
                className="text-xs font-semibold text-on-surface-variant"
              >
                Start date
              </label>
              <input
                id="timeline-start"
                type="date"
                value={customStart}
                onChange={(event) => setCustomStart(event.target.value)}
                aria-invalid={customInvalid}
                aria-describedby={customInvalid ? 'timeline-range-error' : undefined}
                className={`rounded border border-outline-variant bg-surface-container-lowest px-2 py-1 text-sm text-on-surface ${focusRingClass}`}
              />
            </div>
            <div className="flex flex-col gap-1">
              <label
                htmlFor="timeline-end"
                className="text-xs font-semibold text-on-surface-variant"
              >
                End date
              </label>
              <input
                id="timeline-end"
                type="date"
                value={customEnd}
                onChange={(event) => setCustomEnd(event.target.value)}
                aria-invalid={customInvalid}
                aria-describedby={customInvalid ? 'timeline-range-error' : undefined}
                className={`rounded border border-outline-variant bg-surface-container-lowest px-2 py-1 text-sm text-on-surface ${focusRingClass}`}
              />
            </div>
            {customInvalid && (
              <p
                id="timeline-range-error"
                role="alert"
                className="self-center text-xs font-semibold text-error"
              >
                {customValidation.error}
              </p>
            )}
          </div>
        )}

        <p className="mt-3 font-mono text-[10px] text-on-surface-variant">
          Times shown in {timeZone} ({offset})
        </p>
      </fieldset>

      {isStale && (
        <div
          role="alert"
          className="mt-3 rounded border border-warning/40 bg-warning-container px-3 py-2 text-xs text-on-warning-container"
        >
          <span className="font-semibold">Stale timeline</span> — showing the last
          successful retrieval.{' '}
          <button
            type="button"
            onClick={handleRetry}
            disabled={isFetching}
            className="font-semibold underline disabled:opacity-50"
          >
            Retry
          </button>
        </div>
      )}

      <div className="mt-4">
        {range === null ? (
          <p role="status" className="text-sm text-on-surface-variant">
            Enter a valid custom range to load the timeline.
          </p>
        ) : isLoading ? (
          <LoadingState label="Loading timeline…" />
        ) : isError && !timeline ? (
          <ErrorState
            title="Failed to load timeline"
            message={error instanceof ApiError ? error.message : undefined}
            correlationId={
              error instanceof ApiError ? error.correlationId : undefined
            }
            onRetry={handleRetry}
          />
        ) : !timeline || !timeline.hasSuccessfulFetches ? (
          <p role="status" className="text-sm text-on-surface-variant">
            No IOC activity was recorded for the selected range in {timeZone}.
          </p>
        ) : (
          <>
            <div className="h-64 w-full" data-testid="timeline-chart">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart
                  data={points}
                  margin={{ top: 8, right: 16, bottom: 8, left: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="currentColor" opacity={0.15} />
                  <XAxis dataKey="localDate" tick={{ fontSize: 11 }} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                  <Tooltip
                    content={<TimelineTooltip timeZone={timeZone} />}
                  />
                  <Legend />
                  <Line
                    type="monotone"
                    dataKey="recordsAdded"
                    name="Added"
                    stroke="#2563eb"
                    dot={false}
                  />
                  <Line
                    type="monotone"
                    dataKey="recordsUpdated"
                    name="Updated"
                    stroke="#16a34a"
                    dot={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </>
        )}

        {timeline && (
          <details className="mt-4">
            <summary
              className={`cursor-pointer font-mono text-[11px] font-semibold text-primary ${focusRingClass}`}
            >
              View timeline data table
            </summary>
            <table className="mt-2 w-full border-collapse text-left text-xs">
              <caption className="sr-only">
                IOC activity by local date in {timeZone}
              </caption>
              <thead>
                <tr className="text-on-surface-variant">
                  <th scope="col" className="border-b border-outline-variant py-1 pr-4">
                    Local date
                  </th>
                  <th scope="col" className="border-b border-outline-variant py-1 pr-4">
                    Added
                  </th>
                  <th scope="col" className="border-b border-outline-variant py-1">
                    Updated
                  </th>
                </tr>
              </thead>
              <tbody>
                {points.map((point) => (
                  <tr key={point.localDate}>
                    <th
                      scope="row"
                      className="border-b border-outline-variant/40 py-1 pr-4 font-normal"
                    >
                      {point.localDate}
                    </th>
                    <td className="border-b border-outline-variant/40 py-1 pr-4">
                      {point.recordsAdded}
                    </td>
                    <td className="border-b border-outline-variant/40 py-1">
                      {point.recordsUpdated}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </details>
        )}

        {timeline && (
          <p className="mt-2 font-mono text-[10px] text-on-surface-variant">
            {dataUpdatedAt > 0
              ? `Retrieved ${new Date(dataUpdatedAt).toLocaleTimeString()}`
              : ''}
            {isFetching ? ' (refreshing…)' : ''}
          </p>
        )}
      </div>
    </section>
  )
}
