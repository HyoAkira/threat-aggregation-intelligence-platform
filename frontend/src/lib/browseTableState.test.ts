import { defaultBrowseParams } from './browseParams'
import type { BrowseParams } from './browseParams'
import {
  browseParamsFingerprint,
  browseTableStateFromParams,
  initialBrowseSyncState,
  onBrowseTableAction,
  onBrowseUrlChange,
} from './browseTableState'

const base = defaultBrowseParams

function view(overrides: Partial<BrowseParams>): BrowseParams {
  return { ...base, ...overrides }
}

describe('browseTableStateFromParams', () => {
  it('derives displayStart, pageLength, and the single order term', () => {
    expect(browseTableStateFromParams(view({ page: 3, pageSize: 10 }))).toEqual(
      {
        displayStart: 20,
        pageLength: 10,
        order: [5, 'desc'],
      },
    )
    expect(
      browseTableStateFromParams(
        view({ orderBy: 'iocValue', dir: 'asc', page: 1, pageSize: 50 }),
      ),
    ).toEqual({ displayStart: 0, pageLength: 50, order: [0, 'asc'] })
  })
})

describe('fingerprints', () => {
  it('are stable for equal views and differ for any normalized change', () => {
    expect(browseParamsFingerprint(base)).toBe(
      browseParamsFingerprint({ ...base }),
    )
    expect(browseParamsFingerprint(base)).not.toBe(
      browseParamsFingerprint(view({ page: 2 })),
    )
    expect(browseParamsFingerprint(base)).not.toBe(
      browseParamsFingerprint(view({ tag: 'botnet' })),
    )
    expect(browseParamsFingerprint(base)).not.toBe(
      browseParamsFingerprint(view({ malware: 'Emotet' })),
    )
    expect(browseParamsFingerprint(base)).not.toBe(
      browseParamsFingerprint(view({ target: 'Amazon' })),
    )
  })
})

describe('URL and table synchronization transitions', () => {
  it('initial URL requires exactly one draw and records representation', () => {
    const outcome = onBrowseUrlChange(initialBrowseSyncState, base)
    expect(outcome.kind).toBe('initialize')
    if (outcome.kind !== 'initialize') throw new Error('expected initialize')
    expect(outcome.table).toEqual(browseTableStateFromParams(base))
    expect(outcome.state.initialized).toBe(true)
    expect(outcome.state.represented).toBe(browseParamsFingerprint(base))
  })

  it('a table page action sets the guard and needs no extra draw', () => {
    const initialized = onBrowseUrlChange(initialBrowseSyncState, base)
    const pageTwo = view({ page: 2 })

    const state = onBrowseTableAction(initialized.state, pageTwo)

    expect(state.pendingTable).toBe(browseParamsFingerprint(pageTwo))
    expect(state.represented).toBe(browseParamsFingerprint(pageTwo))
  })

  it('the reflected URL from a table action requires no draw', () => {
    const initialized = onBrowseUrlChange(initialBrowseSyncState, base)
    const pageTwo = view({ page: 2 })
    const guarded = onBrowseTableAction(initialized.state, pageTwo)

    const outcome = onBrowseUrlChange(guarded, pageTwo)

    expect(outcome.kind).toBe('reflect')
    expect(outcome.state.pendingTable).toBeNull()
    expect(outcome.state.represented).toBe(browseParamsFingerprint(pageTwo))
  })

  it('a filter reset to page one applies table state with exactly one draw', () => {
    const initialized = onBrowseUrlChange(
      initialBrowseSyncState,
      view({ page: 4 }),
    )
    const filtered = view({ type: 'url', page: 1 })

    const outcome = onBrowseUrlChange(initialized.state, filtered)

    expect(outcome.kind).toBe('apply')
    if (outcome.kind !== 'apply') throw new Error('expected apply')
    expect(outcome.table.displayStart).toBe(0)
    expect(outcome.state.pendingTable).toBeNull()
  })

  it('a page-size change applies the new length without an intermediate draw', () => {
    const initialized = onBrowseUrlChange(
      initialBrowseSyncState,
      view({ page: 3 }),
    )
    const resized = view({ page: 1, pageSize: 50 })

    const outcome = onBrowseUrlChange(initialized.state, resized)

    expect(outcome.kind).toBe('apply')
    if (outcome.kind !== 'apply') throw new Error('expected apply')
    expect(outcome.table).toEqual({
      displayStart: 0,
      pageLength: 50,
      order: [5, 'desc'],
    })
  })

  it('an external Back/Forward view applies with exactly one draw', () => {
    const initialized = onBrowseUrlChange(
      initialBrowseSyncState,
      view({ page: 3 }),
    )
    const historical = view({
      type: 'domain',
      orderBy: 'firstSeenUtc',
      dir: 'asc',
      page: 2,
      pageSize: 10,
    })

    const outcome = onBrowseUrlChange(initialized.state, historical)

    expect(outcome.kind).toBe('apply')
    if (outcome.kind !== 'apply') throw new Error('expected apply')
    expect(outcome.table).toEqual(browseTableStateFromParams(historical))
  })

  it('an unchanged fingerprint is ignored and requires no draw', () => {
    const initialized = onBrowseUrlChange(initialBrowseSyncState, base)
    const outcome = onBrowseUrlChange(initialized.state, { ...base })
    expect(outcome.kind).toBe('ignore')
  })

  it('a table ordering action is reflected without a second draw', () => {
    const initialized = onBrowseUrlChange(initialBrowseSyncState, base)
    const ordered = view({ orderBy: 'iocValue', dir: 'asc', page: 1 })
    const guarded = onBrowseTableAction(initialized.state, ordered)

    const outcome = onBrowseUrlChange(guarded, ordered)

    expect(outcome.kind).toBe('reflect')
    expect(outcome.state.represented).toBe(browseParamsFingerprint(ordered))
  })

  it('the guard is consumed exactly once', () => {
    const initialized = onBrowseUrlChange(initialBrowseSyncState, base)
    const pageTwo = view({ page: 2 })
    const guarded = onBrowseTableAction(initialized.state, pageTwo)

    const first = onBrowseUrlChange(guarded, pageTwo)
    // A later external change to the same view is a real (non-reflected) change.
    const again = onBrowseUrlChange(first.state, pageTwo)
    expect(again.kind).toBe('ignore')
  })
})
