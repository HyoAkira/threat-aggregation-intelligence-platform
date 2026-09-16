import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import DataTables from 'datatables.net-react'
import type { DataTableRef } from 'datatables.net-react'
import DT from 'datatables.net'
import type { Config } from 'datatables.net'
import { ApiError, getAdminProviderHistory } from '../lib/apiClient'
import type { AdminFetchHistoryItem } from '../lib/adminSchemas'
import {
  dateRangeQuery,
  emptyAdminDataTablesResponse,
} from '../lib/adminDataTables'
import type { AdminDataTablesRequest } from '../lib/adminDataTables'
import { formatLocalMonthDayTime, parseUtcDateTime } from '../lib/format'
import { focusRingClass } from './AppShell'

DataTables.use(DT)

const controlClass =
  'rounded border border-outline-variant bg-surface-container-lowest px-3 py-1.5 font-mono text-xs text-on-surface focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary'

type Props = {
  providerId: number
  providerName: string
  refreshKey?: number
  onClose?: () => void
}

function StatusBadge({ status }: { status: string }) {
  const failed = status === 'failed'
  return (
    <span
      className={`inline-flex items-center gap-1 rounded px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider ${failed ? 'bg-error-container text-on-error-container' : 'bg-tertiary/10 text-tertiary'}`}
    >
      <span
        className={`h-1.5 w-1.5 rounded-full ${failed ? 'bg-error' : 'bg-tertiary'}`}
      />
      {status}
    </span>
  )
}

function formatDuration(startedAt: string, finishedAt: string | null): string {
  if (!finishedAt) return '—'
  const milliseconds =
    parseUtcDateTime(finishedAt).getTime() -
    parseUtcDateTime(startedAt).getTime()
  if (milliseconds < 1000) return `${milliseconds}ms`
  const seconds = Math.floor(milliseconds / 1000)
  if (seconds < 60) return `${seconds}s`
  const minutes = Math.floor(seconds / 60)
  return `${minutes}m ${seconds % 60}s`
}

function DateRangeControls({
  dateFrom,
  dateTo,
  setDateFrom,
  setDateTo,
  onApply,
  onClear,
}: {
  dateFrom: string
  dateTo: string
  setDateFrom: (value: string) => void
  setDateTo: (value: string) => void
  onApply: () => void
  onClear: () => void
}) {
  return (
    <div className="flex flex-wrap items-end gap-3 border-b border-outline-variant px-4 py-3">
      <div className="flex flex-col gap-1">
        <label
          htmlFor="history-date-from"
          className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
        >
          From
        </label>
        <input
          id="history-date-from"
          type="date"
          value={dateFrom}
          onChange={(event) => setDateFrom(event.target.value)}
          className={controlClass}
        />
      </div>
      <div className="flex flex-col gap-1">
        <label
          htmlFor="history-date-to"
          className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
        >
          To
        </label>
        <input
          id="history-date-to"
          type="date"
          value={dateTo}
          onChange={(event) => setDateTo(event.target.value)}
          className={controlClass}
        />
      </div>
      <button
        type="button"
        onClick={onApply}
        className={`rounded bg-primary px-3 py-1.5 font-mono text-[11px] font-semibold text-on-primary hover:bg-primary-container ${focusRingClass}`}
      >
        Apply range
      </button>
      <button
        type="button"
        onClick={onClear}
        className={`rounded border border-outline-variant px-3 py-1.5 font-mono text-[11px] font-semibold text-on-surface-variant hover:bg-surface-container-low ${focusRingClass}`}
      >
        Clear
      </button>
    </div>
  )
}

function ErrorCell({ value }: { value: string | null }) {
  const [expanded, setExpanded] = useState(false)
  if (!value) return <span className="text-on-surface-variant">—</span>
  return (
    <div>
      <button
        type="button"
        onClick={() => setExpanded((current) => !current)}
        className={`rounded bg-error/10 px-1.5 py-0.5 font-mono text-[10px] font-semibold text-error hover:bg-error/20 ${focusRingClass}`}
      >
        {expanded ? 'Hide' : 'Inspect'}
      </button>
      {expanded ? (
        <pre className="mt-2 max-w-72 whitespace-pre-wrap break-words rounded bg-error-container/40 p-2 font-mono text-[10px] text-on-surface">
          {value}
        </pre>
      ) : null}
    </div>
  )
}

