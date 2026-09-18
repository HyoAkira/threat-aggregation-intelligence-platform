import { useState } from 'react'
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { z } from 'zod'
import { ApiError, getIocDetail, getIocRelations } from '../lib/apiClient'
import type { IocRelation } from '../lib/apiClient'
import { iocDetailKey, iocRelationsKey } from '../lib/queryKeys'
import { formatLocalDateTime } from '../lib/format'
import {
  iocTypeBadgeClass,
  iocTypeLabels,
  threatLabel,
} from '../lib/iocPresentation'
import { focusRingClass } from '../components/AppShell'
import { IocAiReport } from '../components/IocAiReport'
import { IocGroupCard } from '../components/IocGroupCard'
import { IocSourceCard } from '../components/IocSourceCard'
import { IocFalsePositiveBadge } from '../components/IocFalsePositiveBadge'
import { Pagination } from '../components/Pagination'
import {
  EmptyState,
  ErrorState,
  ForbiddenState,
  LoadingState,
} from '../components/uiStates'

// The router's default search parser turns numeric params into numbers, so raw
// URL values are coerced back to strings before structural validation.
const detailSearchSchema = z.object({
  relation: z.coerce.string().optional(),
  relPage: z.coerce.string().optional(),
})

export const Route = createFileRoute('/iocs/$id')({
  validateSearch: detailSearchSchema,
  component: IocDetailPage,
})

const RELATION_PAGE_SIZE = 20
const sectionTitleClass =
  'font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant'

function parseRelation(raw: string | undefined): IocRelation | null {
  if (raw === 'urlHost' || raw === 'parentDomain') return raw
  return null
}

function parseRelationPage(raw: string | undefined): number {
  const parsed = Number(raw)
  return Number.isInteger(parsed) && parsed >= 1 ? parsed : 1
}

function IocDetailPage() {
  const { id: idParam } = Route.useParams()
  const search = Route.useSearch()
  const id = Number(idParam)
  const idValid = Number.isInteger(id) && id >= 1

  const detail = useQuery({
    queryKey: iocDetailKey(id),
    queryFn: ({ signal }) => getIocDetail(id, signal),
    enabled: idValid,
  })

  if (!idValid) {
    return (
      <section aria-labelledby="detail-heading">
        <h1
          id="detail-heading"
          className="text-2xl font-semibold text-on-surface"
        >
          Indicator detail
        </h1>
        <ErrorState
          title="Invalid indicator id"
          message={`“${idParam}” is not a valid indicator id.`}
        />
      </section>
    )
  }

  return (
    <section aria-labelledby="detail-heading">
      <h1 id="detail-heading" className="sr-only">
        Indicator detail
      </h1>
      {detail.isPending ? <LoadingState label="Loading indicator…" /> : null}
      {detail.isError ? (
        <DetailErrorState
          error={detail.error}
          onRetry={() => void detail.refetch()}
        />
      ) : null}
      {detail.data ? (
        <ForensicDetail
          relation={parseRelation(search.relation)}
          relPage={parseRelationPage(search.relPage)}
          group={detail.data}
        />
      ) : null}
    </section>
  )
}

