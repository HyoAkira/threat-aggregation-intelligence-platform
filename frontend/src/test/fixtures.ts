import type { IocGroup, PagedIocGroups } from '../lib/schemas'
import type { StoredSession } from '../lib/session'

export function makeSession(
  overrides: Partial<StoredSession> = {},
): StoredSession {
  return {
    token: 'jwt-token',
    username: 'analyst',
    role: 'User',
    expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
    ...overrides,
  }
}

/** ThreatFox source retaining stored zero and non-null values. */
export const threatFoxSource = {
  provider: 'ThreatFox',
  providerRecordId: 'tf-17',
  port: 443,
  firstSeenUtc: '2026-01-01T08:00:00Z',
  lastSeenUtc: '2026-06-01T08:00:00Z',
  threatType: 'botnet',
  malware: 'win.agenttesla',
  malwareAlias: 'agenttesla',
  malwarePrintable: 'AgentTesla',
  threatFoxConfidenceLevel: 0,
  anonymous: false,
  tags: ['botnet', 'credential_stealer'],
  isFalsePositive: false,
}

/** PhishTank source retaining stored false, empty string, and empty details values. */
export const phishTankSource = {
  provider: 'PhishTank',
  providerRecordId: 'pt-9',
  firstSeenUtc: '2026-02-01T08:00:00Z',
  lastSeenUtc: '2026-07-01T08:00:00Z',
  isVerified: false,
  phishTankOnline: false,
  target: '',
  phishTankDetails: [],
  tags: ['phishing'],
  isFalsePositive: true,
}

export const urlGroup: IocGroup = {
  id: 7,
  iocValue: 'https://evil.example/x',
  iocType: 'url',
  urlHost: 'evil.example',
  parentDomain: 'evil.example',
  firstSeenUtc: '2026-01-01T08:00:00Z',
  lastSeenUtc: '2026-07-01T08:00:00Z',
  sources: [threatFoxSource, phishTankSource],
}

export const ipv4Group: IocGroup = {
  id: 8,
  iocValue: '1.2.3.4',
  iocType: 'ipv4',
  firstSeenUtc: '2026-01-02T08:00:00Z',
  lastSeenUtc: '2026-05-02T08:00:00Z',
  sources: [
    {
      provider: 'ThreatFox',
      providerRecordId: 'tf-18',
      firstSeenUtc: '2026-01-02T08:00:00Z',
      tags: [],
      isFalsePositive: false,
    },
  ],
}

export function makePaged(
  items: IocGroup[],
  page: number,
  pageSize: number,
  totalCount: number,
): PagedIocGroups {
  return { items, page, pageSize, totalCount }
}

/** Minimal in-memory sessionStorage shim for the Node test environment. */
export function createSessionStorageStub() {
  const store = new Map<string, string>()
  return {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => void store.set(key, value),
    removeItem: (key: string) => void store.delete(key),
    clear: () => store.clear(),
  }
}
