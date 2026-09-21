/**
 * @jest-environment jsdom
 */
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { IocChatPanel } from './IocChatPanel'
import { postIocChat } from '../lib/iocChatClient'

jest.mock('@tanstack/react-router', () => ({
  Link: ({ children }: { children: ReactNode }) => (
    <a href="/iocs/7">{children}</a>
  ),
}))

jest.mock('../lib/iocChatClient', () => ({
  postIocChat: jest.fn(),
}))

const postMock = postIocChat as jest.MockedFunction<typeof postIocChat>

function renderPanel() {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <IocChatPanel iocId={7} />
    </QueryClientProvider>,
  )
}

function openWidget() {
  fireEvent.click(
    screen.getByRole('button', { name: 'Open investigation chat' }),
  )
}

describe('IocChatPanel', () => {
  beforeEach(() => {
    jest.resetAllMocks()
  })

  afterEach(cleanup)

  it('opens the conversation in a dismissible support-style chat window', () => {
    renderPanel()

    expect(screen.queryByRole('dialog')).toBeNull()
    openWidget()
    expect(
      screen.getByRole('dialog', { name: 'IOC investigation chat' }),
    ).toBeTruthy()
    expect(
      screen.getByRole('button', { name: 'Close investigation chat' }),
    ).toBeTruthy()
    expect(screen.getByRole('log', { name: 'Conversation' })).toBeTruthy()

    fireEvent.keyDown(window, { key: 'Escape' })
    expect(screen.queryByRole('dialog')).toBeNull()
    // Closing hands the user back to the corner launcher.
    expect(
      screen.getByRole('button', { name: 'Open investigation chat' }),
    ).toBeTruthy()
  })

  it('keeps the conversation in component memory and renders cited IOC links safely', async () => {
    postMock.mockResolvedValue({
      answer: 'Fact: IOC #7 is related. Evidence: #7',
      iocIds: [7],
      providerRecordIds: ['record-7'],
      hasAssessment: false,
    })
    renderPanel()
    openWidget()

    fireEvent.change(screen.getByLabelText('Ask about this IOC'), {
      target: { value: 'Compare evidence' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Ask' }))

    await screen.findByText(/IOC #7 is related/)
    expect(screen.getByRole('link', { name: '#7' })).toBeTruthy()
    expect(postMock).toHaveBeenCalledWith(
      7,
      'Compare evidence',
      [],
      expect.any(AbortSignal),
    )

    cleanup()
    renderPanel()
    openWidget()
    // A fresh mount starts with the greeting and no prior transcript.
    expect(screen.queryByText('Compare evidence')).toBeNull()
    expect(screen.getByText(/This conversation is not saved/)).toBeTruthy()
  })

  it('sends on Enter and offers starter questions before the first question', async () => {
    postMock.mockResolvedValue({
      answer: 'Provider summary',
      iocIds: [],
      providerRecordIds: [],
      hasAssessment: true,
    })
    renderPanel()
    openWidget()

    const starter = 'Which providers report this value?'
    fireEvent.click(screen.getByRole('button', { name: starter }))
    const input =
      screen.getByLabelText<HTMLTextAreaElement>('Ask about this IOC')
    expect(input.value).toBe(starter)

    fireEvent.keyDown(input, { key: 'Enter' })
    await waitFor(() =>
      expect(postMock).toHaveBeenCalledWith(
        7,
        starter,
        [],
        expect.any(AbortSignal),
      ),
    )

    await screen.findByText('Provider summary')
    expect(screen.getByText('Assistant · includes assessment')).toBeTruthy()
    // Starter questions stand in for the empty state, so they retire once asked.
    expect(screen.queryByRole('button', { name: starter })).toBeNull()
  })

  it('shows a safe API error and does not expose the request history as persistence', async () => {
    postMock.mockRejectedValue(new Error('temporary failure'))
    renderPanel()
    openWidget()

    fireEvent.change(screen.getByLabelText('Ask about this IOC'), {
      target: { value: 'Question' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Ask' }))

    await screen.findByRole('alert')
    expect(screen.getByRole('alert').textContent).toContain(
      'could not be completed',
    )
  })
})