function ForensicDetail({
  group,
  relation,
  relPage,
}: {
  group: Awaited<ReturnType<typeof getIocDetail>>
  relation: IocRelation | null
  relPage: number
}) {
  const [copyLabel, setCopyLabel] = useState('Copy')
  const [isAiWorkspaceOpen, setIsAiWorkspaceOpen] = useState(false)
  // The left pane is only half the viewport while analysis is open, so it
  // needs its own compact breakpoint instead of inheriting the full-page one.
  const compactDetail = isAiWorkspaceOpen
  const label = threatLabel(group)
  const ports = Array.from(
    new Set(
      group.sources
        .map((source) => source.port)
        .filter((port): port is number => port !== null && port !== undefined),
    ),
  )

  function copyIndicator() {
    const clipboard = navigator.clipboard as Clipboard | undefined
    if (!clipboard?.writeText) return
    void clipboard.writeText(group.iocValue).then(() => {
      setCopyLabel('Copied')
      window.setTimeout(() => setCopyLabel('Copy'), 1800)
    })
  }

  const detailContent = (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link
          to="/browse"
          className={`inline-flex items-center gap-1.5 font-mono text-xs font-semibold uppercase tracking-wider text-primary transition-colors hover:text-on-surface ${focusRingClass}`}
        >
          <span
            aria-hidden="true"
            className="material-symbols-outlined text-[18px]"
          >
            arrow_back
          </span>
          Back to Explorer
        </Link>
      </div>

      <header className="relative overflow-hidden rounded border border-outline-variant bg-surface-container-lowest">
        <div aria-hidden="true" className="h-1 bg-primary" />
        <div className="space-y-5 p-5">
          <div
            className={`flex flex-col justify-between gap-4 ${
              compactDetail
                ? '2xl:flex-row 2xl:items-start'
                : 'xl:flex-row xl:items-start'
            }`}
          >
            <div>
              <div className="flex flex-wrap items-center gap-3">
                <h2
                  className={`break-all font-mono font-bold tracking-tight text-on-surface ${
                    compactDetail
                      ? 'text-xl md:text-2xl 2xl:text-3xl'
                      : 'text-2xl md:text-3xl'
                  }`}
                >
                  {group.iocValue}
                </h2>
                <button
                  type="button"
                  onClick={copyIndicator}
                  className={`inline-flex items-center gap-1.5 rounded bg-surface-container-low px-2.5 py-1.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface transition-colors hover:bg-surface-container ${focusRingClass}`}
                >
                  <span
                    aria-hidden="true"
                    className="material-symbols-outlined text-[15px] text-primary"
                  >
                    {copyLabel === 'Copied' ? 'check' : 'content_copy'}
                  </span>
                  {copyLabel}
                </button>
              </div>
              <div className="mt-3 flex flex-wrap items-center gap-1.5">
                <span
                  className={`rounded px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider ${iocTypeBadgeClass[group.iocType]}`}
                >
                  {iocTypeLabels[group.iocType]} indicator
                </span>
                {label ? (
                  <span className="rounded bg-error-container px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-on-error-container">
                    Threat: {label}
                  </span>
                ) : null}
                <span className="rounded bg-surface-container px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
                  {group.sources.length} provider source
                  {group.sources.length === 1 ? '' : 's'}
                </span>
                <IocFalsePositiveBadge sources={group.sources} />
              </div>
            </div>

            <div className="grid min-w-56 grid-cols-2 gap-px overflow-hidden rounded border border-outline-variant bg-outline-variant">
              <div className="bg-surface-container-low p-3">
                <span className={sectionTitleClass}>First seen</span>
                <p className="mt-1 font-mono text-xs font-semibold text-on-surface">
                  {formatLocalDateTime(group.firstSeenUtc) ?? 'Unknown'}
                </p>
              </div>
              <div className="bg-surface-container-low p-3">
                <span className={sectionTitleClass}>Last seen</span>
                <p className="mt-1 font-mono text-xs font-semibold text-on-surface">
                  {formatLocalDateTime(group.lastSeenUtc ?? undefined) ??
                    'Unknown'}
                </p>
              </div>
            </div>
          </div>

          <dl className="flex flex-wrap items-center gap-x-5 gap-y-2 border-t border-outline-variant/60 pt-4 font-mono text-[11px] text-on-surface-variant">
            <div className="flex items-center gap-1.5">
              <span
                aria-hidden="true"
                className="material-symbols-outlined text-[15px] text-outline"
              >
                history
              </span>
              <dt>First seen:</dt>
              <dd className="font-semibold text-on-surface">
                {formatLocalDateTime(group.firstSeenUtc) ?? 'Unknown'}
              </dd>
            </div>
            <div className="flex items-center gap-1.5">
              <span
                aria-hidden="true"
                className="material-symbols-outlined text-[15px] text-tertiary"
              >
                schedule
              </span>
              <dt>Last seen:</dt>
              <dd className="font-semibold text-on-surface">
                {formatLocalDateTime(group.lastSeenUtc ?? undefined) ??
                  'Unknown'}
              </dd>
            </div>
            <div className="flex items-center gap-1.5">
              <span
                aria-hidden="true"
                className="material-symbols-outlined text-[15px] text-primary"
              >
                hub
              </span>
              <dt>Sources:</dt>
              <dd className="font-semibold text-on-surface">
                {group.sources.length}
              </dd>
            </div>
            <div className="ml-auto">
              <button
                type="button"
                onClick={() => setIsAiWorkspaceOpen(true)}
                className={`inline-flex items-center gap-1.5 rounded bg-primary px-3 py-1.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-on-primary transition-colors hover:bg-primary/90 ${focusRingClass}`}
              >
                <span
                  aria-hidden="true"
                  className="material-symbols-outlined text-[15px]"
                >
                  auto_awesome
                </span>
                Analyze with AI
              </button>
            </div>
          </dl>
        </div>
      </header>

      <div
        className={`grid grid-cols-1 ${
          compactDetail ? 'gap-5' : 'gap-6 xl:grid-cols-12'
        }`}
      >
        <div className={`space-y-6 ${compactDetail ? '' : 'xl:col-span-7'}`}>
          <section className="rounded border border-outline-variant bg-surface-container-lowest">
            <div className="flex items-start justify-between gap-3 border-b border-outline-variant/60 px-4 py-3">
              <div>
                <h2 className="text-base font-semibold tracking-tight text-on-surface">
                  Provider evidence
                </h2>
                <p className="mt-0.5 text-xs text-on-surface-variant">
                  The raw indicator records submitted by external threat
                  intelligence sources, showing which providers have reported
                  the same IOC and supporting the claim with attributable source
                  data.
                </p>
              </div>
              <span className="whitespace-nowrap rounded bg-surface-container px-2 py-0.5 font-mono text-[10px] uppercase tracking-wider text-on-surface-variant">
                {group.sources.length} records
              </span>
            </div>
            <div className="space-y-3 bg-surface-container-low/40 p-4">
              {group.sources.map((source, index) => (
                <IocSourceCard
                  key={`${source.provider}-${source.providerRecordId}-${index}`}
                  source={source}
                />
              ))}
            </div>
          </section>

          <RelationsSection
            id={group.id}
            urlHost={group.urlHost ?? null}
            parentDomain={group.parentDomain ?? null}
            relation={relation}
            relPage={relPage}
          />
        </div>

        <aside className={`space-y-6 ${compactDetail ? '' : 'xl:col-span-5'}`}>
          <section className="rounded border border-outline-variant bg-surface-container-lowest">
            <div className="border-b border-outline-variant/60 px-4 py-3">
              <h2 className="text-base font-semibold tracking-tight text-on-surface">
                IOC context
              </h2>
            </div>
            <dl className="space-y-2 p-4 text-xs">
              <ContextRow
                label="Indicator type"
                value={`${iocTypeLabels[group.iocType]} (${group.iocType})`}
              />
              <ContextRow label="URL host" value={group.urlHost ?? null} />
              <ContextRow
                label="Parent domain"
                value={group.parentDomain ?? null}
              />
              <ContextRow
                label="Observed ports"
                value={ports.length > 0 ? ports.join(', ') : null}
              />
              <ContextRow label="IOC identifier" value={String(group.id)} />
            </dl>
          </section>
          <section className="rounded border border-outline-variant bg-surface-container-lowest p-4">
            <span className={sectionTitleClass}>Investigation notes</span>
            <p className="mt-2 text-sm leading-6 text-on-surface-variant">
              This application aggregates known public indicators. It does not
              score, block, or otherwise act on the value. Review the
              provider-attributed evidence before making an operational
              decision.
            </p>
          </section>
        </aside>
      </div>
    </div>
  )

  if (isAiWorkspaceOpen) {
    return (
      <div className="grid min-h-[calc(100vh-10rem)] grid-cols-1 gap-6 xl:grid-cols-2">
        <div className="min-w-0">{detailContent}</div>
        <aside className="min-w-0 xl:sticky xl:top-20 xl:h-[calc(100vh-5rem)] xl:self-start">
          <IocAiReport
            iocId={group.id}
            autoGenerate
            onClose={() => setIsAiWorkspaceOpen(false)}
          />
        </aside>
      </div>
    )
  }

  return detailContent
}

