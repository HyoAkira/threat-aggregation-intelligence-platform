import type { BrowseParams } from './browseParams'
import type { IocRelation, SearchSubmission } from './apiClient'

export const iocSearchKey = (submission: SearchSubmission) =>
  [
    'ioc-search',
    submission.q,
    submission.tag ?? null,
    submission.provider ?? null,
  ] as const

export const iocDetailKey = (id: number) => ['ioc-detail', id] as const

export const iocRelationsKey = (
  id: number,
  relation: IocRelation | 'none',
  page: number,
) => ['ioc-relations', id, relation, page] as const

export const browseKey = (params: BrowseParams) =>
  [
    'ioc-browse',
    params.type,
    params.provider,
    params.tag,
    params.orderBy,
    params.dir,
    params.page,
    params.pageSize,
  ] as const
