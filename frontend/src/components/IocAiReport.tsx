import { useEffect, useRef, useState } from 'react'
import {
  AiReportProblemError,
  downloadIocAiReportPdf,
  streamIocAiReport,
} from '../lib/aiReportClient'
import { formatLocalDateTime } from '../lib/format'
import { renderSafeMarkdown } from './safeMarkdown'

type ReportMeta = {
  policyVersion: number
  generatedAtUtc: string
}

type Phase = 'idle' | 'streaming' | 'complete' | 'failed'

const loadingQuotes = [
  'Why do APT actors love playing hide and seek? Because every time you spot them, they just burn their command-and-control server and start living off the land!',
  'Why was the IOC analyst always calm during a crisis? They knew how to keep their hashes low and their entropy steady.',
  'How do threat actors like their steak prepared? Zero-day cooked, extra raw, and served with a side of unpatched vulnerabilities.',
  'Why did the threat hunter refuse to go into the woods? Too many lateral movements in the bushes and not enough YARA rules to clear the area!',
  'What is a ransomware operator’s favorite classical music piece? The Encrypted Symphony in .locked minor.',
  'Why did the CTI analyst get dumped by their partner? They kept assigning a Medium-High Confidence rating to the relationship status without enough supporting telemetry!',
  'How does a threat intelligence analyst order at a restaurant? “I’d like a salad, but first, can you provide the TTPs for how the kitchen prepared it?”',
  'Why don’t phishing campaigns ever go to party venues? Because as soon as someone notices the domain looks fishy, the whole event gets DNS sinkholed!',
  'What did the analyst say when they found duplicate indicators in their threat feed? “Looks like we have an active case of re-hash-ing old news!”',
  'Why did the botnet cross the road? To perform a distributed denial-of-service attack on the chicken coop!',
]

function randomLoadingQuote(): string {
  return loadingQuotes[Math.floor(Math.random() * loadingQuotes.length)]
}

/**
 * On-demand, ephemeral AI report panel for the IOC detail page. Anonymous and signed-in users
 * receive a sanitized Markdown report after final validation, with a local download. The report
 * is never stored and incremental upstream output is never displayed.
 */
