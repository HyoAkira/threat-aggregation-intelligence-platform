import { focusRingClass } from './AppShell'

const pageButtonClass = `rounded-md border border-slate-400 bg-white px-3 py-1.5 text-sm font-medium text-slate-800 enabled:hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 ${focusRingClass}`

/**
 * Bounded pagination: one API page at a time, with previous/next controls that
 * are disabled at the bounds and an explicit page/total summary.
 */
export function Pagination({
  page,
  pageSize,
  totalCount,
  onPage,
  pageLabel = 'Page',
}: {
  page: number
  pageSize: number
  totalCount: number
  onPage: (page: number) => void
  pageLabel?: string
}) {
  const totalPages = totalCount === 0 ? 0 : Math.ceil(totalCount / pageSize)
  const hasPrevious = page > 1
  const hasNext = page < totalPages

  return (
    <nav aria-label="Pagination" className="flex flex-wrap items-center gap-3">
      <button
        type="button"
        onClick={() => onPage(page - 1)}
        disabled={!hasPrevious}
        className={pageButtonClass}
      >
        Previous page
      </button>
      <span aria-live="polite" className="text-sm text-slate-700">
        {pageLabel} {page} of {Math.max(totalPages, 1)} · {totalCount} total
      </span>
      <button
        type="button"
        onClick={() => onPage(page + 1)}
        disabled={!hasNext}
        className={pageButtonClass}
      >
        Next page
      </button>
    </nav>
  )
}
