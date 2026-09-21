/**
 * @jest-environment jsdom
 */
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import {
  RouterProvider,
  createMemoryHistory,
  createRouter,
} from '@tanstack/react-router'
import { routeTree } from '../routeTree.gen'
import { ApiError } from '../lib/apiErrors'
import type { BrowseParams } from '../lib/browseParams'
import type { IocGroup, PagedIocGroups } from '../lib/schemas'
import { ipv4Group, makePaged, urlGroup } from '../test/fixtures'
import { browseIocs } from '../lib/apiClient'

jest.mock('../lib/apiClient', () => {
  const actual = jest.requireActual('../lib/apiClient')
  return { ...actual, browseIocs: jest.fn() }
})

const browseIocsMock = browseIocs as jest.MockedFunction<typeof browseIocs>

type Call = {
  params: BrowseParams
  resolve: (value: PagedIocGroups) => void
  reject: (reason: unknown) => void
  signal: AbortSignal | undefined
}

const domainGroup: IocGroup = {
  id: 9,
  iocValue: 'bad.example',
  iocType: 'domain',
  firstSeenUtc: '2026-03-01T08:00:00Z',
  lastSeenUtc: '2026-03-02T08:00:00Z',
  sources: [
    {
      provider: 'ThreatFox',
      providerRecordId: 'tf-19',
      firstSeenUtc: '2026-03-01T08:00:00Z',
      tags: [],
      isFalsePositive: false,
    },
  ],
}

function pageOf(all: IocGroup[], totalCount = all.length): PagedIocGroups {
  return makePaged(all, 1, 20, totalCount)
}

/** Records each call and resolves immediately with the responder's value. */
function useResponder(
  responder: (params: BrowseParams) => PagedIocGroups | Promise<PagedIocGroups>,
): Call[] {
  const calls: Call[] = []
  browseIocsMock.mockImplementation((params, signal) => {
    return new Promise<PagedIocGroups>((resolve, reject) => {
      calls.push({ params, resolve, reject, signal })
      Promise.resolve(responder(params)).then(resolve, reject)
    })
  })
  return calls
}

/** Records each call and leaves resolution to the test. */
function useDeferred(): Call[] {
  const calls: Call[] = []
  browseIocsMock.mockImplementation((params, signal) => {
    return new Promise<PagedIocGroups>((resolve, reject) => {
      calls.push({ params, resolve, reject, signal })
    })
  })
  return calls
}

async function renderBrowse(path = '/browse') {
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [path] }),
  })
  await act(async () => {
    render(<RouterProvider router={router as never} />)
  })
  return router
}

async function flush() {
  await act(async () => {
    await Promise.resolve()
  })
}

beforeAll(() => {
  window.scrollTo = jest.fn()
})

beforeEach(() => {
  browseIocsMock.mockReset()
})

afterEach(() => {
  cleanup()
})

