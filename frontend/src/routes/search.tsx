import { useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { ApiError, searchIocs } from '../lib/apiClient'
import type { SearchSubmission } from '../lib/apiClient'
import { iocSearchKey } from '../lib/queryKeys'
import { focusRingClass } from '../components/AppShell'
import { IocGroupCard } from '../components/IocGroupCard'
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
  let provider: number | null = null
  if (providerRaw !== '') {
    const parsed = Number(providerRaw)
    if (!Number.isInteger(parsed) || parsed < 1) {
      return {
        submission: null,
        error: 'Provider must be a positive provider id.',
      }
    }
    provider = parsed
  }

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

const inputClass =
  'mt-1 w-full rounded-md border border-slate-400 px-3 py-2 text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700'

function SearchPage() {
  const [values, setValues] = useState<SearchFormValues>({
    q: '',
    tag: '',
    provider: '',
  })
  const [formError, setFormError] = useState<string | null>(null)
  const [submission, setSubmission] = useState<SearchSubmission | null>(null)

  const query = useQuery({
    queryKey: iocSearchKey(submission ?? { q: '', tag: null, provider: null }),
    queryFn: ({ signal }) => searchIocs(submission!, signal),
    enabled: submission !== null,
  })

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const validation = validateSearchSubmission(values)
    setFormError(validation.error)
    setSubmission(validation.submission)
  }

  return (
    <section aria-labelledby="search-heading">
      <h1 id="search-heading" className="text-2xl font-bold text-slate-900">
        Search indicators
      </h1>
      <p className="mt-2 max-w-2xl text-sm text-slate-700">
        Search for an exact indicator: an IPv4 or IPv6 address, a domain, or an
        HTTP(S) URL. The type is detected automatically.
      </p>
      <form
        onSubmit={handleSubmit}
        className="mt-6 max-w-2xl space-y-4"
        noValidate
      >
        <div>
          <label
            htmlFor="search-q"
            className="block text-sm font-medium text-slate-800"
          >
            Indicator (IPv4, IPv6, domain, or HTTP(S) URL)
          </label>
          <input
            id="search-q"
            name="q"
            type="text"
            required
            value={values.q}
            onChange={(event) =>
              setValues((current) => ({ ...current, q: event.target.value }))
            }
            className={inputClass}
          />
        </div>
        <div className="flex flex-wrap gap-4">
          <div className="min-w-40 flex-1">
            <label
              htmlFor="search-tag"
              className="block text-sm font-medium text-slate-800"
            >
              Tag filter (optional)
            </label>
            <input
              id="search-tag"
              name="tag"
              type="text"
              value={values.tag}
              onChange={(event) =>
                setValues((current) => ({
                  ...current,
                  tag: event.target.value,
                }))
              }
              className={inputClass}
            />
          </div>
          <div className="min-w-40 flex-1">
            <label
              htmlFor="search-provider"
              className="block text-sm font-medium text-slate-800"
            >
              Provider id filter (optional)
            </label>
            <input
              id="search-provider"
              name="provider"
              type="number"
              min={1}
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
          disabled={query.isPending}
          aria-busy={query.isPending}
          className={`rounded-md bg-sky-800 px-4 py-2 font-medium text-white enabled:hover:bg-sky-900 disabled:cursor-wait disabled:opacity-60 ${focusRingClass}`}
        >
          {query.isPending ? 'Searching…' : 'Search'}
        </button>
      </form>

      <div className="mt-8">
        {submission && query.isPending ? (
          <LoadingState label="Searching…" />
        ) : null}
        {submission && query.isError ? (
          <SearchErrorState
            error={query.error}
            q={submission.q}
            onRetry={() => void query.refetch()}
          />
        ) : null}
        {query.data ? (
          <div className="space-y-4">
            <h2 className="text-lg font-semibold text-slate-900">
              Result for{' '}
              <span className="font-mono">{query.data.iocValue}</span>
            </h2>
            <IocGroupCard group={query.data} linkToDetail />
          </div>
        ) : null}
      </div>
    </section>
  )
}
