export type AdminDataTablesOrder = {
  column?: number | string
  name?: string | null
  dir?: string
}

export type AdminDataTablesRequest = {
  draw?: number
  start?: number
  length?: number
  order?: AdminDataTablesOrder[]
}

export type AdminDataTablesResponse<T> = {
  draw: number
  data: T[]
  recordsTotal: number
  recordsFiltered: number
}

export function emptyAdminDataTablesResponse<T>(
  draw: number,
): AdminDataTablesResponse<T> {
  return { draw, data: [], recordsTotal: 0, recordsFiltered: 0 }
}

export function dateRangeQuery(
  dateFrom: string,
  dateTo: string,
): { dateFrom: string | null; dateTo: string | null } {
  return {
    dateFrom: localDateBoundary(dateFrom, false),
    dateTo: localDateBoundary(dateTo, true),
  }
}

/**
 * Converts a browser date input into an ISO instant. The end value is
 * exclusive and advances one local calendar day, so DST transitions do not
 * turn a selected local date into the wrong UTC range.
 */
function localDateBoundary(
  value: string,
  endExclusive: boolean,
): string | null {
  const trimmed = value.trim()
  if (!trimmed) return null

  const date = new Date(`${trimmed}T00:00:00`)
  if (Number.isNaN(date.getTime())) return null
  if (endExclusive) date.setDate(date.getDate() + 1)
  return date.toISOString()
}
