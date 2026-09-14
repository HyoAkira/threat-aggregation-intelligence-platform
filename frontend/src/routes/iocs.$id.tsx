import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { z } from 'zod'
import { ApiError, getIocDetail, getIocRelations } from '../lib/apiClient'
import type { IocRelation } from '../lib/apiClient'
import { iocDetailKey, iocRelationsKey } from '../lib/queryKeys'
import { focusRingClass } from '../components/AppShell'
import { IocGroupCard } from '../components/IocGroupCard'
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
        <h1 id="detail-heading" className="text-2xl font-bold text-slate-900">
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
      <h1 id="detail-heading" className="text-2xl font-bold text-slate-900">
        Indicator detail
      </h1>
      <div className="mt-4">
        {detail.isPending ? <LoadingState label="Loading indicator…" /> : null}
        {detail.isError ? (
          <DetailErrorState
            error={detail.error}
            onRetry={() => void detail.refetch()}
          />
        ) : null}
        {detail.data ? (
          <div className="space-y-8">
            <IocGroupCard group={detail.data} />
            <RelationsSection
              id={detail.data.id}
              urlHost={detail.data.urlHost ?? null}
              parentDomain={detail.data.parentDomain ?? null}
              relation={parseRelation(search.relation)}
              relPage={parseRelationPage(search.relPage)}
            />
          </div>
        ) : null}
      </div>
    </section>
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
    <section aria-labelledby="relations-heading">
      <h2
        id="relations-heading"
        className="text-lg font-semibold text-slate-900"
      >
        Related indicators
      </h2>
      <p className="mt-1 text-sm text-slate-700">
        Relations load on demand, one page at a time.
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
            className={`rounded-md border px-3 py-1.5 text-sm font-medium ${focusRingClass} ${
              relation === option.kind
                ? 'border-sky-800 bg-sky-800 text-white'
                : 'border-slate-400 bg-white text-slate-800 hover:bg-slate-100'
            }`}
          >
            {option.label}
          </button>
        ))}
      </div>

      {relation !== null ? (
        <div className="mt-4 space-y-4">
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
                  {relations.data.items.map((group) => (
                    <IocGroupCard
                      key={group.id}
                      group={group}
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
