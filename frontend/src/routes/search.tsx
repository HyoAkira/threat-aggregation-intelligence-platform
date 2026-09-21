import { useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { ApiError, searchIocs, searchIocsBulk } from '../lib/apiClient'
import type { BulkSearchSubmission, SearchSubmission } from '../lib/apiClient'
import { iocSearchBulkKey, iocSearchKey } from '../lib/queryKeys'
import type { BulkSearchItem, BulkSearchResult } from '../lib/schemas'
import { focusRingClass } from '../components/AppShell'
import { IocGroupCard } from '../components/IocGroupCard'
import { IocMultilineSearchInput } from '../components/IocMultilineSearchInput'
import {
  TagAutocompleteInput,
  CatalogAutocompleteInput,
} from '../components/TagAutocompleteInput'
import { ProviderAutocompleteInput } from '../components/ProviderAutocompleteInput'
import { Pagination } from '../components/Pagination'
import {
  EmptyState,
  ErrorState,
  ForbiddenState,
  LoadingState,
} from '../components/uiStates'

export const Route = createFileRoute('/search')({
  component: SearchPage,
})

export type SearchFormValues = {
  q: string
  tag: string
  provider: string
  malware?: string
  target?: string
}

/** Bulk submission before the page assigns shared paging bounds. */
export type BulkSearchDraft = {
  malware?: string | null
  target?: string | null
  queries: string[]
  tag: string | null
  provider: string | null
}

export type SearchValidation =
  | { submission: SearchSubmission; bulk: null; error: null }
  | { submission: null; bulk: BulkSearchDraft; error: null }
  | { submission: null; bulk: null; error: string }

export const MAX_BULK_QUERIES = 100

/** Splits the editor into trimmed, non-blank values preserving order and duplicates. */
export function normalizeSearchInputs(q: string): string[] {
  return q
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
}

/**
 * Local validation only rejects what the page contract requires: at least one
 * non-blank indicator, bounded values, and optional filters. Everything else
 * (syntax, classification, normalization) is decided by the API response, and
 * the user never chooses an IOC type. One value uses the existing exact search;
 * two or more use the bulk-search contract.
 */
export function validateSearchSubmission(
  values: SearchFormValues,
): SearchValidation {
  const inputs = normalizeSearchInputs(values.q)
  if (inputs.length === 0)
    return {
      submission: null,
      bulk: null,
      error: 'Enter an indicator to search.',
    }
  if (inputs.some((input) => input.length > 2048))
    return { submission: null, bulk: null, error: 'The indicator is too long.' }
  if (inputs.length > MAX_BULK_QUERIES)
    return {
      submission: null,
      bulk: null,
      error: `Search up to ${MAX_BULK_QUERIES} indicators at a time.`,
    }

  const tags = Array.from(
    new Set(
      values.tag
        .split(',')
        .map((item) => item.trim().toLowerCase())
        .filter(Boolean),
    ),
  )
  if (tags.some((tag) => tag.length > 100))
    return {
      submission: null,
      bulk: null,
      error: 'The tag filter is too long.',
    }
  const tag = tags.join(',') === '' ? null : tags.join(',')

  const providerRaw = values.provider.trim()
  if (providerRaw.length > 255)
    return {
      submission: null,
      bulk: null,
      error: 'The provider filter is too long.',
    }
  const provider = providerRaw === '' ? null : providerRaw

  const malwareRaw = (values.malware ?? '').trim()
  if (malwareRaw.length > 200)
    return {
      submission: null,
      bulk: null,
      error: 'The malware filter is too long.',
    }
  const malware = malwareRaw === '' ? null : malwareRaw

  const targetRaw = (values.target ?? '').trim()
  if (targetRaw.length > 500)
    return {
      submission: null,
      bulk: null,
      error: 'The target filter is too long.',
    }
  const target = targetRaw === '' ? null : targetRaw

  if (inputs.length === 1) {
    return {
      submission: { q: inputs[0], tag, provider, malware, target },
      bulk: null,
      error: null,
    }
  }

  return {
    submission: null,
    bulk: { queries: inputs, tag, provider, malware, target },
    error: null,
  }
}

/** Error state that keeps 400 (invalid syntax) distinct from 404 (valid but absent). */
export function SearchErrorState({
  error,
  q,
  onRetry,
}: {
  error: unknown
  q: string
  onRetry: () => void
}) {
  if (error instanceof ApiError) {
    if (error.status === 400) {
      return (
        <ErrorState
          title="Invalid indicator"
          message={
            error.problem?.detail ??
            'The submitted value is not a supported IPv4, IPv6, domain, or HTTP(S) URL.'
          }
        />
      )
    }
    if (error.status === 404) {
      return (
        <EmptyState
          title="No indicator found"
          message={`No stored indicator matches “${q}”. It may be valid, but nothing has been reported for it.`}
        />
      )
    }
    if (error.status === 403) {
      return (
        <ForbiddenState message="You do not have permission to search indicators." />
      )
    }
    return (
      <ErrorState
        title="Search failed"
        message={error.message}
        correlationId={error.correlationId}
        onRetry={onRetry}
      />
    )
  }
  return (
    <ErrorState
      title="Search failed"
      message="The request could not be completed."
      onRetry={onRetry}
    />
  )
}

/**
 * Bulk failures are page-level transport or envelope failures only: mixed per-item statuses are
 * rendered as data, never as an error. Unexpected failures stay retryable and show the
 * correlation ID when the API supplied one.
 */
export function BulkSearchErrorState({
  error,
  onRetry,
}: {
  error: unknown
  onRetry: () => void
}) {
  if (error instanceof ApiError) {
    if (error.status === 403) {
      return (
        <ForbiddenState message="You do not have permission to search indicators." />
      )
    }
    return (
      <ErrorState
        title="Search failed"
        message={error.problem?.detail ?? error.message}
        correlationId={error.correlationId}
        onRetry={onRetry}
      />
    )
  }
  return (
    <ErrorState
      title="Search failed"
      message="The request could not be completed."
      onRetry={onRetry}
    />
  )
}

const statusLabel: Record<BulkSearchItem['status'], string> = {
  matched: 'Matched',
  not_found: 'Not found',
  invalid: 'Invalid',
}

const statusBadgeClass: Record<BulkSearchItem['status'], string> = {
  matched: 'bg-primary/10 text-primary',
  not_found: 'bg-surface-container-highest text-on-surface-variant',
  invalid: 'bg-error-container text-on-error-container',
}

/** One submitted line's outcome: matched groups, or a status explanation and no result card. */
export function BulkResultItem({ item }: { item: BulkSearchItem }) {
  return (
    <section
      className="overflow-hidden rounded border border-outline-variant"
      aria-label={`Bulk result for ${item.input}`}
    >
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-outline-variant/60 bg-surface-container-low/60 px-4 py-2">
        <h3 className="break-all font-mono text-sm font-semibold text-on-surface">
          {item.input}
        </h3>
        <span
          className={`rounded px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider ${statusBadgeClass[item.status]}`}
        >
          {statusLabel[item.status]}
        </span>
      </div>
      {item.status === 'matched' ? (
        <div className="space-y-3 p-4">
          {item.groups.map((group) => (
            <IocGroupCard
              key={`${group.iocType}:${group.iocValue}`}
              group={group}
              headingLevel="h3"
              linkToDetail
            />
          ))}
        </div>
      ) : (
        <p className="px-4 py-3 text-sm text-on-surface-variant">
          {item.status === 'invalid'
            ? (item.error?.detail ??
              'This value could not be recognized as a supported IOC.')
            : (item.error?.detail ?? 'No stored indicator matches this value.')}
        </p>
      )}
    </section>
  )
}

const fieldLabelClass =
  'font-mono text-[10px] font-semibold uppercase tracking-wider text-outline'
const inputClass = `mt-1 w-full rounded border border-outline-variant bg-surface-container-lowest px-3 py-2 font-mono text-sm text-on-surface placeholder:text-outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary`
const SEARCH_PAGE_SIZE = 20

/**
 * Bulk response presentation: an overall summary, one section per submitted value in server order,
 * and shared page navigation when any matched value has additional pages. Input order is never
 * merged or reordered, and invalid/not-found values never render a result card.
 */
export function BulkResults({
  data,
  page,
  onPage,
}: {
  data: BulkSearchResult
  page: number
  onPage: (page: number) => void
}) {
  const hasMorePages = data.items.some(
    (item) => item.totalCount > SEARCH_PAGE_SIZE,
  )

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-sm font-semibold uppercase tracking-wider text-on-surface-variant">
          Results for {data.totalCount}{' '}
          {data.totalCount === 1 ? 'indicator' : 'indicators'}
        </h2>
        <p
          aria-live="polite"
          className="mt-1 font-mono text-xs text-on-surface-variant"
        >
          {data.matchedCount} matched · {data.notFoundCount} not found ·{' '}
          {data.invalidCount} invalid
        </p>
      </div>
      {data.items.map((item, index) => (
        <BulkResultItem key={`${index}:${item.input}`} item={item} />
      ))}
      {hasMorePages ? (
        <Pagination
          page={page}
          pageSize={SEARCH_PAGE_SIZE}
          totalCount={data.items.reduce(
            (total, item) => Math.max(total, item.totalCount),
            0,
          )}
          onPage={onPage}
          pageLabel="Search page"
        />
      ) : null}
    </div>
  )
}

