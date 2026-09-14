import type { IocGroup, IocTypeToken } from './schemas'
import { formatUtcDateTime } from './format'

/** Human labels for the four indicator types the API supports. */
export const iocTypeLabels: Record<IocTypeToken, string> = {
  ipv4: 'IPv4',
  ipv6: 'IPv6',
  domain: 'Domain',
  url: 'URL',
}

/** Tonal pill styling per indicator type (Material tokens). */
export const iocTypeBadgeClass: Record<IocTypeToken, string> = {
  ipv4: 'bg-primary-fixed text-on-primary-fixed',
  ipv6: 'bg-surface-container-highest text-on-surface',
  domain: 'bg-secondary-container text-on-secondary-container',
  url: 'bg-tertiary-container/10 text-tertiary',
}

const IPV6_HINT_PATTERN =
  /^(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$|^(?:[0-9a-fA-F]{1,4}:){1,7}:$|^(?:[0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}$|^(?:[0-9a-fA-F]{1,4}:){1,5}(?::[0-9a-fA-F]{1,4}){1,2}$|^(?:[0-9a-fA-F]{1,4}:){1,4}(?::[0-9a-fA-F]{1,4}){1,3}$|^(?:[0-9a-fA-F]{1,4}:){1,3}(?::[0-9a-fA-F]{1,4}){1,4}$|^(?:[0-9a-fA-F]{1,4}:){1,2}(?::[0-9a-fA-F]{1,4}){1,5}$|^[0-9a-fA-F]{1,4}:(?::[0-9a-fA-F]{1,4}){1,6}$|^:(?::[0-9a-fA-F]{1,4}){1,7}$|^::$/

/**
 * Client-side IOC type detection for the search-bar hint badge. This is a
 * presentation hint only: the API owns classification, normalization, and
 * rejection, so an unrecognized value simply shows no badge and lets the
 * request surface the authoritative answer.
 */
export function detectIocType(value: string): IocTypeToken | null {
  const candidate = value.trim()
  if (candidate === '') return null
  if (/^(?:\d{1,3}\.){3}\d{1,3}$/.test(candidate)) return 'ipv4'
  if (IPV6_HINT_PATTERN.test(candidate)) return 'ipv6'
  if (/^https?:\/\//i.test(candidate)) return 'url'
  if (/^([a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,}$/i.test(candidate))
    return 'domain'
  return null
}

/** Short stable UTC presentation for table cells ("YYYY-MM-DD HH:MM UTC"). */
export function formatShortUtc(
  value: string | null | undefined,
): string | null {
  const formatted = formatUtcDateTime(value)
  return formatted ? `${formatted.slice(0, 16)} UTC` : null
}

/** First human-readable threat label across the group's sources, or null. */
export function threatLabel(group: IocGroup): string | null {
  for (const source of group.sources) {
    const label = source.malwarePrintable ?? source.threatType ?? source.malware
    if (label != null && label !== '') return label
  }
  return null
}
