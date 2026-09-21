import type { ReactNode } from 'react'

const stateBoxClass =
  'my-6 rounded border border-outline-variant bg-surface-container-low px-4 py-3 text-on-surface'

/** Explicit loading state, announced politely to assistive technology. */
export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <div role="status" aria-live="polite" className={stateBoxClass}>
      <p className="flex items-center gap-2 font-mono text-xs uppercase tracking-wider text-on-surface-variant">
        <span
          aria-hidden="true"
          className="h-3 w-3 animate-spin rounded-full border-2 border-outline-variant border-t-primary"
        />
        {label}
      </p>
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
      <p className="font-semibold text-on-surface">{title}</p>
      {message ? (
        <p className="mt-1 text-sm text-on-surface-variant">{message}</p>
      ) : null}
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
      className="my-6 rounded border border-error/40 bg-error-container px-4 py-3 text-on-error-container"
    >
      <p className="font-semibold">{title}</p>
      {message ? <p className="mt-1 text-sm">{message}</p> : null}
      {correlationId ? (
        <p className="mt-1 font-mono text-xs uppercase tracking-wider">
          Reference: {correlationId}
        </p>
      ) : null}
      {onRetry ? (
        <button
          type="button"
          onClick={onRetry}
          className="mt-3 rounded bg-error px-3 py-1.5 text-xs font-semibold uppercase tracking-wider text-on-error focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
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
