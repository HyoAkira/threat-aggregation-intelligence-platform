import { BROWSE_PAGE_SIZE_MAX, BROWSE_PAGE_SIZE_MIN } from './browseParams'
import type { BrowseDir, BrowseOrderBy, BrowseParams } from './browseParams'
import type { IocGroup, PagedIocGroups } from './schemas'

/**
 * Closed allowlist of browse endpoint ordering fields, in column order. Only
 * these four columns are orderable; provider, threat-label, and action columns
 * are presentation-only and must never reach the API ordering contract.
 */
export const browseTableColumns = [
  { name: 'iocValue', orderBy: 'iocValue', orderable: true, searchable: false },
  { name: 'iocType', orderBy: 'iocType', orderable: true, searchable: false },
  { name: 'threatLabel', orderBy: null, orderable: false, searchable: false },
  { name: 'providers', orderBy: null, orderable: false, searchable: false },
  {
    name: 'firstSeenUtc',
    orderBy: 'firstSeenUtc',
    orderable: true,
    searchable: false,
  },
  {
    name: 'lastSeenUtc',
    orderBy: 'lastSeenUtc',
    orderable: true,
    searchable: false,
  },
  { name: 'actions', orderBy: null, orderable: false, searchable: false },
] as const satisfies readonly {
  name: string
  orderBy: BrowseOrderBy | null
  orderable: boolean
  searchable: boolean
}[]

export type BrowseTableColumnName = (typeof browseTableColumns)[number]['name']

/** Every browse ordering field the endpoint accepts, derived from the allowlist. */
export const browseOrderableFields = browseTableColumns
  .map((column) => column.orderBy)
  .filter((field): field is BrowseOrderBy => field !== null)

function buildOrderMaps() {
  const indexByField = {} as Record<BrowseOrderBy, number>
  const fieldByIndex: Record<number, BrowseOrderBy> = {}
  const fieldByName: Record<string, BrowseOrderBy> = {}
  browseTableColumns.forEach((column, index) => {
    if (!column.orderBy) return
    indexByField[column.orderBy] = index
    fieldByIndex[index] = column.orderBy
    fieldByName[column.name] = column.orderBy
  })
  return { indexByField, fieldByIndex, fieldByName }
}

const orderMaps = buildOrderMaps()

/** DataTables column index for each supported ordering field. */
export const browseOrderIndexByField = orderMaps.indexByField

/** Supported ordering field for each DataTables column index. */
export const browseOrderFieldByIndex = orderMaps.fieldByIndex

/** Supported ordering field for each DataTables column name. */
export const browseOrderFieldByName = orderMaps.fieldByName

/** One DataTables server-side ordering term as sent in the Ajax request. */
export type DataTablesOrderTerm = {
  column?: number | string
  name?: string | null
  dir?: string
}

/** The subset of the DataTables server-side request the adapter consumes. */
export type DataTablesRequest = {
  draw?: number
  start?: number
  length?: number
  order?: DataTablesOrderTerm[]
}

/** The server-side response envelope DataTables expects from the adapter. */
export type DataTablesResponse<T> = {
  draw: number
  data: T[]
  recordsTotal: number
  recordsFiltered: number
}

export type BrowseRequestRejection =
  'invalid_draw' | 'invalid_start' | 'unsupported_length'

export type BrowseRequestAdaptation =
  | { ok: true; draw: number; params: BrowseParams }
  | { ok: false; draw: number; reason: BrowseRequestRejection }

function adaptationError(
  draw: number,
  reason: BrowseRequestRejection,
): BrowseRequestAdaptation {
  return { ok: false, draw, reason }
}

/**
 * Resolves the first-order term against the closed allowlist. Unsupported
 * columns and unknown directions keep the active parameters, so arbitrary
 * DataTables column data is never forwarded to the API. Multiple order terms
 * are ignored beyond the first because the browse contract is single-field.
 */
function orderFromTerm(
  term: DataTablesOrderTerm | undefined,
  active: BrowseParams,
): { orderBy: BrowseOrderBy; dir: BrowseDir } {
  const fallback = { orderBy: active.orderBy, dir: active.dir }
  if (!term) return fallback

  const byIndex =
    typeof term.column === 'number'
      ? browseOrderFieldByIndex[term.column]
      : undefined
  const byName =
    typeof term.name === 'string'
      ? browseOrderFieldByName[term.name]
      : typeof term.column === 'string'
        ? browseOrderFieldByName[term.column]
        : undefined
  const orderBy = byIndex ?? byName
  if (!orderBy) return fallback

  const dir: BrowseDir =
    term.dir === 'asc' || term.dir === 'desc' ? term.dir : active.dir
  return { orderBy, dir }
}

/**
 * Pure DataTables request -> browse request adapter. Requires an integer
 * `draw`, a non-negative integer `start`, and a positive `length` inside the
 * browse page bounds (so `length === -1`, "all rows", is rejected). The page is
 * derived from `start` and `length`; active type, provider, tag, and
 * observed-period filters are preserved unchanged.
 */
export function adaptBrowseRequest(
  request: DataTablesRequest,
  active: BrowseParams,
): BrowseRequestAdaptation {
  const draw = request.draw
  if (typeof draw !== 'number' || !Number.isInteger(draw)) {
    return adaptationError(0, 'invalid_draw')
  }

  const start = request.start
  if (typeof start !== 'number' || !Number.isInteger(start) || start < 0) {
    return adaptationError(draw, 'invalid_start')
  }

  const length = request.length
  if (
    typeof length !== 'number' ||
    !Number.isInteger(length) ||
    length < BROWSE_PAGE_SIZE_MIN ||
    length > BROWSE_PAGE_SIZE_MAX
  ) {
    return adaptationError(draw, 'unsupported_length')
  }

  const order = orderFromTerm(request.order?.[0], active)
  return {
    ok: true,
    draw,
    params: {
      ...active,
      orderBy: order.orderBy,
      dir: order.dir,
      page: Math.floor(start / length) + 1,
      pageSize: length,
    },
  }
}

/**
 * Pure browse result -> DataTables response adapter. `recordsTotal` and
 * `recordsFiltered` both map to the server matching count because the endpoint
 * supplies a single filtered count and DataTables search/column filtering is
 * disabled; the table's dataset is defined by the external browse filters.
 */
export function adaptBrowseResponse(
  draw: number,
  result: PagedIocGroups,
): DataTablesResponse<IocGroup> {
  return {
    draw,
    data: result.items,
    recordsTotal: result.totalCount,
    recordsFiltered: result.totalCount,
  }
}

/** Empty payload that lets DataTables settle a draw without showing rows. */
export function emptyDataTablesResponse(
  draw: number,
): DataTablesResponse<IocGroup> {
  return { draw, data: [], recordsTotal: 0, recordsFiltered: 0 }
}
