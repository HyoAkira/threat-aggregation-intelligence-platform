/**
 * @jest-environment jsdom
 */
import {
  downloadIocAiReportPdf,
  streamIocAiReport,
  parseFrame,
  AiReportProblemError,
} from './aiReportClient'
import { useAuthStore } from './authStore'
import { createSessionStorageStub, makeSession } from '../test/fixtures'

const encoder = new TextEncoder()

/** A minimal Response-like object whose body replays an SSE payload in small chunks. */
function sseResponse(frames: string): {
  ok: boolean
  status: number
  headers: { get: (name: string) => string }
  body: {
    getReader: () => {
      read: () => Promise<{ done: boolean; value?: Uint8Array }>
    }
  }
} {
  const chunks: Uint8Array[] = []
  for (let index = 0; index < frames.length; index += 17) {
    chunks.push(encoder.encode(frames.slice(index, index + 17)))
  }
  return {
    ok: true,
    status: 200,
    headers: {
      get: (name) =>
        name === 'Content-Type'
          ? 'text/event-stream'
          : (null as unknown as string),
    },
    body: {
      getReader: () => {
        let position = 0
        return {
          read: async () =>
            position < chunks.length
              ? { done: false, value: chunks[position++] }
              : { done: true },
        }
      },
    },
  }
}

const metaFrame =
  'event: meta\ndata: {"iocId":7,"modelId":"test-model","policyVersion":3,"sourceCount":2,"omittedSourceCount":0}\n\n'
const deltaFrame = 'event: delta\ndata: {"text":"Özet"}\n\n'
const completeFrame =
  'event: complete\ndata: {"report":"## Yönetici Özeti\\nTam rapor.","modelId":"test-model","policyVersion":3,"generatedAtUtc":"2026-09-17T10:00:00Z"}\n\n'
const errorFrame =
  'event: error\ndata: {"code":"ai_upstream_failure","message":"The model server request failed."}\n\n'

describe('streamIocAiReport', () => {
  beforeEach(() => {
    ;(globalThis as { sessionStorage?: Storage }).sessionStorage =
      createSessionStorageStub() as Storage
    useAuthStore.setState({ session: null })
  })

  it('sends the bearer token and parses split SSE frames to meta/delta/complete', async () => {
    useAuthStore.setState({ session: makeSession() })
    const fetchMock = jest
      .fn()
      .mockResolvedValue(sseResponse(metaFrame + deltaFrame + completeFrame))
    globalThis.fetch = fetchMock

    const events: string[] = []
    await streamIocAiReport(7, {
      onMeta: (event) =>
        events.push(`meta:${event.modelId}:${event.policyVersion}`),
      onDelta: (text) => events.push(`delta:${text}`),
      onComplete: (event) => events.push(`complete:${event.report}`),
    })

    expect(fetchMock.mock.calls[0][0]).toBe('/api/iocs/7/ai/reports')
    const init = fetchMock.mock.calls[0][1] as RequestInit
    expect(init.method).toBe('POST')
    expect((init.headers as Record<string, string>).Authorization).toMatch(
      /^Bearer /,
    )
    expect(init.body).toBeUndefined()
    expect(
      (init.headers as Record<string, string>)['Content-Type'],
    ).toBeUndefined()
    expect(events).toEqual([
      'meta:test-model:3',
      'delta:Özet',
      'complete:## Yönetici Özeti\nTam rapor.',
    ])
  })

  it('handles a pre-stream ProblemDetails failure', async () => {
    useAuthStore.setState({ session: makeSession() })
    globalThis.fetch = jest.fn().mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({
        status: 409,
        code: 'ai_disabled',
        title: 'AI report unavailable',
        detail: 'AI report generation is disabled.',
        correlationId: 'cid-1',
      }),
    })

    const failure = await streamIocAiReport(7, {}).catch(
      (error: unknown) => error,
    )

    expect(failure).toBeInstanceOf(AiReportProblemError)
    const problem = failure as AiReportProblemError
    expect(problem.status).toBe(409)
    expect(problem.code).toBe('ai_disabled')
    expect(problem.correlationId).toBe('cid-1')
  })

  it('clears the session on a 401 pre-stream response', async () => {
    useAuthStore.setState({ session: makeSession() })
    globalThis.fetch = jest.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({}),
    })

    await expect(streamIocAiReport(7, {})).rejects.toMatchObject({
      status: 401,
    })
    expect(useAuthStore.getState().session).toBeNull()
  })

  it('surfaces an error event and never calls onComplete for it', async () => {
    useAuthStore.setState({ session: makeSession() })
    globalThis.fetch = jest
      .fn()
      .mockResolvedValue(sseResponse(deltaFrame + errorFrame))

    const received: string[] = []
    let completeCalled = false
    await streamIocAiReport(7, {
      onDelta: (text) => received.push(`delta:${text}`),
      onError: (event) => received.push(`error:${event.code}`),
      onComplete: () => {
        completeCalled = true
      },
    })

    expect(received).toEqual(['delta:Özet', 'error:ai_upstream_failure'])
    expect(completeCalled).toBe(false)
  })

  it('aborts through the AbortController without resolving', async () => {
    useAuthStore.setState({ session: makeSession() })
    const controller = new AbortController()
    globalThis.fetch = jest.fn().mockImplementation(
      (_path: string, init: RequestInit) =>
        new Promise<Response>((_resolve, reject) => {
          const signal = init.signal as AbortSignal
          signal?.addEventListener('abort', () =>
            reject(
              new DOMException('The operation was aborted.', 'AbortError'),
            ),
          )
        }),
    )

    const pending = streamIocAiReport(7, {}, controller.signal)
    controller.abort()

    await expect(pending).rejects.toMatchObject({ name: 'AbortError' })
  })

  it('treats an unknown event type as a protocol error', () => {
    expect(() => parseFrame('event: vendor-specific\ndata: {"x":1}\n')).toThrow(
      AiReportProblemError,
    )
  })

  it('parses an isolated meta frame', () => {
    const event = parseFrame(metaFrame)
    expect(event).toEqual({
      type: 'meta',
      iocId: 7,
      modelId: 'test-model',
      policyVersion: 3,
      sourceCount: 2,
      omittedSourceCount: 0,
    })
  })
})

describe('downloadIocAiReportPdf', () => {
  beforeEach(() => {
    ;(globalThis as { sessionStorage?: Storage }).sessionStorage =
      createSessionStorageStub() as Storage
    useAuthStore.setState({ session: null })
  })

  it('posts the completed report and returns the PDF blob', async () => {
    const pdf = new Blob(['%PDF-1.7'], { type: 'application/pdf' })
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      status: 200,
      blob: async () => pdf,
    })
    globalThis.fetch = fetchMock

    await expect(
      downloadIocAiReportPdf(7, '## Report', '2026-09-17T10:00:00Z'),
    ).resolves.toBe(pdf)
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/iocs/7/ai/reports/pdf',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          report: '## Report',
          generatedAtUtc: '2026-09-17T10:00:00Z',
        }),
      }),
    )
  })
})
