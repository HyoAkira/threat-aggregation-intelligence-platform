import { z } from 'zod'
import { iocTypeTokens } from './schemas'
import type { IocTypeToken } from './schemas'

export const browseOrderByOptions = [
  'lastSeenUtc',
  'firstSeenUtc',
  'iocType',
  'iocValue',
] as const
export type BrowseOrderBy = (typeof browseOrderByOptions)[number]

export const browseDirOptions = ['asc', 'desc'] as const
export type BrowseDir = (typeof browseDirOptions)[number]

export const browsePageSizeOptions = [10, 20, 50, 100] as const

export const BROWSE_PAGE_SIZE_MIN = 1
export const BROWSE_PAGE_SIZE_MAX = 100

/** Preset "observed period" windows offered by the explorer filter rail. */
export const observedPeriodPresets = [
  { value: 'all', label: 'All time' },
  { value: '24h', label: 'Last 24h' },
  { value: '7d', label: 'Last 7 Days' },
  { value: '30d', label: 'Last 30 Days' },
] as const
export type ObservedPeriodPreset =
  (typeof observedPeriodPresets)[number]['value']

/** ISO instant for the lower bound of a preset window (open-ended upper bound). */
export function observedPeriodFrom(
  preset: Exclude<ObservedPeriodPreset, 'all'>,
  now: Date = new Date(),
): string {
  const windowMs: Record<Exclude<ObservedPeriodPreset, 'all'>, number> = {
    '24h': 24 * 60 * 60 * 1000,
    '7d': 7 * 24 * 60 * 60 * 1000,
    '30d': 30 * 24 * 60 * 60 * 1000,
  }
  return new Date(now.getTime() - windowMs[preset]).toISOString()
}

/** Fully normalized browse state; every value is one the E09 contract accepts. */
export type BrowseParams = {
  type: IocTypeToken | null
  provider: string | null
  tag: string | null
  lastSeenFrom: string | null
  lastSeenTo: string | null
  orderBy: BrowseOrderBy
  dir: BrowseDir
  page: number
  pageSize: number
}

export const defaultBrowseParams: BrowseParams = {
  type: null,
  provider: null,
  tag: null,
  lastSeenFrom: null,
  lastSeenTo: null,
  orderBy: 'lastSeenUtc',
  dir: 'desc',
  page: 1,
  pageSize: 20,
}

/**
 * Raw URL strings as accepted from the address bar; structural validation happens
 * in parse. The router's default search parser turns numeric params into
 * numbers, so values are coerced back to strings first.
 */
export const rawBrowseSearchSchema = z.object({
  type: z.coerce.string().optional(),
  provider: z.coerce.string().optional(),
  tag: z.coerce.string().optional(),
  lastSeenFrom: z.coerce.string().optional(),
  lastSeenTo: z.coerce.string().optional(),
  orderBy: z.coerce.string().optional(),
  dir: z.coerce.string().optional(),
  page: z.coerce.string().optional(),
  pageSize: z.coerce.string().optional(),
})

export type RawBrowseSearch = z.infer<typeof rawBrowseSearchSchema>

export type ParsedBrowseParams =
  { params: BrowseParams; issues: never[] } | { params: null; issues: string[] }

function isIsoInstant(value: string): boolean {
  return !Number.isNaN(new Date(value).getTime())
}

/**
 * Normalizes raw URL strings into validated browse state. Absent or empty values
 * fall back to defaults; present-but-unsupported values are reported as issues so
 * the page can show an invalid-filters state instead of silently changing the view.
 */
export function parseBrowseParams(raw: RawBrowseSearch): ParsedBrowseParams {
  const issues: string[] = []
  const params: BrowseParams = { ...defaultBrowseParams }

  const type = raw.type?.trim()
  if (type) {
    if ((iocTypeTokens as readonly string[]).includes(type)) {
      params.type = type as IocTypeToken
    } else {
      issues.push(`Unsupported indicator type “${type}”.`)
    }
  }

  const provider = raw.provider?.trim()
  if (provider) {
    params.provider = provider
  }

  const tag = raw.tag?.trim()
  if (tag) params.tag = tag

  const lastSeenFrom = raw.lastSeenFrom?.trim()
  if (lastSeenFrom) {
    if (isIsoInstant(lastSeenFrom)) {
      params.lastSeenFrom = lastSeenFrom
    } else {
      issues.push('The “last seen from” bound must be a valid date.')
    }
  }

  const lastSeenTo = raw.lastSeenTo?.trim()
  if (lastSeenTo) {
    if (isIsoInstant(lastSeenTo)) {
      params.lastSeenTo = lastSeenTo
    } else {
      issues.push('The “last seen to” bound must be a valid date.')
    }
  }

  const orderBy = raw.orderBy?.trim()
  if (orderBy) {
    if ((browseOrderByOptions as readonly string[]).includes(orderBy)) {
      params.orderBy = orderBy as BrowseOrderBy
    } else {
      issues.push(`Unsupported sort field “${orderBy}”.`)
    }
  }

  const dir = raw.dir?.trim()
  if (dir) {
    if ((browseDirOptions as readonly string[]).includes(dir)) {
      params.dir = dir as BrowseDir
    } else {
      issues.push(`Unsupported sort direction “${dir}”.`)
    }
  }

  const page = raw.page?.trim()
  if (page) {
    const value = Number(page)
    if (Number.isInteger(value) && value >= 1) {
      params.page = value
    } else {
      issues.push('Page must be a positive number.')
    }
  }

  const pageSize = raw.pageSize?.trim()
  if (pageSize) {
    const value = Number(pageSize)
    if (
      Number.isInteger(value) &&
      value >= BROWSE_PAGE_SIZE_MIN &&
      value <= BROWSE_PAGE_SIZE_MAX
    ) {
      params.pageSize = value
    } else {
      issues.push(
        `Page size must be between ${BROWSE_PAGE_SIZE_MIN} and ${BROWSE_PAGE_SIZE_MAX}.`,
      )
    }
  }

  return issues.length > 0 ? { params: null, issues } : { params, issues: [] }
}

/**
 * Serializes browse state to URL search parameters, omitting defaults and empty
 * filters so a copied URL stays minimal and restores the same view when parsed.
 */
export function browseParamsToSearch(
  params: BrowseParams,
): Record<string, string | undefined> {
  const search: Record<string, string | undefined> = {}
  if (params.type) search.type = params.type
  if (params.provider !== null) search.provider = String(params.provider)
  if (params.tag) search.tag = params.tag
  if (params.lastSeenFrom) search.lastSeenFrom = params.lastSeenFrom
  if (params.lastSeenTo) search.lastSeenTo = params.lastSeenTo
  if (params.orderBy !== defaultBrowseParams.orderBy)
    search.orderBy = params.orderBy
  if (params.dir !== defaultBrowseParams.dir) search.dir = params.dir
  if (params.page !== defaultBrowseParams.page)
    search.page = String(params.page)
  if (params.pageSize !== defaultBrowseParams.pageSize)
    search.pageSize = String(params.pageSize)
  return search
}

/** True when any filter differs from the default view (drives "Clear filters"). */
export function hasActiveBrowseFilters(params: BrowseParams): boolean {
  return (
    params.type !== null ||
    params.provider !== null ||
    params.tag !== null ||
    params.lastSeenFrom !== null ||
    params.lastSeenTo !== null ||
    params.orderBy !== defaultBrowseParams.orderBy ||
    params.dir !== defaultBrowseParams.dir ||
    params.pageSize !== defaultBrowseParams.pageSize
  )
}
