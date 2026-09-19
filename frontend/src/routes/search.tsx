import { useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { ApiError, searchIocs } from '../lib/apiClient'
import type { SearchSubmission } from '../lib/apiClient'
import { iocSearchKey } from '../lib/queryKeys'
import { focusRingClass } from '../components/AppShell'
import { IocGroupCard } from '../components/IocGroupCard'
import { TagAutocompleteInput } from '../components/TagAutocompleteInput'
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
}

export type SearchValidation =
  | { submission: SearchSubmission; error: null }
  | { submission: null; error: string }

/**
 * Local validation only rejects what the page contract requires: an empty
 * indicator. Everything else (syntax, classification, normalization) is decided
 * by the API response, and the user never chooses an IOC type.
 */
export function validateSearchSubmission(
  values: SearchFormValues,
): SearchValidation {
  const q = values.q.trim()
  if (q === '')
    return { submission: null, error: 'Enter an indicator to search.' }
  if (q.length > 2048)
    return { submission: null, error: 'The indicator is too long.' }

  const tag = values.tag.trim()
  if (tag.length > 100)
    return { submission: null, error: 'The tag filter is too long.' }

  const providerRaw = values.provider.trim()
  if (providerRaw.length > 255)
    return { submission: null, error: 'The provider filter is too long.' }
  const provider = providerRaw === '' ? null : providerRaw

  return {
    submission: { q, tag: tag === '' ? null : tag, provider },
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

const fieldLabelClass =
  'font-mono text-[10px] font-semibold uppercase tracking-wider text-outline'
const inputClass = `mt-1 w-full rounded border border-outline-variant bg-surface-container-lowest px-3 py-2 font-mono text-sm text-on-surface placeholder:text-outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary`
const SEARCH_PAGE_SIZE = 20

function SearchPage() {
  const [values, setValues] = useState<SearchFormValues>({
    q: '',
    tag: '',
    provider: '',
  })
  const [formError, setFormError] = useState<string | null>(null)
  const [submission, setSubmission] = useState<SearchSubmission | null>(null)
  const [page, setPage] = useState(1)

  const pagedSubmission = submission
    ? { ...submission, page, pageSize: SEARCH_PAGE_SIZE }
    : null

  const query = useQuery({
    queryKey: iocSearchKey(
      pagedSubmission ?? { q: '', tag: null, provider: null },
    ),
    queryFn: ({ signal }) => searchIocs(pagedSubmission!, signal),
    enabled: submission !== null,
  })

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const validation = validateSearchSubmission(values)
    setFormError(validation.error)
    setPage(1)
    setSubmission(validation.submission)
  }

  const busy = submission !== null && query.isPending

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
            domain, or an HTTP(S) URL. The type is detected automatically.
          </p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4 p-5" noValidate>
          <div>
            <label htmlFor="search-q" className={fieldLabelClass}>
              Indicator (IPv4, IPv6, domain, or HTTP(S) URL)
            </label>
            <div className="mt-1 flex items-center gap-2 rounded border border-outline-variant bg-surface-container-lowest px-3 py-2 focus-within:border-primary">
              <span
                aria-hidden="true"
                className="material-symbols-outlined text-[20px] text-primary"
              >
                search
              </span>
              <input
                id="search-q"
                name="q"
                type="text"
                required
                autoComplete="off"
                spellCheck={false}
                value={values.q}
                onChange={(event) =>
                  setValues((current) => ({
                    ...current,
                    q: event.target.value,
                  }))
                }
                className="w-full bg-transparent font-mono text-sm text-on-surface outline-none placeholder:text-outline"
                placeholder="194.26.29.112, evil-domain.ru, https://…"
              />
            </div>
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
              />
            </div>
            <div>
              <label htmlFor="search-provider" className={fieldLabelClass}>
                Provider filter (optional)
              </label>
              <input
                id="search-provider"
                name="provider"
                type="text"
                value={values.provider}
                onChange={(event) =>
                  setValues((current) => ({
                    ...current,
                    provider: event.target.value,
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
        {submission && query.isError ? (
          <SearchErrorState
            error={query.error}
            q={submission.q}
            onRetry={() => void query.refetch()}
          />
        ) : null}
        {query.data ? (
          <div className="space-y-3">
            <h2 className="flex flex-wrap items-baseline gap-x-2 text-sm font-semibold uppercase tracking-wider text-on-surface-variant">
              {query.data.totalCount === 1 ? 'Result' : 'Results'} for
              <span className="font-mono normal-case text-on-surface">
                {submission?.q}
              </span>
              <span className="font-mono text-xs normal-case text-outline">
                ({query.data.totalCount})
              </span>
            </h2>
            {query.data.items.map((group) => (
              <IocGroupCard
                key={`${group.iocType}:${group.iocValue}`}
                group={group}
                linkToDetail
              />
            ))}
            {query.data.totalCount > SEARCH_PAGE_SIZE ? (
              <Pagination
                page={query.data.page}
                pageSize={query.data.pageSize}
                totalCount={query.data.totalCount}
                onPage={setPage}
                pageLabel="Search page"
              />
            ) : null}
          </div>
        ) : null}
      </div>
    </section>
  )
}
