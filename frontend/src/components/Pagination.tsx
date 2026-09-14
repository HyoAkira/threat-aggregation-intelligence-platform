import { focusRingClass } from './AppShell'

const pageButtonClass = `rounded border border-outline-variant bg-surface-container-lowest px-3 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-wider text-on-surface transition-colors enabled:hover:bg-surface-container-low disabled:cursor-not-allowed disabled:opacity-40 ${focusRingClass}`

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
    <nav
      aria-label="Pagination"
      className="flex flex-wrap items-center gap-3 border-t border-outline-variant/50 bg-surface-container-lowest px-4 py-3"
    >
      <button
        type="button"
        onClick={() => onPage(page - 1)}
        disabled={!hasPrevious}
        className={pageButtonClass}
      >
        Previous page
      </button>
      <span
        aria-live="polite"
        className="font-mono text-[11px] uppercase tracking-wider text-on-surface-variant"
      >
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
