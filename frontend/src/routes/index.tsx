import { useEffect, useRef, useState } from 'react'
import { Link, createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { browseIocs, searchIocs } from '../lib/apiClient'
import type { SearchSubmission } from '../lib/apiClient'
import { defaultBrowseParams } from '../lib/browseParams'
import type { BrowseParams } from '../lib/browseParams'
import { browseKey, iocSearchKey } from '../lib/queryKeys'
import { iocTypeTokens } from '../lib/schemas'
import type { IocGroup, IocTypeToken } from '../lib/schemas'
import {
  detectIocType,
  formatShortUtc,
  iocTypeLabels,
  threatLabel,
} from '../lib/iocPresentation'
import { IocGroupCard } from '../components/IocGroupCard'
import { EmptyState, ErrorState, LoadingState } from '../components/uiStates'
import { BrowseErrorState } from './browse'
import { SearchErrorState, validateSearchSubmission } from './search'

export const Route = createFileRoute('/')({
  head: () => ({
    meta: [
      {
        title: 'Threat Intelligence — Search Hub',
      },
    ],
    links: [
      {
        rel: 'preconnect',
        href: 'https://fonts.gstatic.com',
        crossOrigin: '',
      },
      {
        rel: 'stylesheet',
        href: 'https://fonts.googleapis.com/css2?family=Geist:wght@100..900&family=JetBrains+Mono:wght@100..800&display=swap',
      },
      {
        rel: 'stylesheet',
        href: 'https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200&display=block',
      },
    ],
  }),
  component: HomePage,
})

const focusRing =
  'rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary'

const RECENT_PAGE_SIZE = 10

const QUICK_QUERIES = [
  '185.220.101.5',
  'cobaltstrike-c2.top',
  'apt29-beacon.org',
] as const

// Detection and labels are shared with the explorer so the hint badge and the
// table agree; re-exported here for the existing home-page contract.
export { detectIocType, iocTypeLabels }

function csvEscape(value: string): string {
  return `"${value.replaceAll('"', '""')}"`
}

function HomePage() {
  const [q, setQ] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const [submission, setSubmission] = useState<SearchSubmission | null>(null)
  const [tableType, setTableType] = useState<IocTypeToken | null>(null)

  const searchQuery = useQuery({
    queryKey: iocSearchKey(submission ?? { q: '', tag: null, provider: null }),
    queryFn: ({ signal }) => searchIocs(submission!, signal),
    enabled: submission !== null,
  })

  const tableParams: BrowseParams = {
    ...defaultBrowseParams,
    type: tableType,
    pageSize: RECENT_PAGE_SIZE,
  }
  const tableQuery = useQuery({
    queryKey: browseKey(tableParams),
    queryFn: ({ signal }) => browseIocs(tableParams, signal),
  })

  const totalsParams: BrowseParams = { ...defaultBrowseParams, pageSize: 1 }
  const totalsQuery = useQuery({
    queryKey: browseKey(totalsParams),
    queryFn: ({ signal }) => browseIocs(totalsParams, signal),
  })
  const allTotal = totalsQuery.data?.totalCount ?? null

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

  function exportRecentCsv(rows: readonly IocGroup[]) {
    const header = [
      'indicator',
      'type',
      'threat_label',
      'reported_by',
      'first_seen_utc',
    ]
    const lines = [
      header,
      ...rows.map((group) => [
        group.iocValue,
        group.iocType,
        threatLabel(group) ?? '',
        group.sources.length > 0 ? group.sources[0].provider : '',
        group.firstSeenUtc,
      ]),
    ]
    const csv = lines.map((row) => row.map(csvEscape).join(',')).join('\r\n')
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = 'recent-indicators.csv'
    anchor.click()
    URL.revokeObjectURL(url)
    showToast(`Exported ${rows.length} indicators to CSV.`)
  }

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const validation = validateSearchSubmission({ q, tag: '', provider: '' })
    setFormError(validation.error)
    setSubmission(validation.submission)
  }

  const detectedType = detectIocType(q)
  const rows = tableQuery.data?.items ?? []
  const searchBusy = submission !== null && searchQuery.isPending

  return (
    <>
      <section
        aria-labelledby="hub-heading"
        className="relative -mx-4 -mt-6 overflow-hidden bg-surface pb-10 md:-mx-6"
      >
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 opacity-40"
        >
          <svg
            className="h-full w-full"
            height="100%"
            width="100%"
            xmlns="http://www.w3.org/2000/svg"
          >
            <defs>
              <pattern
                height="32"
                id="hub-grid"
                patternUnits="userSpaceOnUse"
                width="32"
              >
                <circle className="fill-outline-variant" cx="1" cy="1" r="1" />
              </pattern>
            </defs>
            <rect fill="url(#hub-grid)" height="100%" width="100%" />
          </svg>
        </div>

        <div className="relative mx-auto flex max-w-3xl flex-col items-center px-4 pt-12 text-center md:px-6">
          <div className="inline-flex items-center gap-2 rounded-full bg-surface-container-low px-3 py-1 shadow-sm">
            <span
              aria-hidden="true"
              className="h-2 w-2 animate-pulse rounded-full bg-tertiary"
            />
            <span className="text-xs uppercase tracking-wider text-on-surface-variant">
              Live IOC repository
            </span>
            <span className="rounded bg-surface-container-high px-1.5 py-0.5 text-xs font-semibold uppercase text-primary">
              Auto-detect
            </span>
          </div>

          <h1
            id="hub-heading"
            className="mt-6 text-balance text-4xl font-semibold tracking-tight text-on-surface md:text-5xl"
          >
            Inspect Any Indicator of Compromise.
          </h1>
          <p className="mt-3 max-w-2xl text-base text-on-surface-variant md:text-lg">
            Instant type auto-detection for IPv4, IPv6, domains, and HTTP(S)
            URLs, aggregated from every configured provider feed.
          </p>

          <form
            role="search"
            aria-label="Search indicators"
            noValidate
            onSubmit={handleSubmit}
            className="mt-8 w-full"
          >
            <label htmlFor="hub-search-q" className="sr-only">
              Indicator (IPv4, IPv6, domain, or HTTP(S) URL)
            </label>
            <div className="flex h-16 items-center rounded-full bg-surface-container-lowest px-4 shadow-md transition-all focus-within:shadow-xl md:px-6">
              <span
                aria-hidden="true"
                className="material-symbols-outlined mr-3 select-none text-[26px] text-primary"
              >
                search
              </span>
              <input
                id="hub-search-q"
                name="q"
                type="text"
                autoComplete="off"
                spellCheck={false}
                placeholder="Auto-detecting type (e.g. 194.26.29.112, evil-domain.ru, https://…)"
                value={q}
                onChange={(event) => setQ(event.target.value)}
                className="w-full bg-transparent font-mono text-sm text-on-surface outline-none placeholder:font-sans placeholder:text-outline md:text-base"
              />
              {detectedType ? (
                <span className="mr-2 hidden items-center gap-1 rounded bg-surface-container px-2 py-0.5 text-xs font-medium uppercase text-primary sm:inline-flex">
                  <span
                    aria-hidden="true"
                    className="material-symbols-outlined text-[12px]"
                  >
                    verified
                  </span>
                  {iocTypeLabels[detectedType]}
                </span>
              ) : null}
              {q !== '' ? (
                <button
                  type="button"
                  aria-label="Clear input"
                  onClick={() => {
                    setQ('')
                    setFormError(null)
                  }}
                  className={`mr-1 rounded-full p-1.5 text-outline transition-colors hover:bg-surface-container hover:text-on-surface ${focusRing}`}
                >
                  <span
                    aria-hidden="true"
                    className="material-symbols-outlined text-[18px]"
                  >
                    close
                  </span>
                </button>
              ) : null}
              <button
                type="submit"
                disabled={searchBusy}
                aria-busy={searchBusy}
                className={`ml-1 hidden items-center gap-1.5 rounded-full bg-primary-container px-4 py-2 text-sm font-medium uppercase tracking-wider text-on-primary shadow-sm transition-colors hover:bg-primary disabled:cursor-wait disabled:opacity-60 sm:flex ${focusRing}`}
              >
                <span>{searchBusy ? 'Scanning…' : 'Scan'}</span>
                <span
                  aria-hidden="true"
                  className="hidden font-mono text-xs opacity-70 md:inline"
                >
                  [↵]
                </span>
              </button>
            </div>
            <button
              type="submit"
              disabled={searchBusy}
              aria-busy={searchBusy}
              className={`mt-3 w-full rounded-full bg-primary-container px-4 py-2.5 text-sm font-semibold uppercase tracking-wider text-on-primary shadow-sm transition-colors hover:bg-primary disabled:cursor-wait disabled:opacity-60 sm:hidden ${focusRing}`}
            >
              {searchBusy ? 'Scanning…' : 'Scan'}
            </button>
          </form>

          <div className="mt-4 flex w-full flex-wrap items-center justify-between gap-2 px-1">
            <div
              role="group"
              aria-label="Filter recent indicators by type"
              className="flex flex-wrap items-center gap-1.5"
            >
              <TypeChip
                active={tableType === null}
                label="All Types"
                onClick={() => setTableType(null)}
              />
              {iocTypeTokens.map((token) => (
                <TypeChip
                  key={token}
                  active={tableType === token}
                  label={iocTypeLabels[token]}
                  onClick={() => setTableType(token)}
                />
              ))}
            </div>
          </div>

          <div className="mt-4 flex flex-wrap items-center justify-center gap-2 text-sm text-on-surface-variant">
            <span className="text-xs uppercase tracking-wider text-outline">
              Quick queries:
            </span>
            {QUICK_QUERIES.map((sample) => (
              <button
                key={sample}
                type="button"
                onClick={() => setQ(sample)}
                className={`font-mono text-xs underline decoration-dotted transition-colors hover:text-primary ${focusRing}`}
              >
                {sample}
              </button>
            ))}
          </div>
        </div>
      </section>

      {formError ? (
        <div className="mt-8">
          <ErrorState title="Check your input" message={formError} />
        </div>
      ) : null}
      {submission && searchQuery.isPending ? (
        <div className="mt-8">
          <LoadingState label="Scanning…" />
        </div>
      ) : null}
      {submission && searchQuery.isError ? (
        <div className="mt-8">
          <SearchErrorState
            error={searchQuery.error}
            q={submission.q}
            onRetry={() => void searchQuery.refetch()}
          />
        </div>
      ) : null}
      {searchQuery.data ? (
        <section aria-label="Search result" className="mt-8">
          <h2 className="flex flex-wrap items-baseline gap-x-2 text-lg font-semibold text-on-surface">
            Result for{' '}
            <span className="font-mono">{searchQuery.data.iocValue}</span>
          </h2>
          <div className="mt-3">
            <IocGroupCard group={searchQuery.data} linkToDetail />
          </div>
        </section>
      ) : null}

      <section aria-label="Indicator counts by type" className="mt-8">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {iocTypeTokens.map((token) => (
            <StatCard key={token} type={token} allTotal={allTotal} />
          ))}
        </div>
      </section>

      <section aria-labelledby="recent-heading" className="mt-8">
        <div className="flex flex-col overflow-hidden rounded-xl bg-surface-container-lowest shadow-sm">
          <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 md:px-6">
            <div className="flex flex-wrap items-center gap-3">
              <span
                aria-hidden="true"
                className="h-2.5 w-2.5 rounded-full bg-tertiary"
              />
              <h2
                id="recent-heading"
                className="text-lg font-medium text-on-surface"
              >
                Recently ingested indicators
              </h2>
              <span className="rounded bg-surface-container-low px-2 py-0.5 text-xs uppercase text-on-surface-variant">
                Source-attributed
              </span>
            </div>
            <button
              type="button"
              onClick={() => exportRecentCsv(rows)}
              disabled={rows.length === 0}
              className={`inline-flex items-center gap-1.5 rounded bg-surface-container-low px-3 py-1.5 text-xs uppercase text-on-surface-variant transition-colors hover:bg-surface-container disabled:opacity-50 ${focusRing}`}
            >
              <span
                aria-hidden="true"
                className="material-symbols-outlined text-[16px]"
              >
                file_download
              </span>
              <span>Export CSV</span>
            </button>
          </div>

          <div className="w-full overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="h-9 select-none bg-surface-container-low text-xs uppercase tracking-wider text-outline">
                  <th scope="col" className="px-4 py-2 font-semibold md:px-6">
                    Indicator
                  </th>
                  <th scope="col" className="px-3 py-2 font-semibold">
                    Type
                  </th>
                  <th scope="col" className="px-3 py-2 font-semibold">
                    Threat label / family
                  </th>
                  <th scope="col" className="px-3 py-2 font-semibold">
                    Reported by
                  </th>
                  <th
                    scope="col"
                    className="px-4 py-2 text-right font-semibold md:px-6"
                  >
                    Ingested
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-surface-container-low">
                {rows.map((group) => (
                  <RecentRow
                    key={group.id}
                    group={group}
                    onCopy={copyIndicator}
                  />
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex min-h-12 flex-wrap items-center justify-center px-4 py-3 text-sm text-on-surface-variant md:px-6">
            {tableQuery.isPending ? (
              <LoadingState label="Loading recent indicators…" />
            ) : null}
            {tableQuery.isError ? (
              <BrowseErrorState
                error={tableQuery.error}
                onRetry={() => void tableQuery.refetch()}
              />
            ) : null}
            {tableQuery.data && rows.length === 0 ? (
              <EmptyState
                title="No indicators found"
                message="No stored indicators match the selected type."
              />
            ) : null}
          </div>

          {tableQuery.data && rows.length > 0 ? (
            <div className="flex flex-wrap items-center justify-between gap-2 bg-surface-container-low/40 px-4 py-3 text-xs text-on-surface-variant md:px-6">
              <div className="flex items-center gap-2">
                <span
                  aria-hidden="true"
                  className="h-2 w-2 rounded-full bg-tertiary"
                />
                <span>
                  Showing {rows.length} of{' '}
                  {tableQuery.data.totalCount.toLocaleString('en-US')} stored
                  indicators
                </span>
              </div>
              <Link
                to="/browse"
                search={tableType ? { type: tableType } : {}}
                className={`font-semibold text-primary hover:underline ${focusRing}`}
              >
                Open full explorer →
              </Link>
            </div>
          ) : null}
        </div>
      </section>

      {toast ? (
        <div
          role="status"
          aria-live="polite"
          className="fixed bottom-6 right-6 z-50 flex items-center gap-2 rounded-md bg-inverse-surface px-4 py-2 text-sm text-inverse-on-surface shadow-xl"
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
    </>
  )
}

function TypeChip({
  active,
  label,
  onClick,
}: {
  active: boolean
  label: string
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={`rounded-full px-3 py-1 text-xs font-medium uppercase tracking-wide transition-colors ${
        active
          ? 'bg-primary text-on-primary'
          : 'bg-surface-container-low text-on-surface-variant hover:bg-surface-container'
      } ${focusRing}`}
    >
      {label}
    </button>
  )
}

const statIcon: Record<IocTypeToken, string> = {
  ipv4: 'lan',
  ipv6: 'hub',
  domain: 'public_off',
  url: 'link_off',
}

const statBarClass: Record<IocTypeToken, string> = {
  ipv4: 'bg-primary',
  ipv6: 'bg-surface-tint',
  domain: 'bg-error',
  url: 'bg-tertiary',
}

/** One type card: the real stored count and its share of all indicators. */
function StatCard({
  type,
  allTotal,
}: {
  type: IocTypeToken
  allTotal: number | null
}) {
  const params: BrowseParams = { ...defaultBrowseParams, type, pageSize: 1 }
  const query = useQuery({
    queryKey: browseKey(params),
    queryFn: ({ signal }) => browseIocs(params, signal),
  })
  const count = query.data?.totalCount ?? null
  const share =
    count != null && allTotal != null && allTotal > 0
      ? Math.min(100, Math.round((count / allTotal) * 100))
      : null

  return (
    <div className="flex flex-col justify-between rounded-xl bg-surface-container-lowest p-5 shadow-sm transition-shadow hover:shadow-md">
      <div className="flex items-center justify-between">
        <span className="text-xs uppercase tracking-wider text-outline">
          {iocTypeLabels[type]} indicators
        </span>
        <span
          aria-hidden="true"
          className="flex h-7 w-7 items-center justify-center rounded-full bg-surface-container-low text-primary"
        >
          <span className="material-symbols-outlined text-[16px]">
            {statIcon[type]}
          </span>
        </span>
      </div>
      <div className="my-2">
        <div className="text-2xl font-semibold tracking-tight text-on-surface">
          {count != null ? count.toLocaleString('en-US') : '—'}
        </div>
        {share != null ? (
          <div className="mt-1 text-xs text-on-surface-variant">
            {share}% of all stored indicators
          </div>
        ) : null}
      </div>
      <div className="h-1.5 w-full overflow-hidden rounded-full bg-surface-container-low">
        <div
          className={`h-full rounded-full ${statBarClass[type]}`}
          style={share != null ? { width: `${share}%` } : undefined}
        />
      </div>
    </div>
  )
}

function RecentRow({
  group,
  onCopy,
}: {
  group: IocGroup
  onCopy: (value: string) => void
}) {
  const label = threatLabel(group)
  const reporter = group.sources.length > 0 ? group.sources[0].provider : null
  const ingested = formatShortUtc(group.firstSeenUtc)

  return (
    <tr className="group transition-colors hover:bg-surface-container-low/50">
      <td className="px-4 py-2.5 md:px-6">
        <div className="flex items-center gap-2">
          <Link
            to="/iocs/$id"
            params={{ id: String(group.id) }}
            className={`max-w-xs truncate font-mono text-sm font-semibold text-on-surface underline-offset-2 hover:text-primary ${focusRing}`}
          >
            {group.iocValue}
          </Link>
          <button
            type="button"
            aria-label={`Copy ${group.iocValue}`}
            onClick={() => onCopy(group.iocValue)}
            className={`rounded p-1 text-outline opacity-0 transition-all hover:bg-surface-container hover:text-on-surface group-hover:opacity-100 focus-visible:opacity-100 ${focusRing}`}
          >
            <span
              aria-hidden="true"
              className="material-symbols-outlined text-[14px]"
            >
              content_copy
            </span>
          </button>
        </div>
      </td>
      <td className="px-3 py-2.5">
        <span className="rounded bg-surface-container-high px-2 py-0.5 text-xs font-medium uppercase text-primary">
          {iocTypeLabels[group.iocType]}
        </span>
      </td>
      <td className="px-3 py-2.5 text-sm font-medium text-on-surface">
        {label ?? '—'}
      </td>
      <td className="px-3 py-2.5 text-sm text-on-surface-variant">
        {reporter ?? '—'}
      </td>
      <td className="px-4 py-2.5 text-right text-xs text-outline md:px-6">
        {ingested ?? '—'}
      </td>
    </tr>
  )
}
