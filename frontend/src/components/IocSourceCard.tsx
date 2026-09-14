import type { IocSource } from '../lib/schemas'
import {
  formatFalsePositive,
  formatFieldValue,
  formatUtcDateTime,
} from '../lib/format'

const cardClass = 'rounded-lg border border-slate-200 bg-white p-4'
const fieldRowClass = 'flex flex-wrap gap-x-2 gap-y-0.5 text-sm'
const fieldLabelClass = 'font-medium text-slate-700'
const fieldValueClass = 'text-slate-900'
const tagClass =
  'rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700'

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
      <header className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <h4 className="text-base font-semibold text-slate-900">
          {source.provider}
          <span className="ml-2 font-mono text-xs font-normal text-slate-500">
            record {source.providerRecordId}
          </span>
        </h4>
        <p
          className={`text-xs font-medium ${source.isFalsePositive ? 'text-amber-700' : 'text-slate-600'}`}
        >
          {formatFalsePositive(source.isFalsePositive)}
        </p>
      </header>

      <dl className="mt-2 space-y-1">
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
        <p className="mt-3 flex flex-wrap items-center gap-1.5">
          <span className="text-xs font-medium text-slate-600">Tags:</span>
          {source.tags.map((tag) => (
            <span key={tag} className={tagClass}>
              {tag}
            </span>
          ))}
        </p>
      ) : null}

      {source.providerDetailsLink ? (
        <p className="mt-3 text-sm">
          <a
            href={source.providerDetailsLink}
            target="_blank"
            rel="noopener noreferrer"
            className="font-medium text-sky-800 underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
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
      <div className={fieldRowClass}>
        <dt className={fieldLabelClass}>Details:</dt>
        <dd className={fieldValueClass}>(no detail entries)</dd>
      </div>
    )
  }
  return (
    <div className="text-sm">
      <dt className={fieldLabelClass}>Details:</dt>
      <dd>
        <pre className="mt-1 max-w-xl overflow-x-auto rounded-md bg-slate-50 p-2 font-mono text-xs text-slate-800">
          {JSON.stringify(details, null, 2)}
        </pre>
      </dd>
    </div>
  )
}