function ContextRow({ label, value }: { label: string; value: string | null }) {
  if (value === null) return null
  return (
    <div className="flex items-baseline justify-between gap-4 rounded bg-surface-container-low px-3 py-2">
      <dt className={sectionTitleClass}>{label}</dt>
      <dd className="break-all text-right font-mono text-[11px] font-semibold text-on-surface">
        {value}
      </dd>
    </div>
  )
}

export function DetailErrorState({
  error,
  onRetry,
}: {
  error: unknown
  onRetry: () => void
}) {
  if (error instanceof ApiError && error.status === 404) {
    return (
      <EmptyState
        title="Indicator not found"
        message="No indicator exists for this id. It may have been removed or the link may be stale."
      />
    )
  }
  if (error instanceof ApiError && error.status === 403) {
    return (
      <ForbiddenState message="You do not have permission to view this indicator." />
    )
  }
  return (
    <ErrorState
      title="Could not load indicator"
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

function RelationsSection({
  id,
  urlHost,
  parentDomain,
  relation,
  relPage,
}: {
  id: number
  urlHost: string | null
  parentDomain: string | null
  relation: IocRelation | null
  relPage: number
}) {
  const navigate = useNavigate()

  const available: Array<{ kind: IocRelation; label: string }> = []
  if (urlHost) {
    available.push({ kind: 'urlHost', label: `URLs on host ${urlHost}` })
  }
  if (parentDomain) {
    available.push({
      kind: 'parentDomain',
      label: `Indicators under ${parentDomain}`,
    })
  }

  // IP indicators store no urlHost or parentDomain, so no relation controls render.
  if (available.length === 0) return null

  function goToRelation(next: IocRelation | null, page = 1) {
    void navigate({
      to: '/iocs/$id',
      params: { id: String(id) },
      search: {
        relation: next ?? undefined,
        relPage: page > 1 ? String(page) : undefined,
      },
    })
  }

  const relations = useQuery({
    queryKey: iocRelationsKey(id, relation ?? 'none', relPage),
    queryFn: ({ signal }) =>
      getIocRelations(
        {
          id,
          relation: relation!,
          page: relPage,
          pageSize: RELATION_PAGE_SIZE,
        },
        signal,
      ),
    enabled: relation !== null,
  })

  return (
    <section
      className="rounded border border-outline-variant bg-surface-container-lowest"
      aria-labelledby="relations-heading"
    >
      <div className="border-b border-outline-variant/60 px-4 py-3">
        <h2
          id="relations-heading"
          className="text-base font-semibold text-on-surface"
        >
          Related indicators
        </h2>
        <p className="mt-0.5 text-xs text-on-surface-variant">
          Relations load on demand, one bounded API page at a time.
        </p>
        <div className="mt-3 flex flex-wrap gap-2">
          {available.map((option) => (
            <button
              key={option.kind}
              type="button"
              aria-pressed={relation === option.kind}
              onClick={() =>
                goToRelation(relation === option.kind ? null : option.kind)
              }
              className={`rounded px-3 py-1.5 font-mono text-[10px] font-semibold uppercase tracking-wider transition-colors ${focusRingClass} ${
                relation === option.kind
                  ? 'bg-primary text-on-primary'
                  : 'bg-surface-container-low text-on-surface-variant hover:bg-surface-container'
              }`}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      {relation !== null ? (
        <div className="space-y-4 p-4">
          {relations.isPending ? (
            <LoadingState label="Loading related indicators…" />
          ) : null}
          {relations.isError ? (
            <RelationsErrorState
              error={relations.error}
              onRetry={() => void relations.refetch()}
            />
          ) : null}
          {relations.data ? (
            relations.data.items.length === 0 ? (
              <EmptyState
                title="No related indicators"
                message="No other stored indicators share this host or parent domain."
              />
            ) : (
              <>
                <div className="space-y-4">
                  {relations.data.items.map((item) => (
                    <IocGroupCard
                      key={item.id}
                      group={item}
                      headingLevel="h3"
                      linkToDetail
                    />
                  ))}
                </div>
                <Pagination
                  page={relations.data.page}
                  pageSize={relations.data.pageSize}
                  totalCount={relations.data.totalCount}
                  onPage={(page) => goToRelation(relation, page)}
                  pageLabel="Relations page"
                />
              </>
            )
          ) : null}
        </div>
      ) : null}
    </section>
  )
}

export function RelationsErrorState({
  error,
  onRetry,
}: {
  error: unknown
  onRetry: () => void
}) {
  if (error instanceof ApiError && error.status === 403) {
    return (
      <ForbiddenState message="You do not have permission to view related indicators." />
    )
  }
  return (
    <ErrorState
      title="Could not load related indicators"
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
