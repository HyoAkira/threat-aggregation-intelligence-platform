import type { IocSource } from '../lib/schemas'

/**
 * Application-derived freshness assessment for the complete IOC group.
 * This deliberately lives outside provider cards because it is not provider evidence.
 */
export function IocFalsePositiveBadge({ sources }: { sources: IocSource[] }) {
  const count = sources.filter((source) => source.isFalsePositive).length
  const status =
    count > 0
      ? `${count} source${count === 1 ? '' : 's'} marked false positive`
      : 'Not marked false positive'

  return (
    <span
      title="Application-derived status based on record freshness; not supplied by the provider."
      className={`rounded px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider ${
        count > 0
          ? 'bg-warning-container text-on-warning-container'
          : 'bg-tertiary-container/10 text-tertiary'
      }`}
    >
      {status}
    </span>
  )
}
