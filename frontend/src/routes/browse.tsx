import { useEffect, useState } from 'react'
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { ApiError, browseIocs } from '../lib/apiClient'
import {
  browseDirOptions,
  browseOrderByOptions,
  browsePageSizeOptions,
  browseParamsToSearch,
  defaultBrowseParams,
  hasActiveBrowseFilters,
  parseBrowseParams,
  rawBrowseSearchSchema,
} from '../lib/browseParams'
import type { BrowseParams } from '../lib/browseParams'
import { iocTypeTokens } from '../lib/schemas'
import { browseKey } from '../lib/queryKeys'
import { focusRingClass } from '../components/AppShell'
import { IocGroupCard } from '../components/IocGroupCard'
import { Pagination } from '../components/Pagination'
import {
  EmptyState,
  ErrorState,
  ForbiddenState,
  LoadingState,
} from '../components/uiStates'

export const Route = createFileRoute('/browse')({
  validateSearch: rawBrowseSearchSchema,
  component: BrowsePage,
})

const controlClass =
  'mt-1 rounded-md border border-slate-400 bg-white px-3 py-2 text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700'

function providerDraftValue(params: BrowseParams | null): string {
  return params?.provider != null ? String(params.provider) : ''
}

function BrowsePage() {
  const rawSearch = Route.useSearch()
  const navigate = useNavigate()
  const { params, issues } = parseBrowseParams(rawSearch)

  function goTo(next: Partial<BrowseParams>) {
    if (!params) return
    void navigate({
      to: '/browse',
      search: browseParamsToSearch({ ...params, ...next }),
    })
  }

  const [tagDraft, setTagDraft] = useState(params?.tag ?? '')
  const [providerDraft, setProviderDraft] = useState(() =>
    providerDraftValue(params),
  )
  // Keep the text drafts in sync when the URL changes (reload, Back, Forward).
  useEffect(() => {
    setTagDraft(params?.tag ?? '')
    setProviderDraft(providerDraftValue(params))
  }, [params?.tag, params?.provider])

  const resolvedParams = params ?? defaultBrowseParams
  const results = useQuery({
    queryKey: browseKey(resolvedParams),
    queryFn: ({ signal }) => browseIocs(resolvedParams, signal),
    enabled: params !== null,
  })

  return (
    <section aria-labelledby="browse-heading">
      <h1 id="browse-heading" className="text-2xl font-bold text-slate-900">
        Browse indicators
      </h1>
      <p className="mt-2 max-w-2xl text-sm text-slate-700">
        Explore the grouped dataset. The filters, ordering, and page live in the
        address bar, so reloading, going Back or Forward, or sharing the link
        restores this exact view.
      </p>

      {params === null ? (
        <div className="mt-6 space-y-4">
          <ErrorState title="Unsupported filters" message={issues.join(' ')} />
          <Link
            to="/browse"
            search={() => ({})}
            className={`inline-block font-medium text-sky-800 underline underline-offset-2 ${focusRingClass}`}
          >
            Reset to the default view
          </Link>
        </div>
      ) : (
        <>
          <form
            className="mt-6 grid max-w-3xl grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
            onSubmit={(event) => {
              event.preventDefault()
              goTo({
                tag: tagDraft.trim() === '' ? null : tagDraft.trim(),
                provider:
                  providerDraft.trim() === '' ||
                  !Number.isInteger(Number(providerDraft))
                    ? null
                    : Number(providerDraft),
                page: 1,
              })
            }}
          >
            <div>
              <label
                htmlFor="browse-type"
                className="block text-sm font-medium text-slate-800"
              >
                Indicator type
              </label>
              <select
                id="browse-type"
                value={params.type ?? ''}
                onChange={(event) =>
                  goTo({
                    type: (event.target.value || null) as BrowseParams['type'],
                    page: 1,
                  })
                }
                className={`${controlClass} w-full`}
              >
                <option value="">All types</option>
                {iocTypeTokens.map((token) => (
                  <option key={token} value={token}>
                    {token}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label
                htmlFor="browse-tag"
                className="block text-sm font-medium text-slate-800"
              >
                Tag
              </label>
              <input
                id="browse-tag"
                type="text"
                value={tagDraft}
                onChange={(event) => setTagDraft(event.target.value)}
                className={`${controlClass} w-full`}
              />
            </div>
            <div>
              <label
                htmlFor="browse-provider"
                className="block text-sm font-medium text-slate-800"
              >
                Provider id
              </label>
              <input
                id="browse-provider"
                type="number"
                min={1}
                value={providerDraft}
                onChange={(event) => setProviderDraft(event.target.value)}
                className={`${controlClass} w-full`}
              />
            </div>
            <div>
              <label
                htmlFor="browse-order"
                className="block text-sm font-medium text-slate-800"
              >
                Order by
              </label>
              <select
                id="browse-order"
                value={params.orderBy}
                onChange={(event) =>
                  goTo({
                    orderBy: event.target.value as BrowseParams['orderBy'],
                    page: 1,
                  })
                }
                className={`${controlClass} w-full`}
              >
                {browseOrderByOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label
                htmlFor="browse-dir"
                className="block text-sm font-medium text-slate-800"
              >
                Direction
              </label>
              <select
                id="browse-dir"
                value={params.dir}
                onChange={(event) =>
                  goTo({
                    dir: event.target.value as BrowseParams['dir'],
                    page: 1,
                  })
                }
                className={`${controlClass} w-full`}
              >
                {browseDirOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label
                htmlFor="browse-page-size"
                className="block text-sm font-medium text-slate-800"
              >
                Page size
              </label>
              <select
                id="browse-page-size"
                value={String(params.pageSize)}
                onChange={(event) =>
                  goTo({ pageSize: Number(event.target.value), page: 1 })
                }
                className={`${controlClass} w-full`}
              >
                {browsePageSizeOptions.map((option) => (
                  <option key={option} value={String(option)}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex items-end gap-3 sm:col-span-2 lg:col-span-3">
              <button
                type="submit"
                className={`rounded-md bg-sky-800 px-4 py-2 font-medium text-white hover:bg-sky-900 ${focusRingClass}`}
              >
                Apply filters
              </button>
              {hasActiveBrowseFilters(params) ? (
                <Link
                  to="/browse"
                  search={() => ({})}
                  className={`font-medium text-sky-800 underline underline-offset-2 ${focusRingClass}`}
                >
                  Clear filters
                </Link>
              ) : null}
            </div>
          </form>

          <div className="mt-8">
            {results.isPending ? (
              <LoadingState label="Loading indicators…" />
            ) : null}
            {results.isError ? (
              <BrowseErrorState
                error={results.error}
                onRetry={() => void results.refetch()}
              />
            ) : null}
            {results.data ? (
              results.data.items.length === 0 ? (
                <EmptyState
                  title="No indicators found"
                  message="No stored indicators match the current filters."
                  action={
                    hasActiveBrowseFilters(params) ? (
                      <Link
                        to="/browse"
                        search={() => ({})}
                        className={`font-medium text-sky-800 underline underline-offset-2 ${focusRingClass}`}
                      >
                        Clear filters
                      </Link>
                    ) : undefined
                  }
                />
              ) : (
                <div className="space-y-4">
                  {results.data.items.map((group) => (
                    <IocGroupCard key={group.id} group={group} linkToDetail />
                  ))}
                </div>
              )
            ) : null}
            {results.data && results.data.items.length > 0 ? (
              <div className="mt-6">
                <Pagination
                  page={results.data.page}
                  pageSize={results.data.pageSize}
                  totalCount={results.data.totalCount}
                  onPage={(page) => goTo({ page })}
                />
              </div>
            ) : null}
          </div>
        </>
      )}
    </section>
  )
}

export function BrowseErrorState({
  error,
  onRetry,
}: {
  error: unknown
  onRetry: () => void
}) {
  if (error instanceof ApiError && error.status === 403) {
    return (
      <ForbiddenState message="You do not have permission to browse indicators." />
    )
  }
  return (
    <ErrorState
      title="Could not load indicators"
      message={
        error instanceof Error
          ? error.message
          : 'The request could not be completed.'
      }
      correlationId={
        error instanceof ApiError ? error.correlationId : undefined
      }
      onRetry={onRetry}
    />
  )
}
