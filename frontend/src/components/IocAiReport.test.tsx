/**
 * @jest-environment jsdom
 */
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { IocAiReport } from './IocAiReport'
import { renderSafeMarkdown } from './safeMarkdown'
import { AiReportProblemError, streamIocAiReport } from '../lib/aiReportClient'
import { useAuthStore } from '../lib/authStore'
import { createSessionStorageStub, makeSession } from '../test/fixtures'

jest.mock('../lib/aiReportClient', () => {
  const actual = jest.requireActual('../lib/aiReportClient')
  return { ...actual, streamIocAiReport: jest.fn() }
})

const streamMock = streamIocAiReport as jest.MockedFunction<
  typeof streamIocAiReport
>

const validReport = [
  '## Yönetici Özeti',
  'Bu bir **doğrulanmış** gözlemdir [S1].',
  '## Teknik Analiz',
  'Analiz.',
].join('\n')

function renderReport() {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <IocAiReport iocId={7} />
    </QueryClientProvider>,
  )
}

describe('IocAiReport', () => {
  beforeEach(() => {
    ;(globalThis as { sessionStorage?: Storage }).sessionStorage =
      createSessionStorageStub() as Storage
    useAuthStore.setState({ session: null })
    jest.resetAllMocks()
    jest.restoreAllMocks()
  })

  afterEach(cleanup)

  it('allows anonymous visitors to generate a report', async () => {
    streamMock.mockImplementation(async (_iocId, handlers) => {
      handlers.onComplete?.({
        type: 'complete',
        report: validReport,
        modelId: 'public-model',
        policyVersion: 1,
        generatedAtUtc: '2026-09-17T10:00:00Z',
      })
    })

    renderReport()

    fireEvent.click(screen.getByText('Generate report'))
    await screen.findByText('Instruction version: 1')
    expect(screen.queryByText('Model: public-model')).toBeNull()
    expect(streamMock).toHaveBeenCalledWith(
      7,
      expect.anything(),
      expect.anything(),
    )
  })

  it('starts streaming immediately when opened from the AI workspace action', async () => {
    streamMock.mockImplementation(async (_iocId, handlers) => {
      handlers.onDelta?.('Streaming now')
    })

    render(
      <QueryClientProvider client={new QueryClient()}>
        <IocAiReport iocId={7} autoGenerate onClose={jest.fn()} />
      </QueryClientProvider>,
    )

    await screen.findByRole('status')
    expect(screen.queryByText('Streaming now')).toBeNull()
    expect(screen.getByRole('progressbar').getAttribute('aria-valuenow')).toBe(
      '0',
    )
    expect(streamMock).toHaveBeenCalledWith(
      7,
      expect.anything(),
      expect.anything(),
    )
    expect(screen.getByLabelText('Close AI report')).toBeTruthy()
  })

  it('streams provisional plain text, then renders the validated report with metadata and download', async () => {
    useAuthStore.setState({ session: makeSession({ role: 'User' }) })
    streamMock.mockImplementation(async (_iocId, handlers) => {
      handlers.onMeta?.({
        type: 'meta',
        iocId: 7,
        modelId: 'test-model',
        policyVersion: 3,
        sourceCount: 2,
        omittedSourceCount: 0,
      })
      handlers.onDelta?.('## Yönetici Özeti')
      handlers.onDelta?.('Şu an yarı yazı…')
      handlers.onComplete?.({
        type: 'complete',
        report: validReport,
        modelId: 'test-model',
        policyVersion: 3,
        generatedAtUtc: '2026-09-17T10:00:00Z',
      })
    })

    renderReport()

    fireEvent.click(screen.getByText('Generate report'))

    expect(screen.getByText(/Instruction version: 3/)).toBeTruthy()
    expect(screen.queryByText('Model: test-model')).toBeNull()
    expect(screen.getByText('doğrulanmış')).toBeTruthy()
    expect(screen.getByText('Download report')).toBeTruthy()
  })

  it('shows an accessible failure state and a retry button after a failed stream', async () => {
    useAuthStore.setState({ session: makeSession() })
    streamMock.mockRejectedValue(
      new AiReportProblemError(
        409,
        'ai_disabled',
        'AI report unavailable',
        'AI report generation is disabled.',
        'cid-9',
      ),
    )

    renderReport()
    fireEvent.click(screen.getByText('Generate report'))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('AI report generation is disabled.')
    expect(alert.textContent).toContain('cid-9')
    expect(screen.getByText('Retry report')).toBeTruthy()
  })

  it('keeps partial output out of the UI while streaming and supports cancel', async () => {
    useAuthStore.setState({ session: makeSession() })
    streamMock.mockImplementation(async (_iocId, handlers, signal) => {
      handlers.onMeta?.({
        type: 'meta',
        iocId: 7,
        modelId: 'test-model',
        policyVersion: 1,
        sourceCount: 1,
        omittedSourceCount: 0,
      })
      handlers.onDelta?.('<script>alert(1)</script> [x](javascript:alert(1))')
      await new Promise((resolve, reject) => {
        const timer = setTimeout(resolve, 5000)
        signal?.addEventListener('abort', () => {
          clearTimeout(timer)
          reject(new DOMException('The operation was aborted.', 'AbortError'))
        })
      })
      handlers.onDelta?.('more')
      handlers.onComplete?.({
        type: 'complete',
        report: validReport,
        modelId: 'test-model',
        policyVersion: 1,
        generatedAtUtc: '2026-09-17T10:00:00Z',
      })
    })

    renderReport()
    fireEvent.click(screen.getByText('Generate report'))

    await screen.findByRole('status')
    expect(screen.queryByText(/alert\(1\)/)).toBeNull()
    // No streamed content is mounted, so no script element can exist in the document.
    expect(document.querySelector('script')).toBeNull()

    fireEvent.click(screen.getByLabelText('Cancel report generation'))
    // After abort the stream promise rejects with AbortError and the panel returns to idle.
    await screen.findByText(/No report has been generated/i)
  })

  it('renders a malicious markdown fixture without an executable link or HTML node', () => {
    const malicious = [
      '## Feragatname <img src=x onerror=alert(1)>',
      '- [click](javascript:alert(1)) <script>alert(1)</script>',
    ].join('\n')

    render(<div>{renderSafeMarkdown(malicious)}</div>)

    expect(document.querySelector('a')).toBeNull()
    expect(document.querySelector('script')).toBeNull()
    expect(document.querySelector('img')).toBeNull()
    // The link text survives as plain text with its target stripped.
    expect(screen.getByText(/click/)).toBeTruthy()
  })

  it('downloads the completed report as a local markdown file', async () => {
    useAuthStore.setState({ session: makeSession() })
    streamMock.mockImplementation(async (_iocId, handlers) => {
      handlers.onComplete?.({
        type: 'complete',
        report: validReport,
        modelId: 'test-model',
        policyVersion: 3,
        generatedAtUtc: '2026-09-17T10:00:00Z',
      })
    })

    const createObjectURL = jest.fn(() => 'blob:download')
    const revokeObjectURL = jest.fn()
    const originalCreateObjectURL = URL.createObjectURL
    const originalRevokeObjectURL = URL.revokeObjectURL
    URL.createObjectURL =
      createObjectURL as unknown as typeof URL.createObjectURL
    URL.revokeObjectURL =
      revokeObjectURL as unknown as typeof URL.revokeObjectURL
    const clickedAnchors: { download: string | null }[] = []
    const originalClick = HTMLAnchorElement.prototype.click
    HTMLAnchorElement.prototype.click = function click() {
      clickedAnchors.push(this)
    }

    try {
      renderReport()
      fireEvent.click(screen.getByText('Generate report'))
      fireEvent.click(await screen.findByText('Download report'))

      expect(createObjectURL).toHaveBeenCalled()
      expect(clickedAnchors[0]?.download).toBe('ioc-report-7.md')
      expect(revokeObjectURL).toHaveBeenCalledWith('blob:download')
    } finally {
      URL.createObjectURL = originalCreateObjectURL
      URL.revokeObjectURL = originalRevokeObjectURL
      HTMLAnchorElement.prototype.click = originalClick
    }
  })

  it('discards the report when the panel remounts (no persisted state)', async () => {
    useAuthStore.setState({ session: makeSession() })
    streamMock.mockImplementation(async (_iocId, handlers) => {
      handlers.onComplete?.({
        type: 'complete',
        report: validReport,
        modelId: 'test-model',
        policyVersion: 3,
        generatedAtUtc: '2026-09-17T10:00:00Z',
      })
    })

    const first = renderReport()
    fireEvent.click(screen.getByText('Generate report'))
    await screen.findByText(/Instruction version: 3/)
    first.unmount()

    const second = render(
      <QueryClientProvider client={new QueryClient()}>
        <IocAiReport iocId={7} />
      </QueryClientProvider>,
    )
    expect(second.container.textContent).toContain(
      'No report has been generated',
    )
    second.unmount()
  })
})
