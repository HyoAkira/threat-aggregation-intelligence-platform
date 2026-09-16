import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import DataTables from 'datatables.net-react'
import type { DataTableRef } from 'datatables.net-react'
import DT from 'datatables.net'
import type { Config } from 'datatables.net'
import { ApiError, getAdminLogsTable } from '../lib/apiClient'
import type { AdminLogEntry } from '../lib/adminSchemas'
import {
  dateRangeQuery,
  emptyAdminDataTablesResponse,
} from '../lib/adminDataTables'
import type { AdminDataTablesRequest } from '../lib/adminDataTables'
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
  const [expanded, setExpanded] = useState(false)
  if (!value) return <span className="text-on-surface-variant">—</span>
  return (
    <div>
      <button
        type="button"
        onClick={() => setExpanded((current) => !current)}
        className={`rounded bg-error/10 px-1.5 py-0.5 font-mono text-[10px] font-semibold text-error hover:bg-error/20 ${focusRingClass}`}
      >
        {expanded ? 'Hide' : 'Show'}
      </button>
      {expanded ? (
        <pre className="mt-2 max-w-96 whitespace-pre-wrap break-words rounded bg-surface-container-low p-2 font-mono text-[10px] leading-relaxed text-on-surface">
          {value}
        </pre>
      ) : null}
    </div>
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
      },
      { data: 'level', name: 'level', orderable: false, searchable: false },
      { data: 'message', name: 'message', orderable: false, searchable: false },
      {
        data: 'correlationId',
        name: 'correlationId',
        orderable: false,
        searchable: false,
      },
      {
        data: 'exception',
        name: 'exception',
        orderable: false,
        searchable: false,
      },
    ],
    [],
  )

  const slots = useMemo(
    () => ({
      0: (_value: unknown, row: AdminLogEntry) => (
        <span className="whitespace-nowrap font-mono text-[11px] text-on-surface-variant">
          {formatTimestamp(row.timestamp)}
        </span>
      ),
      1: (_value: unknown, row: AdminLogEntry) => (
        <span
          className={`inline-block rounded px-1.5 py-0.5 font-mono text-[10px] font-semibold uppercase ${row.level === 'Error' || row.level === 'Fatal' ? 'bg-error-container text-on-error-container' : row.level === 'Warning' ? 'bg-warning-container text-on-warning-container' : row.level === 'Information' ? 'bg-tertiary/10 text-tertiary' : 'bg-surface-container text-on-surface-variant'}`}
        >
          {row.level}
        </span>
      ),
      2: (_value: unknown, row: AdminLogEntry) => (
        <span className="whitespace-pre-wrap break-words font-body text-[12px] text-on-surface">
          {row.message}
        </span>
      ),
      3: (_value: unknown, row: AdminLogEntry) => (
        <span className="font-mono text-[11px] text-on-surface-variant">
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
    <div className="mt-4 overflow-x-auto rounded border border-outline-variant bg-surface-container-lowest">
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
            <th scope="col" className="px-3 py-2 font-semibold">
              Timestamp
            </th>
            <th scope="col" className="px-3 py-2 font-semibold">
              Level
            </th>
            <th scope="col" className="px-3 py-2 font-semibold">
              Message
            </th>
            <th scope="col" className="px-3 py-2 font-semibold">
              Correlation ID
            </th>
            <th scope="col" className="px-3 py-2 font-semibold">
              Exception
            </th>
          </tr>
        </thead>
      </DataTables>
    </div>
  )
}
