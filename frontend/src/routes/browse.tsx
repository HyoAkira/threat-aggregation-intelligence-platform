import { Suspense, lazy, useCallback, useEffect, useRef, useState } from 'react'
import {
  Link,
  ClientOnly,
  createFileRoute,
  useNavigate,
} from '@tanstack/react-router'
import { ApiError } from '../lib/apiClient'
import {
  browseDirOptions,
  browseOrderByOptions,
  browsePageSizeOptions,
  browseParamsToSearch,
  hasActiveBrowseFilters,
  observedPeriodFrom,
  observedPeriodPresets,
  parseBrowseParams,
  rawBrowseSearchSchema,
} from '../lib/browseParams'
import type { BrowseParams } from '../lib/browseParams'
import { iocTypeTokens } from '../lib/schemas'
import { iocTypeLabels } from '../lib/iocPresentation'
import { focusRingClass } from '../components/AppShell'
import { TagAutocompleteInput } from '../components/TagAutocompleteInput'
import {
  EmptyState,
  ErrorState,
  ForbiddenState,
  LoadingState,
} from '../components/uiStates'
import type {
  BrowseTableHandle,
  BrowseTableStatus,
} from '../components/BrowseDataTable'

export const Route = createFileRoute('/browse')({
  validateSearch: rawBrowseSearchSchema,
  component: BrowsePage,
})

const controlClass =
  'mt-1 w-full rounded border border-outline-variant bg-surface-container-lowest px-3 py-2 font-mono text-xs text-on-surface focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary'
const fieldLabelClass =
  'font-mono text-[10px] font-semibold uppercase tracking-wider text-outline'

// Browser-only: the DataTables module is loaded dynamically beneath ClientOnly.
const LazyBrowseTable = lazy(() => import('../components/BrowseDataTable'))

function providerDraftValue(params: BrowseParams | null): string {
  return params?.provider ?? ''
}

/** Local datetime string for a `datetime-local` input from an ISO instant. */
function toDatetimeLocalValue(iso: string | null): string {
  if (!iso) return ''
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return ''
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}

/** ISO instant from a `datetime-local` input value, or null when empty/invalid. */
function fromDatetimeLocalValue(value: string): string | null {
  if (value.trim() === '') return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date.toISOString()
}

