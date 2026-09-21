/**
 * @jest-environment jsdom
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { getAdminTimeline } from '../lib/apiClient'
import { detectTimeZone } from '../lib/adminTimeline'
import { AdminSummaryTimeline } from './AdminSummaryTimeline'

jest.mock('../lib/apiClient', () => {
  const actual = jest.requireActual('../lib/apiClient')
  return { ...actual, getAdminTimeline: jest.fn() }
})

jest.mock('../lib/adminTimeline', () => {
  const actual = jest.requireActual('../lib/adminTimeline')
  return { ...actual, detectTimeZone: jest.fn(() => 'UTC') }
})

const getAdminTimelineMock = getAdminTimeline as jest.MockedFunction<
  typeof getAdminTimeline
>
const detectTimeZoneMock = detectTimeZone as jest.MockedFunction<
  typeof detectTimeZone
>

function renderTimeline() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={client}>
      <AdminSummaryTimeline />
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  getAdminTimelineMock.mockReset()
  detectTimeZoneMock.mockReturnValue('UTC')
  getAdminTimelineMock.mockResolvedValue({
    from: '2026-09-15T00:00:00.000Z',
    to: '2026-09-22T00:00:00.000Z',
    timeZone: 'UTC',
    hasSuccessfulFetches: true,
    points: [
      {
        localDate: '2026-09-15',
        bucketStartUtc: '2026-09-15T00:00:00.000Z',
        recordsAdded: 1,
        recordsUpdated: 0,
      },
    ],
  })
})

test('does not request the timeline while a custom range is invalid', async () => {
  renderTimeline()
  await waitFor(() => expect(getAdminTimelineMock).toHaveBeenCalled())
  getAdminTimelineMock.mockClear()

  fireEvent.click(screen.getByLabelText('Custom'))
  expect(screen.getByRole('alert').textContent).toContain(
    'both a start and end date',
  )
  expect(getAdminTimelineMock).not.toHaveBeenCalled()

  fireEvent.change(screen.getByLabelText('Start date'), {
    target: { value: '2026-09-21' },
  })
  fireEvent.change(screen.getByLabelText('End date'), {
    target: { value: '2026-09-15' },
  })
  expect(screen.getByRole('alert').textContent).toContain('on or before')
  expect(getAdminTimelineMock).not.toHaveBeenCalled()

  fireEvent.change(screen.getByLabelText('Start date'), {
    target: { value: '2026-08-01' },
  })
  fireEvent.change(screen.getByLabelText('End date'), {
    target: { value: '2026-09-01' },
  })
  expect(screen.getByRole('alert').textContent).toContain('at most 31')
  expect(getAdminTimelineMock).not.toHaveBeenCalled()
})

test('requests the timeline once a valid custom range is entered', async () => {
  renderTimeline()
  await waitFor(() => expect(getAdminTimelineMock).toHaveBeenCalled())
  getAdminTimelineMock.mockClear()

  fireEvent.click(screen.getByLabelText('Custom'))
  fireEvent.change(screen.getByLabelText('Start date'), {
    target: { value: '2026-09-15' },
  })
  fireEvent.change(screen.getByLabelText('End date'), {
    target: { value: '2026-09-21' },
  })

  await waitFor(() => expect(getAdminTimelineMock).toHaveBeenCalledTimes(1))
  expect(getAdminTimelineMock.mock.calls[0][0]).toEqual({
    from: '2026-09-15T00:00:00.000Z',
    to: '2026-09-22T00:00:00.000Z',
    timeZone: 'UTC',
  })
})

test('keeps the last points and marks the timeline stale after a failed refresh', async () => {
  renderTimeline()
  await waitFor(() =>
    expect(screen.getAllByText('2026-09-15').length).toBeGreaterThan(0),
  )

  getAdminTimelineMock.mockRejectedValueOnce(new Error('network down'))
  fireEvent.click(screen.getByRole('button', { name: /refresh/i }))

  await waitFor(() =>
    expect(screen.getByText(/Stale timeline/)).toBeTruthy(),
  )
  expect(screen.getAllByText('2026-09-15').length).toBeGreaterThan(0)
})

test('pauses automatic polling while the document is hidden', async () => {
  jest.useFakeTimers()
  try {
    renderTimeline()
    await waitFor(() => expect(getAdminTimelineMock).toHaveBeenCalledTimes(1))
    getAdminTimelineMock.mockClear()

    const visibility = jest
      .spyOn(document, 'visibilityState', 'get')
      .mockReturnValue('hidden')
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'))
    })
    act(() => {
      jest.advanceTimersByTime(90000)
    })
    expect(getAdminTimelineMock).not.toHaveBeenCalled()

    visibility.mockReturnValue('visible')
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'))
    })
    act(() => {
      jest.advanceTimersByTime(30000)
    })
    await waitFor(() => expect(getAdminTimelineMock).toHaveBeenCalled())
    visibility.mockRestore()
  } finally {
    jest.useRealTimers()
  }
})

test('updates the disclosure and query when the browser timezone changes', async () => {
  renderTimeline()
  await waitFor(() =>
    expect(screen.getByText(/Times shown in UTC \(UTC\+00:00\)/)).toBeTruthy(),
  )
  getAdminTimelineMock.mockClear()

  detectTimeZoneMock.mockReturnValue('Europe/Istanbul')
  act(() => {
    window.dispatchEvent(new Event('focus'))
  })

  await waitFor(() =>
    expect(
      screen.getByText(/Times shown in Europe\/Istanbul \(UTC\+03:00\)/),
    ).toBeTruthy(),
  )
  await waitFor(() => expect(getAdminTimelineMock).toHaveBeenCalled())
  expect(getAdminTimelineMock.mock.calls.at(-1)![0]).toMatchObject({
    timeZone: 'Europe/Istanbul',
  })
})
