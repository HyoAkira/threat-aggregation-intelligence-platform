import { useEffect, useRef, useState } from 'react'
import { Link } from '@tanstack/react-router'
import { ApiError } from '../lib/apiErrors'
import { postIocChat } from '../lib/iocChatClient'
import type { IocChatTurn } from '../lib/iocChatClient'
import { renderSafeMarkdown } from './safeMarkdown'
import { focusRingClass } from './AppShell'

type Message = IocChatTurn & {
  iocIds?: number[]
  providerRecordIds?: string[]
  hasAssessment?: boolean
}

const iconClass = 'material-symbols-outlined'

/** Starter questions offered in the greeting, mirroring a support widget. */
const SUGGESTIONS = [
  'Summarise the public evidence for this indicator.',
  'Compare this indicator with related stored IOCs.',
  'Which providers report this value?',
]

function AssistantAvatar() {
  return (
    <span
      aria-hidden="true"
      className="mb-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary"
    >
      <span className={`${iconClass} text-[16px]`}>smart_toy</span>
    </span>
  )
}

/**
 * Support-style investigation chat: a round launcher pinned to the bottom-right
 * corner opens a docked conversation window with a status header, message
 * bubbles, and a composer. The transcript lives only in React memory, and
 * closing the window aborts any request still in flight.
 */
