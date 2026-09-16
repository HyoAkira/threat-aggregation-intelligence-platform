import {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
} from 'react'
import type { RefObject } from 'react'
import DataTables from 'datatables.net-react'
import type { DataTableRef } from 'datatables.net-react'
import DT from 'datatables.net'
import type { Config } from 'datatables.net'
import { ApiError, browseIocs } from '../lib/apiClient'
import type { BrowseParams } from '../lib/browseParams'
import type { IocGroup } from '../lib/schemas'
import {
  adaptBrowseRequest,
  adaptBrowseResponse,
  browseTableColumns,
  emptyDataTablesResponse,
} from '../lib/browseDataTables'
import type { DataTablesRequest } from '../lib/browseDataTables'
import {
  browseParamsFingerprint,
  browseTableStateFromParams,
  initialBrowseSyncState,
  onBrowseTableAction,
  onBrowseUrlChange,
} from '../lib/browseTableState'
import {
  formatShortLocal,
  iocTypeBadgeClass,
  iocTypeLabels,
  threatLabel,
} from '../lib/iocPresentation'
import { focusRingClass } from './AppShell'

// DataTables is DOM-oriented: this module must only ever load in the browser
// (it is imported dynamically beneath `ClientOnly`).
DataTables.use(DT)

const rowClass =
  'group align-top transition-colors hover:bg-surface-container-low/60'
const headerClass = 'px-3 py-2 font-semibold'

/** Transport/state of the table surfaced to the route's outer status UI. */
export type BrowseTableStatus =
  | { kind: 'pending' }
  | {
      kind: 'success'
      shown: number
      totalCount: number
      page: number
      pageSize: number
    }
  | { kind: 'empty'; totalCount: number }
  | { kind: 'forbidden' }
  | { kind: 'error'; error: unknown }

export type BrowseTableHandle = {
  /** Reissues exactly one request for the current URL state. */
  reload: () => void
}

type BrowseDataTableProps = {
  params: BrowseParams
  onTableParams: (params: BrowseParams) => void
  onStatus: (status: BrowseTableStatus) => void
  onCopy: (value: string) => void
  onOpenDetail: (id: number) => void
}

type Handlers = {
  onTableParams: (params: BrowseParams) => void
  onStatus: (status: BrowseTableStatus) => void
  onCopy: (value: string) => void
  onOpenDetail: (id: number) => void
}