function BrowseTableFallback() {
  return (
    <div
      role="status"
      aria-live="polite"
      className="px-4 py-6 font-mono text-xs uppercase tracking-wider text-on-surface-variant"
    >
      Preparing the indicator table…
    </div>
  )
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
  const [fromDraft, setFromDraft] = useState(() =>
    toDatetimeLocalValue(params?.lastSeenFrom ?? null),
  )
  const [toDraft, setToDraft] = useState(() =>
    toDatetimeLocalValue(params?.lastSeenTo ?? null),
  )
  // Keep the text drafts in sync when the URL changes (reload, Back, Forward).
  useEffect(() => {
    setTagDraft(params?.tag ?? '')
    setProviderDraft(providerDraftValue(params))
    setFromDraft(toDatetimeLocalValue(params?.lastSeenFrom ?? null))
    setToDraft(toDatetimeLocalValue(params?.lastSeenTo ?? null))
  }, [params?.tag, params?.provider, params?.lastSeenFrom, params?.lastSeenTo])

  const [tableStatus, setTableStatus] = useState<BrowseTableStatus>({
    kind: 'pending',
  })
  const tableHandleRef = useRef<BrowseTableHandle | null>(null)

  const handleTableParams = useCallback(
    (next: BrowseParams) => {
      void navigate({ to: '/browse', search: browseParamsToSearch(next) })
    },
    [navigate],
  )
  const openDetail = useCallback(
    (id: number) => {
      void navigate({ to: '/iocs/$id', params: { id: String(id) } })
    },
    [navigate],
  )

  const [toast, setToast] = useState<string | null>(null)
  const toastTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  useEffect(
    () => () => {
      if (toastTimer.current) clearTimeout(toastTimer.current)
    },
    [],
  )
  function showToast(message: string) {
    setToast(message)
    if (toastTimer.current) clearTimeout(toastTimer.current)
    toastTimer.current = setTimeout(() => setToast(null), 2500)
  }
  function copyIndicator(value: string) {
    const clipboard = navigator.clipboard as Clipboard | undefined
    if (!clipboard?.writeText) {
      showToast('Copy is not available in this browser.')
      return
    }
    void clipboard.writeText(value).then(
      () => showToast(`Copied to clipboard: ${value}`),
      () => showToast('Copy failed.'),
    )
  }

  const showTable =
    tableStatus.kind === 'pending' || tableStatus.kind === 'success'

  return (
    <section aria-labelledby="browse-heading">
      <div className="flex flex-wrap items-end justify-between gap-3 border-b border-outline-variant/60 pb-4">
        <div>
          <p className={fieldLabelClass}>Threat Intelligence Explorer</p>
          <div className="mt-1 flex flex-wrap items-baseline gap-3">
            <h1
              id="browse-heading"
              className="text-2xl font-semibold tracking-tight text-on-surface"
            >
              Browse indicators
            </h1>
            {tableStatus.kind === 'success' ? (
              <span className="rounded-full bg-surface-container-high px-2.5 py-0.5 font-mono text-[11px] font-semibold text-on-surface-variant">
                {tableStatus.totalCount.toLocaleString('en-US')} IOCs
              </span>
            ) : null}
          </div>
          <p className="mt-1 max-w-2xl text-xs text-on-surface-variant">
            Filter, order, and page the grouped dataset. Every control is
            reflected in the address bar, so reloading, Back/Forward, or sharing
            the link restores this exact view.
          </p>
        </div>
      </div>

      {params === null ? (
        <div className="mt-6 space-y-4">
          <ErrorState title="Unsupported filters" message={issues.join(' ')} />
          <Link
            to="/browse"
            search={() => ({})}
            className={`inline-block font-medium text-primary underline underline-offset-2 ${focusRingClass}`}
          >
            Reset to the default view
          </Link>
        </div>
      ) : (
        <div className="mt-6 flex flex-col items-start gap-6 lg:flex-row">
          <aside className="w-full shrink-0 rounded border border-outline-variant bg-surface-container-lowest lg:w-72">
            <div className="flex items-center justify-between border-b border-outline-variant/60 px-3 py-2">
              <span className="flex items-center gap-1.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface">
                <span
                  aria-hidden="true"
                  className="material-symbols-outlined text-[16px] text-primary"
                >
                  tune
                </span>
                Filter Telemetry
              </span>
              {hasActiveBrowseFilters(params) ? (
                <Link
                  to="/browse"
                  search={() => ({})}
                  className={`font-mono text-[10px] font-semibold uppercase tracking-wider text-primary hover:underline ${focusRingClass}`}
                >
                  Clear filters
                </Link>
              ) : (
                <span className="font-mono text-[10px] uppercase tracking-wider text-outline">
                  Default view
                </span>
              )}
            </div>

            <form
              className="space-y-4 p-3"
              onSubmit={(event) => {
                event.preventDefault()
                goTo({
                  tag: tagDraft.trim() === '' ? null : tagDraft.trim(),
                  provider:
                    providerDraft.trim() === '' ? null : providerDraft.trim(),
                  lastSeenFrom: fromDatetimeLocalValue(fromDraft),
                  lastSeenTo: fromDatetimeLocalValue(toDraft),
                  page: 1,
                })
              }}
            >
              <fieldset>
                <legend className={fieldLabelClass}>Indicator type</legend>
                <div
                  role="group"
                  aria-label="Filter by indicator type"
                  className="mt-2 flex flex-wrap gap-1.5"
                >
                  <button
                    type="button"
                    aria-pressed={params.type === null}
                    onClick={() => goTo({ type: null, page: 1 })}
                    className={`rounded-full px-2.5 py-1 font-mono text-[10px] font-semibold uppercase tracking-wider transition-colors ${focusRingClass} ${
                      params.type === null
                        ? 'bg-primary text-on-primary'
                        : 'bg-surface-container-low text-on-surface-variant hover:bg-surface-container'
                    }`}
                  >
                    All Types
                  </button>
                  {iocTypeTokens.map((token) => (
                    <button
                      key={token}
                      type="button"
                      aria-pressed={params.type === token}
                      onClick={() => goTo({ type: token, page: 1 })}
                      className={`rounded-full px-2.5 py-1 font-mono text-[10px] font-semibold uppercase tracking-wider transition-colors ${focusRingClass} ${
                        params.type === token
                          ? 'bg-primary text-on-primary'
                          : 'bg-surface-container-low text-on-surface-variant hover:bg-surface-container'
                      }`}
                    >
                      {iocTypeLabels[token]}
                    </button>
                  ))}
                </div>
              </fieldset>

              <div>
                <label htmlFor="browse-tag" className={fieldLabelClass}>
                  Tag
                </label>
                <TagAutocompleteInput
                  id="browse-tag"
                  value={tagDraft}
                  onChange={setTagDraft}
                  className={controlClass}
                />
              </div>

              <div>
                <label htmlFor="browse-provider" className={fieldLabelClass}>
                  Provider
                </label>
                <input
                  id="browse-provider"
                  type="text"
                  value={providerDraft}
                  onChange={(event) => setProviderDraft(event.target.value)}
                  className={controlClass}
                />
              </div>

              <fieldset>
                <legend className={fieldLabelClass}>Observed period</legend>
                <div className="mt-2 grid grid-cols-2 gap-1">
                  {observedPeriodPresets.map((preset) => (
                    <button
                      key={preset.value}
                      type="button"
                      onClick={() =>
                        goTo({
                          lastSeenFrom:
                            preset.value === 'all'
                              ? null
                              : observedPeriodFrom(preset.value),
                          lastSeenTo: null,
                          page: 1,
                        })
                      }
                      className={`rounded px-2 py-1.5 text-center font-mono text-[10px] font-semibold uppercase tracking-wider transition-colors ${focusRingClass} ${
                        preset.value === 'all' &&
                        params.lastSeenFrom === null &&
                        params.lastSeenTo === null
                          ? 'bg-primary text-on-primary'
                          : 'bg-surface-container-low text-on-surface-variant hover:bg-surface-container'
                      }`}
                    >
                      {preset.label}
                    </button>
                  ))}
                </div>
                <div className="mt-2 grid grid-cols-2 gap-1">
                  <div>
                    <label
                      htmlFor="browse-lastseen-from"
                      className="font-mono text-[9px] uppercase tracking-wider text-outline"
                    >
                      From
                    </label>
                    <input
                      id="browse-lastseen-from"
                      type="datetime-local"
                      value={fromDraft}
                      onChange={(event) => setFromDraft(event.target.value)}
                      className="mt-1 w-full rounded border border-outline-variant bg-surface-container-lowest px-2 py-1 font-mono text-[11px] text-on-surface"
                    />
                  </div>
                  <div>
                    <label
                      htmlFor="browse-lastseen-to"
                      className="font-mono text-[9px] uppercase tracking-wider text-outline"
                    >
                      To
                    </label>
                    <input
                      id="browse-lastseen-to"
                      type="datetime-local"
                      value={toDraft}
                      onChange={(event) => setToDraft(event.target.value)}
                      className="mt-1 w-full rounded border border-outline-variant bg-surface-container-lowest px-2 py-1 font-mono text-[11px] text-on-surface"
                    />
                  </div>
                </div>
              </fieldset>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label htmlFor="browse-order" className={fieldLabelClass}>
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
                    className={controlClass}
                  >
                    {browseOrderByOptions.map((option) => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label htmlFor="browse-dir" className={fieldLabelClass}>
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
                    className={controlClass}
                  >
                    {browseDirOptions.map((option) => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div>
                <label htmlFor="browse-page-size" className={fieldLabelClass}>
                  Page size
                </label>
                <select
                  id="browse-page-size"
                  value={String(params.pageSize)}
                  onChange={(event) =>
                    goTo({ pageSize: Number(event.target.value), page: 1 })
                  }
                  className={controlClass}
                >
                  {browsePageSizeOptions.map((option) => (
                    <option key={option} value={String(option)}>
                      {option}
                    </option>
                  ))}
                </select>
              </div>

              <button
                type="submit"
                className={`w-full rounded bg-primary px-4 py-2 font-mono text-xs font-semibold uppercase tracking-wider text-on-primary transition-colors hover:bg-primary-container ${focusRingClass}`}
              >
                Apply filters
              </button>
            </form>
          </aside>

          <div className="w-full min-w-0 flex-1">
            {tableStatus.kind === 'pending' ? (
              <LoadingState label="Loading indicators…" />
            ) : null}
            {tableStatus.kind === 'error' ? (
              <BrowseErrorState
                error={tableStatus.error}
                onRetry={() => tableHandleRef.current?.reload()}
              />
            ) : null}
            {tableStatus.kind === 'forbidden' ? (
              <ForbiddenState message="You do not have permission to browse indicators." />
            ) : null}
            {tableStatus.kind === 'empty' ? (
              <EmptyState
                title="No indicators found"
                message="No stored indicators match the current filters."
                action={
                  hasActiveBrowseFilters(params) ? (
                    <Link
                      to="/browse"
                      search={() => ({})}
                      className={`font-medium text-primary underline underline-offset-2 ${focusRingClass}`}
                    >
                      Clear filters
                    </Link>
                  ) : undefined
                }
              />
            ) : null}

            <div
              className={`overflow-hidden rounded border border-outline-variant bg-surface-container-lowest ${
                showTable ? '' : 'hidden'
              }`}
            >
              <div className="w-full overflow-x-auto">
                <ClientOnly fallback={<BrowseTableFallback />}>
                  <Suspense fallback={<BrowseTableFallback />}>
                    <LazyBrowseTable
                      ref={tableHandleRef}
                      params={params}
                      onTableParams={handleTableParams}
                      onStatus={setTableStatus}
                      onCopy={copyIndicator}
                      onOpenDetail={openDetail}
                    />
                  </Suspense>
                </ClientOnly>
              </div>
              {tableStatus.kind === 'success' ? (
                <div className="flex flex-wrap items-center justify-between gap-2 border-t border-outline-variant/60 bg-surface-container-low/40 px-3 py-2 font-mono text-[10px] uppercase tracking-wider text-on-surface-variant">
                  <span className="flex items-center gap-2">
                    <span
                      aria-hidden="true"
                      className="h-2 w-2 rounded-full bg-tertiary"
                    />
                    Showing {tableStatus.shown} of{' '}
                    {tableStatus.totalCount.toLocaleString('en-US')} matched
                    indicators
                  </span>
                  <span>Source-attributed · page {tableStatus.page}</span>
                </div>
              ) : null}
            </div>
          </div>
        </div>
      )}

      {toast ? (
        <div
          role="status"
          aria-live="polite"
          className="fixed bottom-6 right-6 z-50 flex items-center gap-2 rounded bg-inverse-surface px-4 py-2 font-mono text-xs text-inverse-on-surface"
        >
          <span
            aria-hidden="true"
            className="material-symbols-outlined text-[16px] text-tertiary-fixed"
          >
            check_circle
          </span>
          <span>{toast}</span>
        </div>
      ) : null}
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
