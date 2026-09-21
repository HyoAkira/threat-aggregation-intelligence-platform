import type { Config } from 'datatables.net'
import DT from 'datatables.net'
import type { DataTableRef } from 'datatables.net-react'
import DataTables from 'datatables.net-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { AdminDataTablesRequest } from '../lib/adminDataTables'
import {
  dateRangeQuery,
  emptyAdminDataTablesResponse,
} from '../lib/adminDataTables'
import type { AdminLogEntry } from '../lib/adminSchemas'
import { ApiError, getAdminLogsTable } from '../lib/apiClient'
import { parseUtcDateTime } from '../lib/format'
import { focusRingClass } from './AppShell'

DataTables.use(DT)

const controlClass =
  'rounded border border-outline-variant bg-surface-container-lowest px-3 py-1.5 font-mono text-xs text-on-surface focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary'

function formatTimestamp(iso: string): string {
  try {
    return parseUtcDateTime(iso).toLocaleString('en-US', {
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

function ExceptionCell({ value }: { value: string | null }) {
  if (!value) return <span className="text-on-surface-variant">—</span>
  return (
    <details className="admin-logs-exception">
      <summary
        className={`inline-flex cursor-pointer list-none items-center gap-1 rounded bg-error/10 px-1.5 py-0.5 font-mono text-[10px] font-semibold text-error hover:bg-error/20 ${focusRingClass}`}
        aria-label="View exception details"
      >
        <span className="material-symbols-outlined admin-logs-exception-icon text-[13px]">
          expand_more
        </span>
        View
      </summary>
      <div className="admin-logs-exception-popover">
        <div className="flex items-center justify-between gap-3 border-b border-error/20 bg-error-container/40 px-3 py-2">
          <span className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-error-container">
            Exception details
          </span>
          <span className="font-mono text-[10px] text-on-surface-variant">
            Click View to close
          </span>
        </div>
        <pre className="max-h-72 overflow-auto whitespace-pre-wrap break-words p-3 font-mono text-[10px] leading-relaxed text-on-surface">
          {value}
        </pre>
      </div>
    </details>
  )
}

type Props = {
  level: string | null
  source: 'api' | 'worker' | 'all'
  correlationId: string | null
  refreshKey?: number
}

export default function AdminLogsDataTable({
  level,
  source,
  correlationId,
  refreshKey = 0,
}: Props) {
  const tableRef = useRef<DataTableRef | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const [appliedRange, setAppliedRange] = useState({ dateFrom: '', dateTo: '' })
  const [error, setError] = useState<unknown>(null)
  const queryRef = useRef({ level, source, correlationId, appliedRange })
  queryRef.current = { level, source, correlationId, appliedRange }

  const reload = useCallback(
    () => setAppliedRange({ dateFrom, dateTo }),
    [dateFrom, dateTo],
  )
  const clear = useCallback(() => {
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
      const orderDir = request.order?.[0]?.dir === 'asc' ? 'asc' : 'desc'
      const {
        level: currentLevel,
        source: currentSource,
        correlationId: currentCorrelationId,
        appliedRange: currentRange,
      } = queryRef.current
      const range = dateRangeQuery(currentRange.dateFrom, currentRange.dateTo)
      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller
      setError(null)
      getAdminLogsTable(
        {
          page: Math.floor(start / pageSize) + 1,
          pageSize,
          level: currentLevel,
          source: currentSource,
          correlationId: currentCorrelationId,
          ...range,
          orderDir,
        },
        controller.signal,
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
          callback(emptyAdminDataTablesResponse<AdminLogEntry>(draw))
        },
      )
    },
    [],
  )

  useEffect(() => {
    const table = tableRef.current?.dt()
    if (table) table.page(0).draw(false)
  }, [appliedRange, correlationId, level, source, refreshKey])

  useEffect(() => () => abortRef.current?.abort(), [])

  const columns = useMemo<NonNullable<Config['columns']>>(
    () => [
      {
        data: 'timestamp',
        name: 'timestamp',
        orderable: true,
        searchable: false,
        width: '15%',
      },
      {
        data: 'level',
        name: 'level',
        orderable: false,
        searchable: false,
        width: '10%',
      },
      {
        data: 'message',
        name: 'message',
        orderable: false,
        searchable: false,
        width: '42%',
      },
      {
        data: 'correlationId',
        name: 'correlationId',
        orderable: false,
        searchable: false,
        width: '18%',
      },
      {
        data: 'exception',
        name: 'exception',
        orderable: false,
        searchable: false,
        width: '16%',
      },
    ],
    [],
  )

  const slots = useMemo(
    () => ({
      0: (_value: unknown, row: AdminLogEntry) => (
        <span className="admin-logs-timestamp font-mono text-[11px] text-on-surface-variant">
          {formatTimestamp(row.timestamp)}
        </span>
      ),
      1: (_value: unknown, row: AdminLogEntry) => (
        <span
          className={`inline-flex items-center justify-center whitespace-nowrap rounded px-1.5 py-0.5 font-mono text-[10px] font-semibold uppercase ${row.level === 'Error' || row.level === 'Fatal' ? 'bg-error-container text-on-error-container' : row.level === 'Warning' ? 'bg-warning-container text-on-warning-container' : row.level === 'Information' ? 'bg-tertiary/10 text-tertiary' : 'bg-surface-container text-on-surface-variant'}`}
        >
          {row.level}
        </span>
      ),
      2: (_value: unknown, row: AdminLogEntry) => (
        <span className="admin-logs-message block max-w-full whitespace-pre-wrap break-words font-body text-[12px] text-on-surface">
          {row.message}
        </span>
      ),
      3: (_value: unknown, row: AdminLogEntry) => (
        <span className="admin-logs-correlation block max-w-full break-all font-mono text-[11px] text-on-surface-variant">
          {row.correlationId ?? '—'}
        </span>
      ),
      4: (_value: unknown, row: AdminLogEntry) => (
        <ExceptionCell value={row.exception} />
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
      order: [[0, 'desc']],
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
    <div className="admin-logs-panel mt-4 rounded border border-outline-variant bg-surface-container-lowest">
      <div className="flex flex-wrap items-end gap-3 border-b border-outline-variant px-4 py-3">
        <div className="flex flex-col gap-1">
          <label
            htmlFor="logs-date-from"
            className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
          >
            From
          </label>
          <input
            id="logs-date-from"
            type="date"
            value={dateFrom}
            onChange={(event) => setDateFrom(event.target.value)}
            className={controlClass}
          />
        </div>
        <div className="flex flex-col gap-1">
          <label
            htmlFor="logs-date-to"
            className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
          >
            To
          </label>
          <input
            id="logs-date-to"
            type="date"
            value={dateTo}
            onChange={(event) => setDateTo(event.target.value)}
            className={controlClass}
          />
        </div>
        <button
          type="button"
          onClick={reload}
          className={`rounded bg-primary px-3 py-1.5 font-mono text-[11px] font-semibold text-on-primary hover:bg-primary-container ${focusRingClass}`}
        >
          Apply range
        </button>
        <button
          type="button"
          onClick={clear}
          className={`rounded border border-outline-variant px-3 py-1.5 font-mono text-[11px] font-semibold text-on-surface-variant hover:bg-surface-container-low ${focusRingClass}`}
        >
          Clear
        </button>
        {error ? (
          <span className="ml-auto font-mono text-[10px] text-error">
            {error instanceof ApiError ? error.message : 'Unable to load logs.'}
          </span>
        ) : null}
      </div>
      <div className="admin-logs-table-wrap">
        <DataTables
          ref={tableRef}
          options={options}
          ajax={ajax}
          columns={columns}
          slots={slots}
          className="admin-logs-table w-full border-collapse text-left"
        >
          <thead>
            <tr className="h-9 select-none border-b border-outline-variant/60 bg-surface-container-low font-mono text-[10px] uppercase tracking-wider text-outline">
              <th scope="col" className="px-3 py-2 font-semibold">
                Timestamp
              </th>
              <th scope="col" className="px-3 py-2 font-semibold">
                Level
              </th>
              <th scope="col" className="px-3 py-2 font-semibold">
                Message
              </th>
              <th
                scope="col"
                className="admin-logs-correlation-column px-3 py-2 font-semibold"
              >
                Correlation ID
              </th>
              <th scope="col" className="px-3 py-2 font-semibold">
                Exception
              </th>
            </tr>
          </thead>
        </DataTables>
      </div>
    </div>
  )
}
