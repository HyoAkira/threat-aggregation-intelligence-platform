import type { ReactNode } from 'react'

const stateBoxClass =
  'my-6 rounded-lg border border-slate-300 bg-slate-50 px-4 py-3 text-slate-800'

/** Explicit loading state, announced politely to assistive technology. */
export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <div role="status" aria-live="polite" className={stateBoxClass}>
      <p>{label}</p>
    </div>
  )
}

/** Explicit empty state for valid responses that contain no data. */
export function EmptyState({
  title,
  message,
  action,
}: {
  title: string
  message?: string
  action?: ReactNode
}) {
  return (
    <div role="status" className={stateBoxClass}>
      <p className="font-medium">{title}</p>
      {message ? <p className="mt-1 text-sm">{message}</p> : null}
      {action ? <div className="mt-3">{action}</div> : null}
    </div>
  )
}

/**
 * Error state for request failures. Distinguishes forbidden access (403, which is
 * never retried) from unexpected failures, which are retryable and show the
 * request's correlation ID for support and log correlation.
 */
export function ErrorState({
  title,
  message,
  correlationId,
  onRetry,
  retryLabel = 'Retry',
}: {
  title: string
  message?: string
  correlationId?: string
  onRetry?: () => void
  retryLabel?: string
}) {
  return (
    <div
      role="alert"
      className="my-6 rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-red-900"
    >
      <p className="font-medium">{title}</p>
      {message ? <p className="mt-1 text-sm">{message}</p> : null}
      {correlationId ? (
        <p className="mt-1 text-sm">
          Reference:{<span className="font-mono">{correlationId}</span>}
        </p>
      ) : null}
      {onRetry ? (
        <button
          type="button"
          onClick={onRetry}
          className="mt-3 rounded-md bg-red-700 px-3 py-1.5 text-sm font-medium text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-700"
        >
          {retryLabel}
        </button>
      ) : null}
    </div>
  )
}

/** Forbidden state shown for 403 responses; never retried automatically. */
export function ForbiddenState({ message }: { message?: string }) {
  return (
    <ErrorState
      title="Access denied"
      message={message ?? 'You do not have permission to view this content.'}
    />
  )
}