function detailHref(id: number): string {
  return `/iocs/${id}`
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

function providersOf(group: IocGroup): string[] {
  return Array.from(new Set(group.sources.map((source) => source.provider)))
}

function IndicatorCell({
  group,
  handlers,
}: {
  group: IocGroup
  handlers: RefObject<Handlers>
}) {
  const { onCopy, onOpenDetail } = handlers.current
  return (
    <div className="flex items-start gap-2">
      <a
        href={detailHref(group.id)}
        title={group.iocValue}
        onClick={(event) => {
          event.preventDefault()
          onOpenDetail(group.id)
        }}
        className={`line-clamp-2 min-w-0 max-w-[13rem] break-all overflow-hidden font-mono text-[11px] font-semibold leading-snug text-on-surface underline-offset-2 hover:text-primary hover:underline ${focusRingClass}`}
      >
        {group.iocValue}
      </a>
      <button
        type="button"
        aria-label={`Copy ${group.iocValue}`}
        onClick={() => onCopy(group.iocValue)}
        className={`rounded p-1 text-outline opacity-0 transition-all group-hover:opacity-100 hover:bg-surface-container hover:text-on-surface focus-visible:opacity-100 ${focusRingClass}`}
      >
        <span
          aria-hidden="true"
          className="material-symbols-outlined text-[14px]"
        >
          content_copy
        </span>
      </button>
    </div>
  )
}

function TypeCell({ group }: { group: IocGroup }) {
  return (
    <span
      className={`rounded px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider ${iocTypeBadgeClass[group.iocType]}`}
    >
      {iocTypeLabels[group.iocType]}
    </span>
  )
}

function ThreatCell({ group }: { group: IocGroup }) {
  const label = threatLabel(group)
  if (!label) return <span className="text-xs text-outline">—</span>
  return <span className="text-xs font-medium text-on-surface">{label}</span>
}

function ProvidersCell({ group }: { group: IocGroup }) {
  const providers = providersOf(group)
  return (
    <div className="flex flex-wrap items-center gap-1">
      {providers.map((provider) => (
        <span
          key={provider}
          className="rounded bg-surface-container-high px-1.5 py-0.5 font-mono text-[10px] font-medium text-on-surface-variant"
        >
          {provider}
        </span>
      ))}
    </div>
  )
}

function DateCell({ value }: { value: string | null | undefined }) {
  return (
    <span className="font-mono text-[10px] text-outline">
      {formatShortLocal(value) ?? '—'}
    </span>
  )
}

function ActionCell({
  group,
  handlers,
}: {
  group: IocGroup
  handlers: RefObject<Handlers>
}) {
  const { onOpenDetail } = handlers.current
  return (
    <a
      href={detailHref(group.id)}
      onClick={(event) => {
        event.preventDefault()
        onOpenDetail(group.id)
      }}
      className={`inline-flex items-center gap-1 rounded px-2 py-1 font-mono text-[10px] font-semibold uppercase tracking-wider text-primary transition-colors hover:bg-surface-container ${focusRingClass}`}
    >
      Analyze
      <span
        aria-hidden="true"
        className="material-symbols-outlined text-[14px]"
      >
        arrow_forward
      </span>
    </a>
  )
}

/**
 * Client-only DataTables surface. It is the sole owner of browse-data requests:
 * DataTables invokes the Ajax function for the initial draw and for each page,
 * size, or ordering interaction, and the function services exactly that draw.
 */
const BrowseDataTable = forwardRef<BrowseTableHandle, BrowseDataTableProps>(
  function BrowseDataTable(
    { params, onTableParams, onStatus, onCopy, onOpenDetail },
    ref,
  ) {
    const wrapperRef = useRef<DataTableRef | null>(null)
    const paramsRef = useRef(params)
    // Mirrors the latest prop without being clobbered by an in-flight draw.
    const latestParamsRef = useRef(params)
    latestParamsRef.current = params
    const paramsFingerprint = browseParamsFingerprint(params)
    const syncRef = useRef(initialBrowseSyncState)
    const abortRef = useRef<AbortController | null>(null)
    const latestDrawRef = useRef<number | null>(null)
    // One stable object whose fields are refreshed every render, so slot
    // closures captured at DataTables init always call the latest handlers.
    const handlersRef = useRef<Handlers>({
      onTableParams,
      onStatus,
      onCopy,
      onOpenDetail,
    })
    handlersRef.current.onTableParams = onTableParams
    handlersRef.current.onStatus = onStatus
    handlersRef.current.onCopy = onCopy
    handlersRef.current.onOpenDetail = onOpenDetail

    // The table is initialized once from the first represented view.
    const initialTableState = useRef(browseTableStateFromParams(params)).current

    const ajax = useCallback(
      (data: unknown, callback: (json: unknown) => void) => {
        const active = paramsRef.current
        const adaptation = adaptBrowseRequest(data as DataTablesRequest, active)

        if (!adaptation.ok) {
          callback(emptyDataTablesResponse(adaptation.draw))
          handlersRef.current.onStatus({
            kind: 'error',
            error: new Error(
              'The table requested a page outside the supported browse contract.',
            ),
          })
          return
        }

        const { draw, params: nextParams } = adaptation
        if (
          browseParamsFingerprint(nextParams) !==
          browseParamsFingerprint(active)
        ) {
          // Table-originated page or ordering action: reflect it in the URL and
          // guard the reflected navigation against a second draw.
          syncRef.current = onBrowseTableAction(syncRef.current, nextParams)
          paramsRef.current = nextParams
          handlersRef.current.onTableParams(nextParams)
        }

        abortRef.current?.abort()
        const controller = new AbortController()
        abortRef.current = controller
        latestDrawRef.current = draw
        handlersRef.current.onStatus({ kind: 'pending' })

        browseIocs(nextParams, controller.signal).then(
          (result) => {
            if (controller.signal.aborted || latestDrawRef.current !== draw)
              return
            callback(adaptBrowseResponse(draw, result))
            handlersRef.current.onStatus(
              result.items.length === 0
                ? { kind: 'empty', totalCount: result.totalCount }
                : {
                    kind: 'success',
                    shown: result.items.length,
                    totalCount: result.totalCount,
                    page: result.page,
                    pageSize: result.pageSize,
                  },
            )
          },
          (error: unknown) => {
            // Cancellation is supersession, never a user-visible failure.
            if (controller.signal.aborted || isAbortError(error)) return
            if (latestDrawRef.current !== draw) return
            // Settle the draw so DataTables clears processing, but suppress its
            // empty presentation; the route shows the error surface instead.
            callback(emptyDataTablesResponse(draw))
            handlersRef.current.onStatus(
              error instanceof ApiError && error.status === 403
                ? { kind: 'forbidden' }
                : { kind: 'error', error },
            )
          },
        )
      },
      [],
    )

    const columns = useMemo<NonNullable<Config['columns']>>(
      () =>
        browseTableColumns.map((column) => ({
          data: column.orderBy,
          name: column.name,
          orderable: column.orderable,
          searchable: column.searchable,
        })),
      [],
    )

    const slots = useMemo(
      () => ({
        0: (_data: unknown, row: IocGroup) => (
          <IndicatorCell group={row} handlers={handlersRef} />
        ),
        1: (_data: unknown, row: IocGroup) => <TypeCell group={row} />,
        2: (_data: unknown, row: IocGroup) => <ThreatCell group={row} />,
        3: (_data: unknown, row: IocGroup) => <ProvidersCell group={row} />,
        4: (_data: unknown, row: IocGroup) => (
          <DateCell value={row.firstSeenUtc} />
        ),
        5: (_data: unknown, row: IocGroup) => (
          <DateCell value={row.lastSeenUtc} />
        ),
        6: (_data: unknown, row: IocGroup) => (
          <ActionCell group={row} handlers={handlersRef} />
        ),
      }),
      [],
    )

    const options = useMemo<Config>(
      () => ({
        serverSide: true,
        processing: true,
        paging: true,
        searching: false,
        orderMulti: false,
        stateSave: false,
        autoWidth: false,
        displayStart: initialTableState.displayStart,
        pageLength: initialTableState.pageLength,
        order: [initialTableState.order],
        layout: {
          topStart: null,
          topEnd: null,
          bottomStart: null,
          bottomEnd: 'paging',
        },
        createdRow: (row: Node) => {
          rowClass.split(' ').forEach((token) => {
            ;(row as HTMLElement).classList.add(token)
          })
        },
      }),
      [initialTableState],
    )

    useImperativeHandle(
      ref,
      () => ({
        reload: () => {
          wrapperRef.current?.dt()?.ajax.reload()
        },
      }),
      [],
    )

    useEffect(() => {
      const nextParams = latestParamsRef.current
      const outcome = onBrowseUrlChange(syncRef.current, nextParams)
      syncRef.current = outcome.state
      paramsRef.current = nextParams

      if (outcome.kind === 'initialize' || outcome.kind === 'reflect') return
      if (outcome.kind === 'ignore') return

      const dt = wrapperRef.current?.dt()
      if (!dt) return
      // Apply the represented state without intermediate draws, then issue
      // exactly one request for the new view.
      abortRef.current?.abort()
      dt.page.len(outcome.table.pageLength)
      dt.order([outcome.table.order[0], outcome.table.order[1]])
      dt.page(Math.floor(outcome.table.displayStart / outcome.table.pageLength))
      dt.draw(false)
      // Re-runs only when the normalized view actually changes, not on every
      // transient render during a table-originated navigation.
    }, [paramsFingerprint])

    useEffect(
      () => () => {
        abortRef.current?.abort()
      },
      [],
    )

    return (
      <DataTables
        ref={wrapperRef}
        options={options}
        ajax={ajax}
        columns={columns}
        slots={slots}
        className="w-full border-collapse text-left"
      >
        <thead>
          <tr className="h-9 select-none border-b border-outline-variant/60 bg-surface-container-low font-mono text-[10px] uppercase tracking-wider text-outline">
            <th scope="col" className={headerClass}>
              Indicator
            </th>
            <th scope="col" className={headerClass}>
              Type
            </th>
            <th scope="col" className={headerClass}>
              Threat family / campaign
            </th>
            <th scope="col" className={headerClass}>
              Providers
            </th>
            <th scope="col" className={headerClass}>
              First seen
            </th>
            <th scope="col" className={headerClass}>
              Last seen
            </th>
            <th scope="col" className={`${headerClass} text-right`}>
              Action
            </th>
          </tr>
        </thead>
      </DataTables>
    )
  },
)

export default BrowseDataTable