describe('DataTables browse requests', () => {
  it('issues exactly one request for the initial URL', async () => {
    const calls = useResponder(() => pageOf([urlGroup, ipv4Group]))

    await renderBrowse('/browse')

    await waitFor(() => expect(calls.length).toBe(1))
    expect(calls[0].params).toMatchObject({
      page: 1,
      pageSize: 20,
      orderBy: 'lastSeenUtc',
      dir: 'desc',
    })
    await screen.findByText(urlGroup.iocValue)
    // No duplicate draw from the reflected route state.
    await flush()
    expect(browseIocsMock).toHaveBeenCalledTimes(1)
  })

  it('renders React slot content and accessible row actions', async () => {
    useResponder(() => pageOf([urlGroup, ipv4Group]))

    await renderBrowse('/browse')

    const indicator = await screen.findByText(urlGroup.iocValue)
    expect(indicator.getAttribute('href')).toBe(`/iocs/${urlGroup.id}`)
    expect(indicator.classList.contains('line-clamp-3')).toBe(true)
    expect(indicator.classList.contains('sm:line-clamp-2')).toBe(true)
    expect(indicator.classList.contains('overflow-hidden')).toBe(true)
    expect(
      screen.getByRole('button', { name: `Copy ${urlGroup.iocValue}` }),
    ).toBeTruthy()
    const table = screen.getByRole('table')
    expect(within(table).getByText('URL')).toBeTruthy()
    expect(within(table).getByText('AgentTesla')).toBeTruthy()
    expect(within(table).getAllByText('ThreatFox').length).toBeGreaterThan(0)
    expect(within(table).getByText('PhishTank')).toBeTruthy()
    const analyzeLinks = within(table).getAllByRole('link', {
      name: /Analyze/,
    })
    expect(analyzeLinks[0].getAttribute('href')).toBe(`/iocs/${urlGroup.id}`)
  })

  it('marks only supported columns orderable', async () => {
    useResponder(() => pageOf([urlGroup]))

    await renderBrowse('/browse')
    await screen.findByText(urlGroup.iocValue)

    const headers = screen.getAllByRole('columnheader')
    expect(headers[0].textContent).toContain('Indicator')
    expect(headers[1].textContent).toContain('Type')

    for (const index of [2, 3, 6]) {
      expect(headers[index].classList.contains('dt-orderable-none')).toBe(true)
      expect(
        headers[index].querySelector('.dt-column-order')?.getAttribute('role'),
      ).toBeNull()
    }
    for (const index of [0, 1, 4, 5]) {
      expect(headers[index].classList.contains('dt-orderable-none')).toBe(false)
      const order = headers[index].querySelector('.dt-column-order')
      expect(order).not.toBeNull()
      expect(order?.getAttribute('role')).toBe('button')
    }
  })

  it('page navigation requests exactly one new page', async () => {
    const calls = useResponder(() => pageOf([urlGroup], 45))

    await renderBrowse('/browse')
    await waitFor(() => expect(calls.length).toBe(1))
    await screen.findByText(urlGroup.iocValue)

    await act(async () => {
      fireEvent.click(await screen.findByLabelText('Next'))
    })

    await waitFor(() => expect(calls.length).toBe(2))
    expect(calls[1].params).toMatchObject({ page: 2, pageSize: 20 })
    expect(browseIocsMock).toHaveBeenCalledTimes(2)
  })

  it('ordering a supported column resets to page one', async () => {
    const calls = useResponder(() => pageOf([urlGroup]))

    await renderBrowse('/browse?page=3')
    await waitFor(() => expect(calls.length).toBe(1))

    const headers = screen.getAllByRole('columnheader')
    await act(async () => {
      fireEvent.click(headers[1].querySelector('.dt-column-order')!)
    })

    await waitFor(() => expect(calls.length).toBe(2))
    expect(calls[1].params).toMatchObject({
      orderBy: 'iocType',
      page: 1,
    })
    expect(browseIocsMock).toHaveBeenCalledTimes(2)
  })

  it('changing page size resets to page one with the selected size', async () => {
    const calls = useResponder(() => pageOf([urlGroup]))

    await renderBrowse('/browse?page=2&pageSize=20')
    await waitFor(() => expect(calls.length).toBe(1))

    await act(async () => {
      fireEvent.change(screen.getByLabelText('Page size'), {
        target: { value: '10' },
      })
    })

    await waitFor(() => expect(calls.length).toBe(2))
    expect(calls[1].params).toMatchObject({ page: 1, pageSize: 10 })
    expect(browseIocsMock).toHaveBeenCalledTimes(2)
  })

  it('applying a filter preserves it and resets to page one', async () => {
    const calls = useResponder(() => pageOf([urlGroup]))

    await renderBrowse('/browse?page=2')
    await waitFor(() => expect(calls.length).toBe(1))

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Domain' }))
    })

    await waitFor(() => expect(calls.length).toBe(2))
    expect(calls[1].params).toMatchObject({
      type: 'domain',
      page: 1,
      pageSize: 20,
    })
  })

  it('applying a malware filter preserves it and resets to page one', async () => {
    const calls = useResponder(() => pageOf([urlGroup]))

    await renderBrowse('/browse')
    await waitFor(() => expect(calls.length).toBe(1))

    await act(async () => {
      fireEvent.change(screen.getByLabelText('Malware'), {
        target: { value: 'Emotet' },
      })
    })
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Apply filters' }))
    })

    await waitFor(() => expect(calls.length).toBe(2))
    expect(calls[1].params).toMatchObject({
      malware: 'Emotet',
      page: 1,
      pageSize: 20,
    })
  })

  it('restores malware and target filters from the URL', async () => {
    const calls = useResponder(() => pageOf([urlGroup]))

    await renderBrowse('/browse?malware=Emotet&target=Amazon')

    await waitFor(() => expect(calls.length).toBe(1))
    expect(calls[0].params).toMatchObject({
      malware: 'Emotet',
      target: 'Amazon',
      page: 1,
    })
    expect(screen.getByLabelText<HTMLInputElement>('Malware').value).toBe(
      'Emotet',
    )
    expect(screen.getByLabelText<HTMLInputElement>('Target').value).toBe(
      'Amazon',
    )
  })

  it('clearing filters drops malware and target from the next request', async () => {
    const calls = useResponder(() => pageOf([urlGroup]))

    const router = await renderBrowse('/browse?malware=Emotet&target=Amazon')
    await waitFor(() => expect(calls.length).toBe(1))

    const clear = screen.getByRole('link', { name: 'Clear filters' })
    expect(clear.getAttribute('href')).toBe('/browse')

    await act(async () => {
      await router.navigate({ to: '/browse', search: {} })
    })

    await waitFor(() => expect(calls.length).toBe(2))
    expect(calls[1].params).toMatchObject({ malware: null, target: null })
    expect(screen.getByLabelText<HTMLInputElement>('Malware').value).toBe('')
  })

  it('does not request an oversized malware filter from the URL', async () => {
    const calls = useResponder(() => pageOf([urlGroup]))

    await renderBrowse(`/browse?malware=${'a'.repeat(201)}`)

    await screen.findByText('Unsupported filters')
    await flush()
    expect(calls.length).toBe(0)
    expect(browseIocsMock).not.toHaveBeenCalled()
  })

  it('Back and Forward restore the prior view with one request each', async () => {
    const calls = useResponder(() => pageOf([urlGroup], 45))

    const router = await renderBrowse('/browse')
    await waitFor(() => expect(calls.length).toBe(1))
    await screen.findByText(urlGroup.iocValue)

    await act(async () => {
      fireEvent.click(await screen.findByLabelText('Next'))
    })
    await waitFor(() => expect(calls.length).toBe(2))
    expect(calls[1].params.page).toBe(2)

    await act(async () => {
      router.history.back()
    })
    await waitFor(() => expect(calls.length).toBe(3))
    expect(calls[2].params.page).toBe(1)

    await act(async () => {
      router.history.forward()
    })
    await waitFor(() => expect(calls.length).toBe(4))
    expect(calls[3].params.page).toBe(2)
  })
})

