import { Link } from '@tanstack/react-router'
import type { IocGroup } from '../lib/schemas'
import { formatLocalDateTime } from '../lib/format'
import { IocSourceCard } from './IocSourceCard'
import { IocFalsePositiveBadge } from './IocFalsePositiveBadge'

const typeBadgeClass: Record<IocGroup['iocType'], string> = {
  ipv4: 'bg-primary-fixed text-on-primary-fixed',
  ipv6: 'bg-surface-container-highest text-on-surface',
  domain: 'bg-secondary-container text-on-secondary-container',
  url: 'bg-tertiary-container/10 text-tertiary',
}

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
  const firstSeen = formatLocalDateTime(group.firstSeenUtc)
  const lastSeen = formatLocalDateTime(group.lastSeenUtc ?? undefined)

  return (
    <section
      className="overflow-hidden rounded border border-outline-variant bg-surface-container-lowest"
      aria-label={`IOC group ${group.iocValue}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2 border-b border-outline-variant/60 bg-surface-container-low/60 px-4 py-3">
        <Heading className="flex flex-wrap items-center gap-x-3 gap-y-1">
          {linkToDetail ? (
            <Link
              to="/iocs/$id"
              params={{ id: String(group.id) }}
              className="break-all font-mono text-sm font-semibold text-primary underline-offset-2 hover:text-primary-container hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
            >
              {group.iocValue}
            </Link>
          ) : (
            <span className="break-all font-mono text-sm font-semibold text-on-surface">
              {group.iocValue}
            </span>
          )}
          <span
            className={`rounded px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider ${typeBadgeClass[group.iocType]}`}
          >
            {group.iocType}
          </span>
        </Heading>
        <div className="flex flex-wrap items-center justify-end gap-2">
          <IocFalsePositiveBadge sources={group.sources} />
          <span className="font-mono text-[10px] uppercase tracking-wider text-outline">
            Group #{group.id}
          </span>
        </div>
      </div>

      <dl className="flex flex-wrap gap-x-6 gap-y-2 px-4 py-3 text-xs">
        <div className="flex flex-wrap gap-x-1.5">
          <dt className="font-mono uppercase tracking-wider text-outline">
            First seen:
          </dt>
          <dd className="font-mono text-on-surface">
            {firstSeen ?? 'Unknown'}
          </dd>
        </div>
        <div className="flex flex-wrap gap-x-1.5">
          <dt className="font-mono uppercase tracking-wider text-outline">
            Last seen:
          </dt>
          <dd className="font-mono text-on-surface">{lastSeen ?? 'Unknown'}</dd>
        </div>
        {group.urlHost ? (
          <div className="flex flex-wrap gap-x-1.5">
            <dt className="font-mono uppercase tracking-wider text-outline">
              URL host:
            </dt>
            <dd className="font-mono text-on-surface">{group.urlHost}</dd>
          </div>
        ) : null}
        {group.parentDomain ? (
          <div className="flex flex-wrap gap-x-1.5">
            <dt className="font-mono uppercase tracking-wider text-outline">
              Parent domain:
            </dt>
            <dd className="font-mono text-on-surface">{group.parentDomain}</dd>
          </div>
        ) : null}
        <div className="flex flex-wrap gap-x-1.5">
          <dt className="font-mono uppercase tracking-wider text-outline">
            Sources:
          </dt>
          <dd className="font-mono text-on-surface">{group.sources.length}</dd>
        </div>
      </dl>

      <div className="space-y-3 bg-surface-container-low/40 p-4">
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
