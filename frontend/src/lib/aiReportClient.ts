import { clearAllSessionData, useAuthStore } from './authStore'
import { isSessionExpired } from './session'

/** Backend-owned SSE event names for the IOC AI report stream. */
export type AiReportEvent =
  | {
      type: 'meta'
      iocId: number
      modelId: string
      policyVersion: number
      sourceCount: number
      omittedSourceCount: number
    }
  | { type: 'delta'; text: string }
  | {
      type: 'complete'
      report: string
      modelId: string
      policyVersion: number
      generatedAtUtc: string
    }
  | { type: 'error'; code: string; message: string }

export class AiReportProblemError extends Error {
  readonly status: number
  readonly code: string
  readonly title: string
  readonly correlationId: string

  constructor(
    status: number,
    code: string,
    title: string,
    message: string,
    correlationId: string,
  ) {
    super(message)
    this.name = 'AiReportProblemError'
    this.status = status
    this.code = code
    this.title = title
    this.correlationId = correlationId
  }
}

type ProblemDetails = {
  status?: number
  code?: string
  title?: string
  detail?: string
  correlationId?: string
}

export type ReportStreamHandlers = {
  onMeta?: (event: Extract<AiReportEvent, { type: 'meta' }>) => void
  onDelta?: (text: string) => void
  onComplete?: (event: Extract<AiReportEvent, { type: 'complete' }>) => void
  onError?: (event: Extract<AiReportEvent, { type: 'error' }>) => void
}

/**
 * Same-origin streaming transport for `POST /api/iocs/{id}/ai/reports`. Sends only the IOC id in
 * the URL plus the Bearer token; the request has no body. The server resolves all evidence and
 * report instructions. This parses split SSE frames of the backend-owned event contract, surfaces
 * pre-stream ProblemDetails (including 401 session loss) as thrown errors, and supports
 * AbortController cancellation. The browser never parses vendor-specific upstream chunks.
 */
export async function streamIocAiReport(
  iocId: number,
  handlers: ReportStreamHandlers,
  signal?: AbortSignal,
): Promise<void> {
  const storedSession = useAuthStore.getState().session
  if (storedSession && isSessionExpired(storedSession)) {
    clearAllSessionData()
    throw new AiReportProblemError(
      401,
      'session_expired',
      'Session expired',
      'Your session has expired. Please sign in again.',
      'local',
    )
  }

  const headers: Record<string, string> = { Accept: 'text/event-stream' }
  if (storedSession) headers.Authorization = `Bearer ${storedSession.token}`

  let response: Response
  try {
    response = await fetch(`/api/iocs/${iocId}/ai/reports`, {
      method: 'POST',
      headers,
      signal,
    })
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError')
      throw cause
    throw new AiReportProblemError(
      0,
      'network_error',
      'Network error',
      'The service could not be reached. Please try again.',
      'local',
    )
  }

  if (!response.ok) {
    let problem: ProblemDetails = {}
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      // Non-JSON failure body; fall back to the status code below.
    }
    if (response.status === 401) clearAllSessionData()
    throw new AiReportProblemError(
      response.status,
      problem.code ?? 'request_failed',
      problem.title ?? 'Report generation failed',
      problem.detail ?? 'The report request could not be completed.',
      problem.correlationId ?? 'unknown',
    )
  }

  if (!response.body) {
    throw new AiReportProblemError(
      0,
      'unsupported_stream',
      'Streaming unsupported',
      'The browser does not expose a readable stream for this response.',
      'local',
    )
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let done = false

  while (!done) {
    let chunk: { done: boolean; value?: Uint8Array }
    try {
      chunk = await reader.read()
    } catch (cause) {
      if (cause instanceof DOMException && cause.name === 'AbortError')
        throw cause
      throw new AiReportProblemError(
        0,
        'network_error',
        'Stream interrupted',
        'The report stream was interrupted before it completed.',
        'local',
      )
    }

    if (chunk.done) break

    buffer += decoder.decode(chunk.value, { stream: true })

    // SSE frames are separated by a blank line; events may be split across chunks.
    let separator = buffer.indexOf('\n\n')
    while (separator !== -1) {
      const frame = buffer.slice(0, separator)
      buffer = buffer.slice(separator + 2)
      const event = parseFrame(frame)
      if (event) {
        dispatch(event, handlers)
        if (event.type === 'complete' || event.type === 'error') {
          done = true
          break
        }
      }
      separator = buffer.indexOf('\n\n')
    }
  }
}

export function parseFrame(frame: string): AiReportEvent | null {
  let eventName: string | null = null
  const dataLines: string[] = []
  for (const rawLine of frame.split('\n')) {
    const line = rawLine.replace(/\r$/, '')
    if (line.startsWith('event:')) {
      eventName = line.slice(6).trim()
    } else if (line.startsWith('data:')) {
      dataLines.push(line.slice(5).trimStart())
    }
  }

  if (eventName === null || dataLines.length === 0) return null
  let payload: Record<string, unknown>
  try {
    payload = JSON.parse(dataLines.join('\n')) as Record<string, unknown>
  } catch {
    throw new AiReportProblemError(
      0,
      'malformed_event',
      'Protocol error',
      'The report stream contained an event that could not be parsed.',
      'local',
    )
  }

  switch (eventName) {
    case 'meta':
      return {
        type: 'meta',
        iocId: Number(payload.iocId),
        modelId: String(payload.modelId ?? ''),
        policyVersion: Number(payload.policyVersion),
        sourceCount: Number(payload.sourceCount ?? 0),
        omittedSourceCount: Number(payload.omittedSourceCount ?? 0),
      }
    case 'delta':
      return { type: 'delta', text: String(payload.text ?? '') }
    case 'complete':
      return {
        type: 'complete',
        report: String(payload.report ?? ''),
        modelId: String(payload.modelId ?? ''),
        policyVersion: Number(payload.policyVersion ?? 0),
        generatedAtUtc: String(payload.generatedAtUtc ?? ''),
      }
    case 'error':
      return {
        type: 'error',
        code: String(payload.code ?? 'unknown_error'),
        message: String(payload.message ?? 'The report generation failed.'),
      }
    default:
      throw new AiReportProblemError(
        0,
        'malformed_event',
        'Protocol error',
        'The report stream contained an unknown event type.',
        'local',
      )
  }
}

function dispatch(event: AiReportEvent, handlers: ReportStreamHandlers): void {
  switch (event.type) {
    case 'meta':
      handlers.onMeta?.(event)
      break
    case 'delta':
      handlers.onDelta?.(event.text)
      break
    case 'complete':
      handlers.onComplete?.(event)
      break
    case 'error':
      handlers.onError?.(event)
      break
  }
}
