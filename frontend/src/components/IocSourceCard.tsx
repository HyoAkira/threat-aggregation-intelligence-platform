import type { IocSource } from '../lib/schemas'
import {
  formatFalsePositive,
  formatFieldValue,
  formatUtcDateTime,
} from '../lib/format'

const cardClass =
  'rounded border border-outline-variant bg-surface-container-lowest'
const fieldRowClass =
  'grid grid-cols-[minmax(7rem,auto)_1fr] items-baseline gap-x-3 gap-y-0.5 py-1 text-xs'
const fieldLabelClass =
  'font-mono text-[10px] uppercase tracking-wider text-outline'
const fieldValueClass = 'break-words font-mono text-on-surface'
const tagClass =
  'rounded-full bg-surface-container px-2 py-0.5 font-mono text-[10px] font-medium uppercase tracking-wider text-on-surface-variant'

/**
 * One labelled source entry: provider attribution, record id, port, first/last
 * seen, false-positive state, tags, external details link, and the provider's
 * typed fields. Null properties produce no label; stored false, zero, and empty
 * values stay visible and understandable. Provider-specific fields always stay
 * on their own card.
 */
export function IocSourceCard({ source }: { source: IocSource }) {
  const firstSeen = formatUtcDateTime(source.firstSeenUtc)
  const lastSeen = formatUtcDateTime(source.lastSeenUtc ?? undefined)

  return (
    <article className={cardClass} aria-label={`Source: ${source.provider}`}>
      <header className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1 border-b border-outline-variant/60 px-3 py-2">
        <h4 className="flex flex-wrap items-baseline gap-x-2 text-sm font-semibold text-on-surface">
          {source.provider}
          <span className="font-mono text-[10px] font-normal uppercase tracking-wider text-outline">
            record {source.providerRecordId}
          </span>
        </h4>
        <p
          className={`rounded-full px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider ${
            source.isFalsePositive
              ? 'bg-warning-container text-on-warning-container'
              : 'bg-tertiary-container/10 text-tertiary'
          }`}
        >
          {formatFalsePositive(source.isFalsePositive)}
        </p>
      </header>

      <dl className="grid grid-cols-1 gap-x-6 px-3 py-2 sm:grid-cols-2">
        <SourceField label="Port" value={formatFieldValue(source.port)} />
        <SourceField label="First seen" value={firstSeen} />
        <SourceField label="Last seen" value={lastSeen} />
        <SourceField
          label="Threat type"
          value={formatFieldValue(source.threatType)}
        />
        <SourceField label="Malware" value={formatFieldValue(source.malware)} />
        <SourceField
          label="Malware alias"
          value={formatFieldValue(source.malwareAlias)}
        />
        <SourceField
          label="Malware (printable)"
          value={formatFieldValue(source.malwarePrintable)}
        />
        <SourceField
          label="Compromised"
          value={formatFieldValue(source.isCompromised)}
        />
        <SourceField
          label="Reporter"
          value={formatFieldValue(source.reporter)}
        />
        <SourceField
          label="Anonymous reporter"
          value={formatFieldValue(source.anonymous)}
        />
        <SourceField
          label="Verified"
          value={formatFieldValue(source.isVerified)}
        />
        <SourceField label="Target" value={formatFieldValue(source.target)} />
        <SourceField
          label="Online"
          value={formatFieldValue(source.phishTankOnline)}
        />
        <SourceField
          label="Confidence level"
          value={formatFieldValue(source.threatFoxConfidenceLevel)}
        />
        <PhishTankDetailsField details={source.phishTankDetails ?? null} />
      </dl>

      {source.tags.length > 0 ? (
        <p className="flex flex-wrap items-center gap-1.5 border-t border-outline-variant/60 px-3 py-2">
          <span className="font-mono text-[10px] uppercase tracking-wider text-outline">
            Tags:
          </span>
          {source.tags.map((tag) => (
            <span key={tag} className={tagClass}>
              {tag}
            </span>
          ))}
        </p>
      ) : null}

      {source.providerDetailsLink ? (
        <p className="border-t border-outline-variant/60 px-3 py-2 text-xs">
          <a
            href={source.providerDetailsLink}
            target="_blank"
            rel="noopener noreferrer"
            className="font-medium text-primary underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
          >
            View provider record (external link, opens in a new tab)
          </a>
        </p>
      ) : null}
    </article>
  )
}

/** Renders one definition row only when the value is present (null is omitted). */
function SourceField({
  label,
  value,
}: {
  label: string
  value: string | null
}) {
  if (value === null) return null
  return (
    <div className={fieldRowClass}>
      <dt className={fieldLabelClass}>{label}:</dt>
      <dd className={fieldValueClass}>{value}</dd>
    </div>
  )
}

/**
 * PhishTank's validated details JSON. An empty details value is still data, so it
 * renders an explicit "empty" note instead of disappearing.
 */
function PhishTankDetailsField({ details }: { details: unknown }) {
  if (details === null || details === undefined) return null
  const isEmpty =
    (Array.isArray(details) && details.length === 0) ||
    (typeof details === 'object' && Object.keys(details).length === 0)
  if (isEmpty) {
    return (
      <div className={`${fieldRowClass} sm:col-span-2`}>
        <dt className={fieldLabelClass}>Details:</dt>
        <dd className={fieldValueClass}>(no detail entries)</dd>
      </div>
    )
  }
  return (
    <div className="py-1 sm:col-span-2">
      <dt className={fieldLabelClass}>Details:</dt>
      <dd>
        <pre className="mt-1 max-w-xl overflow-x-auto rounded bg-surface-container-low p-2 font-mono text-[11px] leading-relaxed text-on-surface">
          {JSON.stringify(details, null, 2)}
        </pre>
      </dd>
    </div>
  )
}