function SearchPage() {
  const [values, setValues] = useState<SearchFormValues>({
    q: '',
    tag: '',
    provider: '',
    malware: '',
    target: '',
  })
  const [formError, setFormError] = useState<string | null>(null)
  const [submission, setSubmission] = useState<SearchSubmission | null>(null)
  const [bulkSubmission, setBulkSubmission] = useState<BulkSearchDraft | null>(
    null,
  )
  const [page, setPage] = useState(1)

  const pagedSubmission = submission
    ? { ...submission, page, pageSize: SEARCH_PAGE_SIZE }
    : null
  const pagedBulk: BulkSearchSubmission | null = bulkSubmission
    ? { ...bulkSubmission, page, pageSize: SEARCH_PAGE_SIZE }
    : null

  const singleQuery = useQuery({
    queryKey: iocSearchKey(
      pagedSubmission ?? { q: '', tag: null, provider: null },
    ),
    queryFn: ({ signal }) => searchIocs(pagedSubmission!, signal),
    enabled: submission !== null,
  })

  const bulkQuery = useQuery({
    queryKey: iocSearchBulkKey(
      pagedBulk ?? {
        queries: [],
        tag: null,
        provider: null,
        page: 1,
        pageSize: SEARCH_PAGE_SIZE,
      },
    ),
    queryFn: ({ signal }) => searchIocsBulk(pagedBulk!, signal),
    enabled: bulkSubmission !== null,
  })

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const validation = validateSearchSubmission(values)
    setFormError(validation.error)
    setPage(1)
    setSubmission(validation.submission)
    setBulkSubmission(validation.bulk)
  }

  const busy =
    (submission !== null && singleQuery.isPending) ||
    (bulkSubmission !== null && bulkQuery.isPending)

  return (
    <section aria-labelledby="search-heading">
      <div className="overflow-hidden rounded border border-outline-variant bg-surface-container-lowest">
        <div className="border-b border-outline-variant/60 bg-surface-container-low/60 px-5 py-4">
          <div className="flex items-center gap-2">
            <span
              aria-hidden="true"
              className="h-2 w-2 rounded-full bg-primary"
            />
            <span className={fieldLabelClass}>Indicator and host lookup</span>
          </div>
          <h1
            id="search-heading"
            className="mt-2 text-2xl font-semibold tracking-tight text-on-surface"
          >
            Search indicators
          </h1>
          <p className="mt-1 max-w-2xl text-sm text-on-surface-variant">
            Search for an IPv4 or IPv6 address, a domain, URL host, parent
            domain, or an HTTP(S) URL. Paste one indicator per line to search
            several at once; the type is detected automatically.
          </p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4 p-5" noValidate>
          <div>
            <label htmlFor="search-q" className={fieldLabelClass}>
              Indicator (IPv4, IPv6, domain, or HTTP(S) URL)
            </label>
            <IocMultilineSearchInput
              id="search-q"
              name="q"
              value={values.q}
              onChange={(q) => setValues((current) => ({ ...current, q }))}
              className={`${inputClass} min-h-24`}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label htmlFor="search-tag" className={fieldLabelClass}>
                Tag filter (optional)
              </label>
              <TagAutocompleteInput
                id="search-tag"
                value={values.tag}
                onChange={(tag) =>
                  setValues((current) => ({ ...current, tag }))
                }
                className={inputClass}
                multiple
              />
            </div>
            <div>
              <label htmlFor="search-provider" className={fieldLabelClass}>
                Provider filter (optional)
              </label>
              <ProviderAutocompleteInput
                id="search-provider"
                name="provider"
                value={values.provider}
                onChange={(provider) =>
                  setValues((current) => ({ ...current, provider }))
                }
                className={inputClass}
              />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label htmlFor="search-malware" className={fieldLabelClass}>
                Malware filter (optional)
              </label>
              <CatalogAutocompleteInput
                kind="malware"
                id="search-malware"
                name="malware"
                value={values.malware ?? ''}
                maxLength={200}
                onChange={(value) =>
                  setValues((current) => ({
                    ...current,
                    malware: value,
                  }))
                }
                className={inputClass}
              />
            </div>
            <div>
              <label htmlFor="search-target" className={fieldLabelClass}>
                Target filter (optional)
              </label>
              <CatalogAutocompleteInput
                kind="target"
                id="search-target"
                name="target"
                value={values.target ?? ''}
                maxLength={500}
                onChange={(value) =>
                  setValues((current) => ({
                    ...current,
                    target: value,
                  }))
                }
                className={inputClass}
              />
            </div>
          </div>

          {formError ? (
            <ErrorState title="Check your input" message={formError} />
          ) : null}

          <button
            type="submit"
            disabled={busy}
            aria-busy={busy}
            className={`inline-flex items-center gap-2 rounded bg-primary px-4 py-2 font-mono text-xs font-semibold uppercase tracking-wider text-on-primary transition-colors hover:bg-primary-container disabled:cursor-wait disabled:opacity-60 ${focusRingClass}`}
          >
            <span>{busy ? 'Searching…' : 'Search'}</span>
          </button>
        </form>
      </div>

      <div className="mt-6">
        {busy ? <LoadingState label="Searching…" /> : null}

        {submission && singleQuery.isError ? (
          <SearchErrorState
            error={singleQuery.error}
            q={submission.q}
            onRetry={() => void singleQuery.refetch()}
          />
        ) : null}
        {bulkSubmission && bulkQuery.isError ? (
          <BulkSearchErrorState
            error={bulkQuery.error}
            onRetry={() => void bulkQuery.refetch()}
          />
        ) : null}

        {submission && singleQuery.data ? (
          <div className="space-y-3">
            <h2 className="flex flex-wrap items-baseline gap-x-2 text-sm font-semibold uppercase tracking-wider text-on-surface-variant">
              {singleQuery.data.totalCount === 1 ? 'Result' : 'Results'} for
              <span className="font-mono normal-case text-on-surface">
                {submission.q}
              </span>
              <span className="font-mono text-xs normal-case text-outline">
                ({singleQuery.data.totalCount})
              </span>
            </h2>
            {singleQuery.data.items.map((group) => (
              <IocGroupCard
                key={`${group.iocType}:${group.iocValue}`}
                group={group}
                linkToDetail
              />
            ))}
            {singleQuery.data.totalCount > SEARCH_PAGE_SIZE ? (
              <Pagination
                page={singleQuery.data.page}
                pageSize={singleQuery.data.pageSize}
                totalCount={singleQuery.data.totalCount}
                onPage={setPage}
                pageLabel="Search page"
              />
            ) : null}
          </div>
        ) : null}

        {bulkSubmission && bulkQuery.data ? (
          <BulkResults data={bulkQuery.data} page={page} onPage={setPage} />
        ) : null}
      </div>
    </section>
  )
}