export function IocChatPanel({ iocId }: { iocId: number }) {
  const [messages, setMessages] = useState<Message[]>([])
  const [draft, setDraft] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isOpen, setIsOpen] = useState(false)
  const abortRef = useRef<AbortController | null>(null)
  const launcherRef = useRef<HTMLButtonElement | null>(null)
  const panelRef = useRef<HTMLElement | null>(null)
  const transcriptRef = useRef<HTMLDivElement | null>(null)
  const hasOpenedRef = useRef(false)

  const close = () => {
    abortRef.current?.abort()
    setIsOpen(false)
  }

  // The window behaves as a modal dialog, so keyboard focus stays inside it and
  // returns to the launcher when it closes.
  useEffect(() => {
    if (isOpen) {
      hasOpenedRef.current = true
      return undefined
    }
    if (hasOpenedRef.current) launcherRef.current?.focus()
    return undefined
  }, [isOpen])

  useEffect(() => {
    if (!isOpen) return undefined
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        close()
        return
      }
      if (event.key !== 'Tab') return
      const panel = panelRef.current
      if (!panel) return
      const focusable = panel.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled])',
      )
      if (focusable.length === 0) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      } else if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [isOpen])

  // Keep the newest message in view as the conversation grows.
  useEffect(() => {
    const transcript = transcriptRef.current
    if (!transcript) return
    transcript.scrollTop = transcript.scrollHeight
  }, [messages, busy, isOpen])

  const submit = async () => {
    const text = draft.trim()
    if (!text || busy) return
    setDraft('')
    setError(null)
    setBusy(true)
    const userMessage: Message = { role: 'user', content: text }
    const history = messages.map(({ role, content }) => ({ role, content }))
    setMessages((current) => [...current, userMessage])
    const controller = new AbortController()
    abortRef.current = controller
    try {
      const response = await postIocChat(
        iocId,
        text,
        history,
        controller.signal,
      )
      setMessages((current) => [
        ...current,
        {
          role: 'assistant',
          content: response.answer,
          iocIds: response.iocIds,
          providerRecordIds: response.providerRecordIds,
          hasAssessment: response.hasAssessment,
        },
      ])
    } catch (cause) {
      if (cause instanceof DOMException && cause.name === 'AbortError') return
      setError(
        cause instanceof ApiError
          ? cause.message
          : 'The IOC chat response could not be completed.',
      )
    } finally {
      abortRef.current = null
      setBusy(false)
    }
  }

  return (
    // display: contents keeps the widget out of the page's layout flow, so a
    // parent's spacing rules cannot nudge the fixed launcher or window.
    <div className="contents">
      {isOpen ? null : (
        <button
          ref={launcherRef}
          type="button"
          onClick={() => setIsOpen(true)}
          aria-label="Open investigation chat"
          className={`fixed bottom-5 right-5 z-40 flex h-14 w-14 items-center justify-center rounded-full bg-primary text-on-primary shadow-xl shadow-primary/25 transition-transform hover:scale-105 active:scale-95 ${focusRingClass}`}
        >
          <span aria-hidden="true" className={`${iconClass} text-[26px]`}>
            forum
          </span>
        </button>
      )}

      {isOpen ? (
        <>
          <div
            aria-hidden="true"
            onMouseDown={close}
            className="fixed inset-0 z-40 bg-scrim/40 sm:hidden"
          />
          <section
            ref={panelRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="ioc-chat-heading"
            className="ioc-chat-enter fixed inset-x-0 bottom-0 z-50 flex h-[min(40rem,100dvh)] flex-col overflow-hidden border border-outline-variant bg-surface-container-lowest shadow-2xl sm:inset-x-auto sm:bottom-5 sm:right-5 sm:h-[min(36rem,calc(100dvh_-_2.5rem))] sm:w-[23rem] sm:rounded-2xl"
          >
            <header className="flex items-center gap-3 bg-primary px-4 py-3 text-on-primary">
              <span
                aria-hidden="true"
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-on-primary/15"
              >
                <span className={`${iconClass} text-[20px]`}>shield</span>
              </span>
              <div className="min-w-0 flex-1">
                <h2
                  id="ioc-chat-heading"
                  className="truncate text-sm font-semibold leading-5"
                >
                  IOC investigation chat
                </h2>
                <p className="flex items-center gap-1.5 truncate text-[11px] leading-4 text-on-primary/80">
                  <span
                    aria-hidden="true"
                    className="h-1.5 w-1.5 shrink-0 rounded-full bg-tertiary-fixed"
                  />
                  Public evidence only · Not saved
                </p>
              </div>
              <button
                type="button"
                onClick={close}
                aria-label="Close investigation chat"
                className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-on-primary/90 transition-colors hover:bg-on-primary/15 ${focusRingClass}`}
              >
                <span aria-hidden="true" className={`${iconClass} text-[20px]`}>
                  close
                </span>
              </button>
            </header>

            <div
              ref={transcriptRef}
              role="log"
              aria-live="polite"
              aria-label="Conversation"
              className="flex-1 space-y-3 overflow-y-auto bg-surface-container-low px-4 py-4"
            >
              <div className="flex items-end gap-2">
                <AssistantAvatar />
                <div className="max-w-[85%] rounded-2xl rounded-bl-md border border-outline-variant/70 bg-surface-container-lowest px-3.5 py-2.5 text-sm text-on-surface shadow-sm">
                  <p className="leading-6">
                    Hi — I answer questions about this indicator using public
                    evidence only, and I can compare it with other stored IOCs.
                  </p>
                  <p className="mt-1 text-xs leading-5 text-on-surface-variant">
                    This conversation is not saved.
                  </p>
                  {messages.length === 0 ? (
                    <div className="mt-3 flex flex-wrap gap-2">
                      {SUGGESTIONS.map((suggestion) => (
                        <button
                          key={suggestion}
                          type="button"
                          onClick={() => setDraft(suggestion)}
                          className={`rounded-full border border-outline-variant bg-surface-container-low px-3 py-1.5 text-left text-[11px] leading-4 text-on-surface-variant transition-colors hover:border-primary/40 hover:text-primary ${focusRingClass}`}
                        >
                          {suggestion}
                        </button>
                      ))}
                    </div>
                  ) : null}
                </div>
              </div>

              {messages.map((message, index) => {
                const isUser = message.role === 'user'
                return (
                  <div
                    key={`${message.role}-${index}`}
                    className={`flex items-end gap-2 ${isUser ? 'justify-end' : 'justify-start'}`}
                  >
                    {isUser ? null : <AssistantAvatar />}
                    <article
                      className={
                        isUser
                          ? 'max-w-[85%] rounded-2xl rounded-br-md bg-primary px-3.5 py-2.5 text-sm text-on-primary shadow-sm'
                          : 'max-w-[85%] rounded-2xl rounded-bl-md border border-outline-variant/70 bg-surface-container-lowest px-3.5 py-2.5 text-sm text-on-surface shadow-sm'
                      }
                    >
                      {isUser ? null : (
                        <p className="mb-1 font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
                          {message.hasAssessment
                            ? 'Assistant · includes assessment'
                            : 'Assistant'}
                        </p>
                      )}
                      <div className="space-y-2 leading-6">
                        {isUser ? (
                          <p className="whitespace-pre-wrap">
                            {message.content}
                          </p>
                        ) : (
                          renderSafeMarkdown(message.content)
                        )}
                      </div>
                      {!isUser &&
                      message.iocIds &&
                      message.iocIds.length > 0 ? (
                        <div className="mt-2 flex flex-wrap items-center gap-1.5 border-t border-outline-variant/60 pt-2">
                          <span className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
                            Evidence
                          </span>
                          {message.iocIds.map((id) => (
                            <Link
                              key={id}
                              to="/iocs/$id"
                              params={{ id: String(id) }}
                              className={`rounded-full bg-primary/10 px-2 py-0.5 font-mono text-[11px] font-semibold text-primary transition-colors hover:bg-primary/20 ${focusRingClass}`}
                            >
                              #{id}
                            </Link>
                          ))}
                        </div>
                      ) : null}
                    </article>
                  </div>
                )
              })}

              {busy ? (
                <div className="flex items-end gap-2">
                  <AssistantAvatar />
                  <div className="rounded-2xl rounded-bl-md border border-outline-variant/70 bg-surface-container-lowest px-4 py-3 shadow-sm">
                    <span className="sr-only">Assistant is working…</span>
                    <span
                      aria-hidden="true"
                      className="flex items-center gap-1"
                    >
                      <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-on-surface-variant/70 [animation-delay:-0.3s]" />
                      <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-on-surface-variant/70 [animation-delay:-0.15s]" />
                      <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-on-surface-variant/70" />
                    </span>
                  </div>
                </div>
              ) : null}

              {error ? (
                <div
                  role="alert"
                  className="rounded-2xl border border-error/40 bg-error-container px-3.5 py-2.5 text-sm text-on-error-container"
                >
                  {error}
                </div>
              ) : null}
            </div>

            <form
              onSubmit={(event) => {
                event.preventDefault()
                void submit()
              }}
              className="border-t border-outline-variant/60 bg-surface-container-lowest px-3 pb-3 pt-3"
            >
              <div className="flex items-end gap-2">
                <label className="sr-only" htmlFor="ioc-chat-message">
                  Ask about this IOC
                </label>
                <textarea
                  id="ioc-chat-message"
                  autoFocus
                  value={draft}
                  onChange={(event) => setDraft(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' && !event.shiftKey) {
                      event.preventDefault()
                      void submit()
                    }
                  }}
                  disabled={busy}
                  maxLength={4000}
                  rows={1}
                  placeholder={`Ask about IOC #${iocId}…`}
                  className="max-h-28 min-h-10 min-w-0 flex-1 resize-none rounded-2xl border border-outline-variant bg-surface-container-low px-3.5 py-2.5 text-sm leading-5 text-on-surface transition-colors placeholder:text-on-surface-variant/70 focus:border-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
                />
                <button
                  type="submit"
                  aria-label="Ask"
                  disabled={busy || !draft.trim()}
                  className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary text-on-primary transition-opacity disabled:opacity-40 ${focusRingClass}`}
                >
                  <span
                    aria-hidden="true"
                    className={`${iconClass} text-[20px]`}
                  >
                    send
                  </span>
                </button>
              </div>
              <p className="mt-2 text-center text-[10px] leading-4 text-on-surface-variant">
                Messages and public IOC evidence go to the Admin-configured
                model endpoint. Nothing here is stored.
              </p>
            </form>
          </section>
        </>
      ) : null}
    </div>
  )
}
