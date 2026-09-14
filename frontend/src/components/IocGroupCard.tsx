import { Link } from '@tanstack/react-router'
import type { IocGroup } from '../lib/schemas'
import { formatUtcDateTime } from '../lib/format'
import { IocSourceCard } from './IocSourceCard'

/**
 * Shared presentation for one normalized IOC group: the group summary (value,
 * type, aggregate dates, host relations) followed by every source entry as its
 * own labelled card, preserving provider attribution. Used by search, detail,
 * relations, and browse so attribution looks identical everywhere.
 */
export function IocGroupCard({
  group,
  headingLevel = 'h2',
  linkToDetail = false,
}: {
  group: IocGroup
  headingLevel?: 'h2' | 'h3'
  linkToDetail?: boolean
}) {
  const Heading = headingLevel
  const firstSeen = formatUtcDateTime(group.firstSeenUtc)
  const lastSeen = formatUtcDateTime(group.lastSeenUtc ?? undefined)

  return (
    <section
      className="rounded-lg border border-slate-200 bg-slate-50 p-4"
      aria-label={`IOC group ${group.iocValue}`}
    >
      <Heading className="flex flex-wrap items-baseline gap-x-3 gap-y-1 text-lg font-semibold text-slate-900">
        {linkToDetail ? (
          <Link
            to="/iocs/$id"
            params={{ id: String(group.id) }}
            className="font-mono text-sky-900 underline underline-offset-2 hover:text-sky-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
          >
            {group.iocValue}
          </Link>
        ) : (
          <span className="font-mono">{group.iocValue}</span>
        )}
        <span className="rounded bg-slate-200 px-2 py-0.5 text-xs font-semibold uppercase tracking-wide text-slate-700">
          {group.iocType}
        </span>
      </Heading>
      <dl className="mt-2 flex flex-wrap gap-x-6 gap-y-1 text-sm">
        <div className="flex flex-wrap gap-x-1.5">
          <dt className="font-medium text-slate-700">First seen:</dt>
          <dd>{firstSeen ?? 'Unknown'}</dd>
        </div>
        <div className="flex flex-wrap gap-x-1.5">
          <dt className="font-medium text-slate-700">Last seen:</dt>
          <dd>{lastSeen ?? 'Unknown'}</dd>
        </div>
        {group.urlHost ? (
          <div className="flex flex-wrap gap-x-1.5">
            <dt className="font-medium text-slate-700">URL host:</dt>
            <dd className="font-mono">{group.urlHost}</dd>
          </div>
        ) : null}
        {group.parentDomain ? (
          <div className="flex flex-wrap gap-x-1.5">
            <dt className="font-medium text-slate-700">Parent domain:</dt>
            <dd className="font-mono">{group.parentDomain}</dd>
          </div>
        ) : null}
        <div className="flex flex-wrap gap-x-1.5">
          <dt className="font-medium text-slate-700">Sources:</dt>
          <dd>{group.sources.length}</dd>
        </div>
      </dl>
      <div className="mt-4 space-y-3">
        {group.sources.map((source, index) => (
          <IocSourceCard
            key={`${source.provider}-${source.providerRecordId}-${index}`}
            source={source}
          />
        ))}
      </div>
    </section>
  )
}