export default function AdminProviderHistoryDataTable({
  providerId,
  providerName,
  refreshKey = 0,
  onClose,
}: Props) {
  const tableRef = useRef<DataTableRef | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const [appliedRange, setAppliedRange] = useState({ dateFrom: '', dateTo: '' })
  const [error, setError] = useState<unknown>(null)
  const queryRef = useRef({ providerId, appliedRange })
  queryRef.current = { providerId, appliedRange }

  const applyRange = useCallback(() => {
    setAppliedRange({ dateFrom, dateTo })
  }, [dateFrom, dateTo])
  const clearRange = useCallback(() => {
    setDateFrom('')
    setDateTo('')
    setAppliedRange({ dateFrom: '', dateTo: '' })
  }, [])

  const ajax = useCallback(
    (rawRequest: unknown, callback: (response: unknown) => void) => {
      const request = rawRequest as AdminDataTablesRequest
      const draw = typeof request.draw === 'number' ? request.draw : 0
      const start = typeof request.start === 'number' ? request.start : 0
      const pageSize = typeof request.length === 'number' ? request.length : 20
      const column = request.order?.[0]?.column
      const orderBy =
        column === 5 || request.order?.[0]?.name === 'finishedAt'
          ? 'finishedAt'
          : 'startedAt'
      const orderDir = request.order?.[0]?.dir === 'asc' ? 'asc' : 'desc'
      const { providerId: currentProviderId, appliedRange: currentRange } =
        queryRef.current
      const range = dateRangeQuery(currentRange.dateFrom, currentRange.dateTo)
      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller
      setError(null)
      getAdminProviderHistory(
        currentProviderId,
        Math.floor(start / pageSize) + 1,
        pageSize,
        controller.signal,
        { ...range, orderBy, orderDir },
      ).then(
        (result) => {
          if (controller.signal.aborted) return
          callback({
            draw,
            data: result.items,
            recordsTotal: result.totalCount,
            recordsFiltered: result.totalCount,
          })
        },
        (reason: unknown) => {
          if (controller.signal.aborted) return
          setError(reason)
          callback(emptyAdminDataTablesResponse<AdminFetchHistoryItem>(draw))
        },
      )
    },
    [],
  )

  useEffect(() => {
    const table = tableRef.current?.dt()
    if (table) {
      table.page(0).draw(false)
    }
  }, [appliedRange, providerId, refreshKey])

  useEffect(() => () => abortRef.current?.abort(), [])

  const columns = useMemo<NonNullable<Config['columns']>>(
    () => [
      { data: 'jobId', name: 'jobId', orderable: false, searchable: false },
      { data: 'trigger', name: 'trigger', orderable: false, searchable: false },
      { data: 'mode', name: 'mode', orderable: false, searchable: false },
      { data: 'status', name: 'status', orderable: false, searchable: false },
      {
        data: 'startedAt',
        name: 'startedAt',
        orderable: true,
        searchable: false,
      },
      {
        data: 'finishedAt',
        name: 'finishedAt',
        orderable: true,
        searchable: false,
      },
      {
        data: 'duration',
        name: 'duration',
        orderable: false,
        searchable: false,
      },
      {
        data: 'recordsAdded',
        name: 'recordsAdded',
        orderable: false,
        searchable: false,
      },
      {
        data: 'recordsUpdated',
        name: 'recordsUpdated',
        orderable: false,
        searchable: false,
      },
      {
        data: 'errorDetail',
        name: 'errorDetail',
        orderable: false,
        searchable: false,
      },
    ],
    [],
  )

  const slots = useMemo(
    () => ({
      0: (_value: unknown, row: AdminFetchHistoryItem) => (
        <span
          className="font-mono text-[11px] text-on-surface-variant"
          title={row.jobId}
        >
          {row.jobId.slice(0, 8)}…
        </span>
      ),
      1: (_value: unknown, row: AdminFetchHistoryItem) => (
        <span className="rounded bg-surface-container px-1.5 py-0.5 font-mono text-[10px] font-semibold uppercase text-on-surface-variant">
          {row.trigger}
        </span>
      ),
      2: (_value: unknown, row: AdminFetchHistoryItem) => (
        <span className="rounded bg-surface-container px-1.5 py-0.5 font-mono text-[10px] font-semibold uppercase text-primary">
          {row.mode}
        </span>
      ),
      3: (_value: unknown, row: AdminFetchHistoryItem) => (
        <StatusBadge status={row.status} />
      ),
      4: (_value: unknown, row: AdminFetchHistoryItem) => (
        <span className="font-mono text-[11px] text-on-surface-variant">
          {formatLocalMonthDayTime(row.startedAt) ?? '—'}
        </span>
      ),
      5: (_value: unknown, row: AdminFetchHistoryItem) => (
        <span className="font-mono text-[11px] text-on-surface-variant">
          {formatLocalMonthDayTime(row.finishedAt) ?? '—'}
        </span>
      ),
      6: (_value: unknown, row: AdminFetchHistoryItem) => (
        <span className="font-mono text-[11px] text-on-surface-variant">
          {formatDuration(row.startedAt, row.finishedAt)}
        </span>
      ),
      7: (_value: unknown, row: AdminFetchHistoryItem) => (
        <span className="font-mono text-[11px] text-tertiary">
          +{row.recordsAdded}
        </span>
      ),
      8: (_value: unknown, row: AdminFetchHistoryItem) => (
        <span className="font-mono text-[11px] text-tertiary">
          +{row.recordsUpdated}
        </span>
      ),
      9: (_value: unknown, row: AdminFetchHistoryItem) => (
        <ErrorCell value={row.errorDetail} />
      ),
    }),
    [],
  )

  const options = useMemo<Config>(
    () => ({
      serverSide: true,
      processing: true,
      searching: false,
      orderMulti: false,
      autoWidth: false,
      pageLength: 20,
      lengthMenu: [10, 20, 50, 100],
      order: [[4, 'desc']],
      layout: {
        topStart: null,
        topEnd: null,
        bottomStart: 'info',
        bottomEnd: 'paging',
      },
    }),
    [],
  )

  return (
    <div className="mt-6 rounded border border-outline-variant bg-surface-container-lowest">
      <div className="flex items-center justify-between border-b border-outline-variant px-4 py-3">
        <h3 className="font-semibold text-on-surface">
          Fetch History — {providerName}
        </h3>
        <div className="flex items-center gap-3">
          {error ? (
            <span className="font-mono text-[10px] text-error">
              {error instanceof ApiError
                ? error.message
                : 'Unable to load history.'}
            </span>
          ) : null}
          {onClose ? (
            <button
              type="button"
              onClick={onClose}
              className={`rounded p-1 text-on-surface-variant hover:bg-surface-container-low ${focusRingClass}`}
              aria-label="Close history"
            >
              <span className="material-symbols-outlined text-[18px]">
                close
              </span>
            </button>
          ) : null}
        </div>
      </div>
      <DateRangeControls
        dateFrom={dateFrom}
        dateTo={dateTo}
        setDateFrom={setDateFrom}
        setDateTo={setDateTo}
        onApply={applyRange}
        onClear={clearRange}
      />
      <div className="overflow-x-auto px-1">
        <DataTables
          ref={tableRef}
          options={options}
          ajax={ajax}
          columns={columns}
          slots={slots}
          className="w-full border-collapse text-left"
        >
          <thead>
            <tr className="h-9 select-none border-b border-outline-variant/60 bg-surface-container-low font-mono text-[10px] uppercase tracking-wider text-outline">
              <th scope="col" className="px-4 py-2 font-semibold">
                Job
              </th>
              <th scope="col" className="px-4 py-2 font-semibold">
                Trigger
              </th>
              <th scope="col" className="px-4 py-2 font-semibold">
                Mode
              </th>
              <th scope="col" className="px-4 py-2 font-semibold">
                Status
              </th>
              <th scope="col" className="px-4 py-2 font-semibold">
                Started
              </th>
              <th scope="col" className="px-4 py-2 font-semibold">
                Finished
              </th>
              <th scope="col" className="px-4 py-2 font-semibold">
                Duration
              </th>
              <th scope="col" className="px-4 py-2 font-semibold">
                Added
              </th>
              <th scope="col" className="px-4 py-2 font-semibold">
                Updated
              </th>
              <th scope="col" className="px-4 py-2 font-semibold">
                Error
              </th>
            </tr>
          </thead>
        </DataTables>
      </div>
    </div>
  )
}