export function IocAiReport({
  iocId,
  autoGenerate = false,
  onClose,
}: {
  iocId: number
  /** Start a fresh analysis as soon as the report workspace opens. */
  autoGenerate?: boolean
  /** Closes the report workspace; any active stream is cancelled first. */
  onClose?: () => void
}) {
  const [phase, setPhase] = useState<Phase>('idle')
  const [report, setReport] = useState<string | null>(null)
  const [meta, setMeta] = useState<ReportMeta | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [errorCorrelationId, setErrorCorrelationId] = useState<string | null>(
    null,
  )
  const [errorStatus, setErrorStatus] = useState<number | null>(null)
  const [pdfErrorMessage, setPdfErrorMessage] = useState<string | null>(null)
  const [pdfDownloading, setPdfDownloading] = useState(false)
  const [loadingQuote, setLoadingQuote] = useState(randomLoadingQuote)
  const [analysisProgress, setAnalysisProgress] = useState(0)
  const abortControllerRef = useRef<AbortController | null>(null)
  const hasStartedRef = useRef(false)
  const sessionExpired = phase === 'failed' && errorStatus === 401

  const generate = () => {
    setPhase('streaming')
    setAnalysisProgress(0)
    setLoadingQuote(randomLoadingQuote())
    setReport(null)
    setErrorMessage(null)
    setErrorCorrelationId(null)
    setErrorStatus(null)
    setPdfErrorMessage(null)

    const controller = new AbortController()
    abortControllerRef.current = controller
    void streamIocAiReport(
      iocId,
      {
        onMeta: (event) => {
          setMeta({
            policyVersion: event.policyVersion,
            generatedAtUtc: '',
          })
        },
        // The stream is deliberately consumed for the terminal validated report only.
        onDelta: () => undefined,
        onComplete: (event) => {
          setAnalysisProgress(100)
          setReport(event.report)
          setMeta({
            policyVersion: event.policyVersion,
            generatedAtUtc: event.generatedAtUtc,
          })
          setPhase('complete')
        },
        onError: (event) => {
          setErrorMessage(event.message)
          setPhase('failed')
        },
      },
      controller.signal,
    ).catch((error: unknown) => {
      if (error instanceof DOMException && error.name === 'AbortError') {
        setPhase('idle')
        return
      }
      if (error instanceof AiReportProblemError) {
        setErrorMessage(error.message)
        setErrorCorrelationId(error.correlationId)
        setErrorStatus(error.status)
      } else {
        setErrorMessage('The report generation failed.')
      }
      setPhase('failed')
    })
  }

  const cancel = () => {
    abortControllerRef.current?.abort()
  }

  const close = () => {
    cancel()
    onClose?.()
  }

  useEffect(() => {
    if (!autoGenerate || hasStartedRef.current) return
    hasStartedRef.current = true
    generate()
  }, [autoGenerate])

  useEffect(
    () => () => {
      abortControllerRef.current?.abort()
    },
    [],
  )

  useEffect(() => {
    if (phase !== 'streaming') return

    const timer = window.setInterval(() => {
      setAnalysisProgress((current) => {
        if (current >= 95) return current
        const increment = current < 50 ? 7 : current < 80 ? 3 : 1
        return Math.min(95, current + increment)
      })
    }, 650)
    return () => window.clearInterval(timer)
  }, [phase])

  const download = () => {
    if (!report) return
    const blob = new Blob([report], { type: 'text/markdown;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `ioc-report-${iocId}.md`
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    URL.revokeObjectURL(url)
  }

  const downloadPdf = async () => {
    if (!report || !meta || pdfDownloading) return
    setPdfDownloading(true)
    setPdfErrorMessage(null)
    try {
      const pdf = await downloadIocAiReportPdf(
        iocId,
        report,
        meta.generatedAtUtc,
      )
      const url = URL.createObjectURL(pdf)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = `ioc-report-${iocId}.pdf`
      document.body.appendChild(anchor)
      anchor.click()
      anchor.remove()
      URL.revokeObjectURL(url)
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      setPdfErrorMessage(
        error instanceof AiReportProblemError
          ? error.message
          : 'The report could not be converted to PDF.',
      )
    } finally {
      setPdfDownloading(false)
    }
  }

  return (
    <section
      className="flex h-full min-h-0 flex-col overflow-hidden rounded border border-outline-variant bg-surface-container-lowest"
      aria-labelledby="ai-report-heading"
    >
      <div className="flex shrink-0 flex-wrap items-start justify-between gap-3 border-b border-outline-variant/60 bg-surface-container-lowest px-4 py-3">
        <div className="min-w-max">
          <div className="flex items-center gap-2">
            <span
              aria-hidden="true"
              className="material-symbols-outlined text-[18px] text-primary"
            >
              auto_awesome
            </span>
            <h2
              id="ai-report-heading"
              className="text-base font-semibold text-on-surface"
            >
              AI report
            </h2>
          </div>
        </div>
        <div className="ml-auto flex max-w-full shrink-0 flex-wrap items-center justify-end gap-1.5">
          {onClose ? (
            <button
              type="button"
              onClick={close}
              aria-label="Close AI report"
              title="Close AI report"
              className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded p-1 text-on-surface-variant transition-colors hover:bg-surface-container hover:text-on-surface focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
            >
              <span
                aria-hidden="true"
                className="material-symbols-outlined text-[18px]"
              >
                close
              </span>
            </button>
          ) : null}
          {phase === 'idle' || phase === 'failed' ? (
            <button
              type="button"
              onClick={generate}
              aria-label={
                phase === 'failed' ? 'Retry report' : 'Generate report'
              }
              title={phase === 'failed' ? 'Retry report' : 'Generate report'}
              className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded bg-primary text-on-primary transition-colors hover:bg-primary-container focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
            >
              <span
                aria-hidden="true"
                className="material-symbols-outlined text-[18px]"
              >
                {phase === 'failed' ? 'refresh' : 'auto_awesome'}
              </span>
              <span className="sr-only">
                {phase === 'failed' ? 'Retry report' : 'Generate report'}
              </span>
            </button>
          ) : null}
          {phase === 'complete' && report !== null ? (
            <>
              <button
                type="button"
                onClick={download}
                aria-label="Download Markdown"
                title="Download Markdown"
                className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded border border-outline-variant text-on-surface transition-colors hover:bg-surface-container hover:text-on-surface focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
              >
                <span
                  aria-hidden="true"
                  className="material-symbols-outlined text-[18px]"
                >
                  description
                </span>
                <span className="sr-only">Download Markdown</span>
              </button>
              <button
                type="button"
                onClick={() => void downloadPdf()}
                disabled={pdfDownloading}
                aria-label="Download PDF"
                title="Download PDF"
                className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded border border-primary text-primary transition-colors hover:bg-primary/5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary disabled:cursor-wait disabled:opacity-60"
              >
                <span
                  aria-hidden="true"
                  className="material-symbols-outlined text-[18px]"
                >
                  picture_as_pdf
                </span>
                <span className="sr-only">
                  {pdfDownloading ? 'Preparing PDF…' : 'Download PDF'}
                </span>
              </button>
            </>
          ) : null}
        </div>
      </div>

      <div
        className="min-h-0 flex-1 overflow-y-auto p-4"
        role="region"
        aria-label="AI report content"
      >
        {phase === 'streaming' ? (
          <div role="status" aria-live="polite">
            <div className="rounded border border-primary/20 bg-primary/5 px-3 py-3">
              <div className="flex items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <span
                    aria-hidden="true"
                    className="material-symbols-outlined text-[18px] text-primary"
                  >
                    security
                  </span>
                  <p className="font-mono text-[10px] font-semibold tracking-wide text-on-surface">
                    Evidence correlation in progress
                  </p>
                </div>
                <span className="font-mono text-[10px] text-primary">LIVE</span>
              </div>
              <div className="mt-3 flex items-center justify-between gap-3 font-mono text-[10px] text-on-surface-variant">
                <span>Analysis progress (estimated)</span>
                <span className="font-semibold text-primary">
                  {analysisProgress}%
                </span>
              </div>
              <div
                role="progressbar"
                aria-label="Estimated analysis progress"
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={analysisProgress}
                className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-primary/15"
              >
                <div
                  className="h-full rounded-full bg-primary transition-[width] duration-500 ease-out"
                  style={{ width: `${analysisProgress}%` }}
                />
              </div>
              <blockquote className="mt-3 border-l-2 border-primary/35 pl-3 text-sm leading-5 text-on-surface-variant">
                {loadingQuote}
              </blockquote>
            </div>
          </div>
        ) : null}

        {phase === 'failed' ? (
          <div
            role="alert"
            className="rounded border border-error/40 bg-error-container px-3 py-2 text-sm text-on-error-container"
          >
            <span className="font-semibold">Report generation failed.</span>{' '}
            {errorMessage ?? 'The run could not be completed.'}
            {sessionExpired ? ' Your session has ended; sign in again.' : null}
            {errorCorrelationId && errorCorrelationId !== 'local' ? (
              <div className="mt-1 font-mono text-[10px]">
                Correlation ID: {errorCorrelationId}
              </div>
            ) : null}
          </div>
        ) : null}

        {pdfErrorMessage ? (
          <div
            role="alert"
            className="mb-3 rounded border border-error/40 bg-error-container px-3 py-2 text-sm text-on-error-container"
          >
            PDF export failed. {pdfErrorMessage}
          </div>
        ) : null}

        {phase === 'idle' ? (
          <p className="text-sm text-on-surface-variant">
            No report has been generated in this session. A new run can be
            started any time; reloading the page discards any previous output.
          </p>
        ) : null}

        {phase === 'complete' && report !== null && meta ? (
          <div>
            <div className="mb-2 flex flex-wrap items-center gap-x-4 gap-y-1 font-mono text-[10px] text-on-surface-variant">
              <span>Analysis complete: 100%</span>
              <span>Instruction version: {meta.policyVersion}</span>
              <span>
                Generated:{' '}
                {formatLocalDateTime(meta.generatedAtUtc) ??
                  meta.generatedAtUtc}
              </span>
            </div>
            <div className="space-y-2 text-sm leading-6 text-on-surface">
              {renderSafeMarkdown(report)}
            </div>
          </div>
        ) : null}
      </div>
    </section>
  )
}