describe('DataTables browse states', () => {
  it('shows the empty state for a successful empty response', async () => {
    useResponder(() => makePaged([], 1, 20, 0))

    await renderBrowse('/browse')

    await screen.findByText('No indicators found')
    expect(screen.queryByText('Could not load indicators')).toBeNull()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('shows the forbidden state without retry for a 403', async () => {
    useResponder(() => Promise.reject(new ApiError(403, 'forbidden', 'no')))

    await renderBrowse('/browse')

    await screen.findByText('Access denied')
    expect(screen.queryByText('Retry')).toBeNull()
    expect(screen.queryByText('No indicators found')).toBeNull()
  })

  it('shows a retryable error and retry issues one request for the URL', async () => {
    const calls = useDeferred()

    await renderBrowse('/browse')
    await waitFor(() => expect(calls.length).toBe(1))

    await act(async () => {
      calls[0].reject(
        new ApiError(500, 'server_error', 'Unexpected failure.', 'corr-dom'),
      )
    })

    await screen.findByText('Could not load indicators')
    expect(screen.getByText(/corr-dom/)).toBeTruthy()
    expect(screen.queryByText(urlGroup.iocValue)).toBeNull()

    await act(async () => {
      fireEvent.click(screen.getByText('Retry'))
    })
    await waitFor(() => expect(calls.length).toBe(2))
    expect(calls[1].params).toMatchObject({ page: 1, pageSize: 20 })
  })

  it('cancels superseded requests and keeps the latest result', async () => {
    const calls = useDeferred()

    await renderBrowse('/browse')
    await waitFor(() => expect(calls.length).toBe(1))
    await act(async () => {
      calls[0].resolve(pageOf([urlGroup]))
    })
    await screen.findByText(urlGroup.iocValue)

    // Start a request, then supersede it before it completes.
    const headers = screen.getAllByRole('columnheader')
    await act(async () => {
      fireEvent.click(headers[1].querySelector('.dt-column-order')!)
    })
    await waitFor(() => expect(calls.length).toBe(2))
    const superseded = calls[1]

    await act(async () => {
      fireEvent.click(headers[0].querySelector('.dt-column-order')!)
    })
    await waitFor(() => expect(calls.length).toBe(3))

    expect(superseded.signal?.aborted).toBe(true)

    // The latest request wins.
    await act(async () => {
      calls[2].resolve(pageOf([domainGroup]))
    })
    await screen.findByText(domainGroup.iocValue)

    // A late completion from the superseded request cannot replace it.
    await act(async () => {
      superseded.resolve(pageOf([ipv4Group]))
    })
    await flush()
    expect(screen.getByText(domainGroup.iocValue)).toBeTruthy()
    expect(screen.queryByText(ipv4Group.iocValue)).toBeNull()
    expect(screen.queryByRole('alert')).toBeNull()
  })
})
