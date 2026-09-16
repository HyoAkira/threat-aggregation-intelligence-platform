import { browseOrderIndexByField } from './browseDataTables'
import type { BrowseParams } from './browseParams'

/**
 * Canonical identity of a normalized browse view. Two URLs that normalize to
 * the same filters, ordering, page, and size share one fingerprint.
 */
export function browseParamsFingerprint(params: BrowseParams): string {
  return JSON.stringify({
    type: params.type,
    provider: params.provider,
    tag: params.tag,
    lastSeenFrom: params.lastSeenFrom,
    lastSeenTo: params.lastSeenTo,
    orderBy: params.orderBy,
    dir: params.dir,
    page: params.page,
    pageSize: params.pageSize,
  })
}

/** The DataTables state a normalized browse view maps onto. */
export type BrowseTableState = {
  displayStart: number
  pageLength: number
  order: [number, 'asc' | 'desc']
}

export function browseTableStateFromParams(
  params: BrowseParams,
): BrowseTableState {
  return {
    displayStart: (params.page - 1) * params.pageSize,
    pageLength: params.pageSize,
    order: [browseOrderIndexByField[params.orderBy], params.dir],
  }
}

/**
 * URL/table synchronization state. `represented` is the fingerprint the table
 * currently shows; `pendingTable` is the guard set when a table interaction is
 * the origin of the next URL change.
 */
export type BrowseSyncState = {
  initialized: boolean
  represented: string | null
  pendingTable: string | null
}

export const initialBrowseSyncState: BrowseSyncState = {
  initialized: false,
  represented: null,
  pendingTable: null,
}

/**
 * What the caller must do for a URL change:
 * - `initialize`/`apply` require exactly one draw (with the given table state);
 * - `reflect` consumes a table-originated guard and requires no draw;
 * - `ignore` is a no-op for an unchanged fingerprint.
 */
export type BrowseSyncOutcome =
  | { kind: 'initialize'; table: BrowseTableState; state: BrowseSyncState }
  | { kind: 'apply'; table: BrowseTableState; state: BrowseSyncState }
  | { kind: 'reflect'; state: BrowseSyncState }
  | { kind: 'ignore'; state: BrowseSyncState }

/** Pure state transition for an incoming (usually URL-derived) browse view. */
export function onBrowseUrlChange(
  state: BrowseSyncState,
  params: BrowseParams,
): BrowseSyncOutcome {
  const fingerprint = browseParamsFingerprint(params)

  if (!state.initialized) {
    return {
      kind: 'initialize',
      table: browseTableStateFromParams(params),
      state: {
        initialized: true,
        represented: fingerprint,
        pendingTable: null,
      },
    }
  }

  if (state.pendingTable === fingerprint) {
    return {
      kind: 'reflect',
      state: { ...state, represented: fingerprint, pendingTable: null },
    }
  }

  if (state.represented === fingerprint) {
    return { kind: 'ignore', state }
  }

  return {
    kind: 'apply',
    table: browseTableStateFromParams(params),
    state: { ...state, represented: fingerprint, pendingTable: null },
  }
}

/**
 * Pure transition for a table-originated page/order action. The draw that is
 * already in flight services the view, so the caller marks the guard and
 * updates the represented fingerprint without requesting another draw.
 */
export function onBrowseTableAction(
  state: BrowseSyncState,
  params: BrowseParams,
): BrowseSyncState {
  const fingerprint = browseParamsFingerprint(params)
  return { ...state, represented: fingerprint, pendingTable: fingerprint }
}
