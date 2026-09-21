import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { focusRingClass } from './AppShell'
import { ErrorState, LoadingState } from './uiStates'
import type { AiSettings } from '../lib/adminSchemas'
import {
  ApiError,
  clearAdminCredential,
  getAdminCredential,
  getAiReportPolicy,
  getAiSettings,
  putAdminCredential,
  putAiReportPolicy,
  putAiSettings,
} from '../lib/apiClient'
import {
  adminAiReportPolicyKey,
  adminAiSettingsKey,
  adminCredentialKey,
} from '../lib/queryKeys'

/** The write-only identity used for the OpenAI-compatible server API key. */
const aiKeyIdentity = 'OpenAICompatible.ServerApiKey'

type ConnectionDraft = {
  baseUrl: string
  modelId: string
  enabled: boolean
}

type CredentialFeedback = {
  kind: 'success' | 'error'
  message: string
}

function settingsToDraft(settings: AiSettings): ConnectionDraft {
  return {
    baseUrl: settings.baseUrl ?? '',
    modelId: settings.modelId ?? '',
    enabled: settings.enabled,
  }
}

function draftToRequest(draft: ConnectionDraft): {
  baseUrl: string | null
  modelId: string | null
  enabled: boolean
} {
  const baseUrl = draft.baseUrl.trim()
  const modelId = draft.modelId.trim()
  return {
    baseUrl: baseUrl.length > 0 ? baseUrl : null,
    modelId: modelId.length > 0 ? modelId : null,
    enabled: draft.enabled,
  }
}

function validateConnection(draft: ConnectionDraft): {
  baseUrl?: string
  modelId?: string
} {
  const errors: { baseUrl?: string; modelId?: string } = {}
  if (draft.enabled && draft.baseUrl.trim().length === 0) {
    errors.baseUrl = 'A Base URL is required while AI analysis is enabled.'
  }
  if (draft.enabled && draft.modelId.trim().length === 0) {
    errors.modelId = 'A model ID is required while AI analysis is enabled.'
  }
  if (draft.baseUrl.trim().length > 0 && !isAbsoluteHttpUrl(draft.baseUrl.trim())) {
    errors.baseUrl = 'The Base URL must be an absolute HTTP or HTTPS URL.'
  }
  if (draft.baseUrl.trim().length > 2048) {
    errors.baseUrl = 'The Base URL must be at most 2048 characters.'
  }
  if (draft.modelId.trim().length > 200) {
    errors.modelId = 'The model ID must be at most 200 characters.'
  }
  return errors
}

function isAbsoluteHttpUrl(value: string): boolean {
  try {
    const url = new URL(value)
    return url.protocol === 'http:' || url.protocol === 'https:'
  } catch {
    return false
  }
}

/* -------------------------------------------------------------------------- */
/*  Connection settings                                                      */
/* -------------------------------------------------------------------------- */

function AiConnectionCard() {
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState<ConnectionDraft | null>(null)
  const [baseline, setBaseline] = useState<ConnectionDraft | null>(null)
  const [fieldErrors, setFieldErrors] = useState<{
    baseUrl?: string
    modelId?: string
  }>({})
  const [feedback, setFeedback] = useState<CredentialFeedback | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  const {
    data: settings,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: adminAiSettingsKey(),
    queryFn: ({ signal }) => getAiSettings(signal),
  })

  useEffect(() => {
    if (settings && !draft) {
      const d = settingsToDraft(settings)
      setDraft(d)
      setBaseline(d)
    }
  }, [settings, draft])

  const dirty =
    baseline && draft
      ? baseline.baseUrl !== draft.baseUrl ||
        baseline.modelId !== draft.modelId ||
        baseline.enabled !== draft.enabled
      : false

  const save = async () => {
    if (!draft) return
    const errors = validateConnection(draft)
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) return

    setIsSaving(true)
    setFeedback(null)
    try {
      const saved = await putAiSettings(draftToRequest(draft))
      const next = settingsToDraft(saved)
      setBaseline(next)
      setDraft(next)
      setFeedback({ kind: 'success', message: 'AI connection settings saved.' })
      void queryClient.invalidateQueries({ queryKey: adminAiSettingsKey() })
    } catch (err) {
      setFeedback({
        kind: 'error',
        message: err instanceof ApiError ? err.message : 'Save failed.',
      })
    } finally {
      setIsSaving(false)
    }
  }

  if (isLoading) return <LoadingState label="Loading AI settings…" />

  if (isError && !settings) {
    return (
      <ErrorState
        title="Failed to load AI settings"
        message={error instanceof ApiError ? error.message : undefined}
        correlationId={
          error instanceof ApiError ? error.correlationId : undefined
        }
        onRetry={() =>
          void queryClient.invalidateQueries({ queryKey: adminAiSettingsKey() })
        }
      />
    )
  }

  if (!draft) return null


  return (
    <section
      aria-labelledby="ai-connection-heading"
      className="mt-4 rounded border border-outline-variant bg-surface-container-lowest p-4"
    >
      <h3 id="ai-connection-heading" className="font-semibold text-on-surface">
        AI Connection
      </h3>
      <p className="mt-1 text-sm text-on-surface-variant">
        OpenAI-compatible chat-completions server used for IOC report analysis.
      </p>

      {feedback && (
        <div
          role={feedback.kind === 'error' ? 'alert' : 'status'}
          aria-live="polite"
          className={`mt-3 rounded border px-3 py-2 text-sm ${
            feedback.kind === 'error'
              ? 'border-error/40 bg-error-container text-on-error-container'
              : 'border-tertiary/40 bg-tertiary/10 text-tertiary'
          }`}
        >
          {feedback.message}
        </div>
      )}

      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-1">
          <label
            htmlFor="ai-base-url"
            className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
          >
            Base URL
          </label>
          <input
            id="ai-base-url"
            type="text"
            autoComplete="off"
            spellCheck={false}
            value={draft.baseUrl}
            onChange={(e) =>
              setDraft({ ...draft, baseUrl: e.target.value })
            }
            placeholder="http://ollama:11434"
            aria-invalid={fieldErrors.baseUrl ? true : undefined}
            aria-describedby={fieldErrors.baseUrl ? 'ai-base-url-error' : undefined}
            className={`rounded border bg-surface-container-low px-3 py-2 font-mono text-[13px] text-on-surface ${
              fieldErrors.baseUrl ? 'border-error' : 'border-outline-variant'
            }`}
          />
          {fieldErrors.baseUrl && (
            <span id="ai-base-url-error" className="font-mono text-[10px] text-error">
              {fieldErrors.baseUrl}
            </span>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <label
            htmlFor="ai-model-id"
            className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
          >
            Model ID
          </label>
          <input
            id="ai-model-id"
            type="text"
            autoComplete="off"
            spellCheck={false}
            value={draft.modelId}
            onChange={(e) => setDraft({ ...draft, modelId: e.target.value })}
            placeholder="llama3.1"
            aria-invalid={fieldErrors.modelId ? true : undefined}
            aria-describedby={fieldErrors.modelId ? 'ai-model-id-error' : undefined}
            className={`rounded border bg-surface-container-low px-3 py-2 font-mono text-[13px] text-on-surface ${
              fieldErrors.modelId ? 'border-error' : 'border-outline-variant'
            }`}
          />
          {fieldErrors.modelId && (
            <span id="ai-model-id-error" className="font-mono text-[10px] text-error">
              {fieldErrors.modelId}
            </span>
          )}
        </div>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-4">
        <label className="flex items-center gap-2 font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
          <input
            type="checkbox"
            checked={draft.enabled}
            onChange={(e) => setDraft({ ...draft, enabled: e.target.checked })}
          />
          AI analysis enabled
        </label>
        <button
          type="button"
          onClick={() => void save()}
          disabled={!dirty || isSaving}
          className={`rounded bg-primary px-3 py-1.5 font-mono text-[11px] font-semibold text-on-primary disabled:opacity-50 ${focusRingClass}`}
        >
          {isSaving ? 'Saving…' : 'Save connection'}
        </button>
        <span className="font-mono text-[10px] text-on-surface-variant">
          {settings?.enabled ? 'Active' : 'Inactive'}
        </span>
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*  Write-only server key                                                    */
/* -------------------------------------------------------------------------- */

function AiKeyCard() {
  const queryClient = useQueryClient()
  const [value, setValue] = useState('')
  const [busy, setBusy] = useState(false)
  const [feedback, setFeedback] = useState<CredentialFeedback | null>(null)

  const { data: status } = useQuery({
    queryKey: adminCredentialKey(aiKeyIdentity),
    queryFn: ({ signal }) => getAdminCredential(aiKeyIdentity, signal),
  })

  const save = async () => {
    if (!value.trim()) {
      setFeedback({ kind: 'error', message: 'Enter a key before saving.' })
      return
    }
    setBusy(true)
    setFeedback(null)
    try {
      await putAdminCredential(aiKeyIdentity, value)
      setValue('')
      void queryClient.invalidateQueries({
        queryKey: adminCredentialKey(aiKeyIdentity),
      })
      setFeedback({ kind: 'success', message: 'Server API key saved.' })
    } catch (err) {
      setFeedback({
        kind: 'error',
        message: err instanceof ApiError ? err.message : 'Key update failed.',
      })
    } finally {
      setBusy(false)
    }
  }

  const clear = async () => {
    const confirmed = window.confirm(
      'Clear the AI server API key? This action cannot be undone.',
    )
    if (!confirmed) return
    setBusy(true)
    setFeedback(null)
    try {
      await clearAdminCredential(aiKeyIdentity)
      setValue('')
      void queryClient.invalidateQueries({
        queryKey: adminCredentialKey(aiKeyIdentity),
      })
      setFeedback({ kind: 'success', message: 'Server API key cleared.' })
    } catch (err) {
      setFeedback({
        kind: 'error',
        message: err instanceof ApiError ? err.message : 'Key clear failed.',
      })
    } finally {
      setBusy(false)
    }
  }

  return (
    <section
      aria-labelledby="ai-key-heading"
      className="mt-4 rounded border border-outline-variant bg-surface-container-lowest p-4"
    >
      <h3 id="ai-key-heading" className="font-semibold text-on-surface">
        AI Server API Key
      </h3>
      <p className="mt-1 text-sm text-on-surface-variant">
        Write-only credential for the configured model server. Optional for
        keyless local servers.
      </p>

      {feedback && (
        <div
          role={feedback.kind === 'error' ? 'alert' : 'status'}
          aria-live="polite"
          className={`mt-3 rounded border px-3 py-2 text-sm ${
            feedback.kind === 'error'
              ? 'border-error/40 bg-error-container text-on-error-container'
              : 'border-tertiary/40 bg-tertiary/10 text-tertiary'
          }`}
        >
          {feedback.message}
        </div>
      )}

      <div className="mt-4 flex flex-wrap items-end gap-3">
        <div className="min-w-[220px] flex-1">
          <label
            htmlFor="ai-key-input"
            className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
          >
            New key
          </label>
          <input
            id="ai-key-input"
            type="password"
            autoComplete="new-password"
            spellCheck={false}
            data-1p-ignore="true"
            data-lpignore="true"
            value={value}
            onChange={(e) => setValue(e.target.value)}
            placeholder="Enter new key"
            className="credential-input mt-1 w-full rounded border border-outline-variant bg-surface-container-low px-3 py-2 font-mono text-[13px] text-on-surface"
          />
        </div>
        <button
          type="button"
          disabled={busy}
          onClick={() => void save()}
          className={`rounded bg-primary px-3 py-1.5 font-mono text-[11px] font-semibold text-on-primary disabled:opacity-50 ${focusRingClass}`}
        >
          {busy ? 'Saving…' : 'Set / replace'}
        </button>
        <button
          type="button"
          disabled={busy || !status?.configured}
          onClick={() => void clear()}
          className={`rounded border border-outline-variant px-3 py-1.5 font-mono text-[11px] font-semibold text-on-surface disabled:opacity-50 ${focusRingClass}`}
        >
          Clear
        </button>
        <span
          role="status"
          className="font-mono text-[10px] text-on-surface-variant"
        >
          {status?.configured ? 'Configured' : 'Not configured'}
        </span>
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*  Report instruction editor                                                */
/* -------------------------------------------------------------------------- */

function AiPolicyCard() {
  const queryClient = useQueryClient()
  const [text, setText] = useState<string | null>(null)
  const [baselineText, setBaselineText] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [feedback, setFeedback] = useState<CredentialFeedback | null>(null)
  const [conflict, setConflict] = useState(false)
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const {
    data: policy,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: adminAiReportPolicyKey(),
    queryFn: ({ signal }) => getAiReportPolicy(signal),
  })

  useEffect(() => {
    if (policy && text === null) {
      setText(policy.text)
      setBaselineText(policy.text)
    }
  }, [policy, text])

  useEffect(
    () => () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current)
    },
    [],
  )

  if (isLoading) return <LoadingState label="Loading report instructions…" />

  if (isError && !policy) {
    return (
      <ErrorState
        title="Failed to load report instructions"
        message={error instanceof ApiError ? error.message : undefined}
        correlationId={
          error instanceof ApiError ? error.correlationId : undefined
        }
        onRetry={() =>
          void queryClient.invalidateQueries({
            queryKey: adminAiReportPolicyKey(),
          })
        }
      />
    )
  }

  if (!policy) return null

  const dirty = baselineText !== null && text !== baselineText
  const publish = async (textToPublish: string) => {
    setBusy(true)
    setFeedback(null)
    setConflict(false)
    try {
      await putAiReportPolicy({
        text: textToPublish,
        expectedVersion: policy.activeVersion,
      })
      setFeedback({ kind: 'success', message: 'Report instructions published.' })
      setText(null)
      setBaselineText(null)
      void queryClient.invalidateQueries({
        queryKey: adminAiReportPolicyKey(),
      })
      if (timeoutRef.current) clearTimeout(timeoutRef.current)
      timeoutRef.current = setTimeout(() => setFeedback(null), 30000)
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setConflict(true)
        setFeedback({
          kind: 'error',
          message:
            'Another administrator published a newer version. Your draft is preserved below for review.',
        })
      } else {
        setFeedback({
          kind: 'error',
          message: err instanceof ApiError ? err.message : 'Publish failed.',
        })
      }
    } finally {
      setBusy(false)
    }
  }

  const restore = (versionText: string) => {
    setText(versionText)
    setFeedback(null)
  }

  return (
    <section
      aria-labelledby="ai-policy-heading"
      className="mt-4 rounded border border-outline-variant bg-surface-container-lowest p-4"
    >
      <h3 id="ai-policy-heading" className="font-semibold text-on-surface">
        Report Instructions
      </h3>
      <p className="mt-1 text-sm text-on-surface-variant">
        Versioned report rules sent to the model. Publishing creates an
        immutable new version.
      </p>

      {feedback && (
        <div
          role={feedback.kind === 'error' ? 'alert' : 'status'}
          aria-live="polite"
          className={`mt-3 rounded border px-3 py-2 text-sm ${
            feedback.kind === 'error'
              ? 'border-error/40 bg-error-container text-on-error-container'
              : 'border-tertiary/40 bg-tertiary/10 text-tertiary'
          }`}
        >
          {feedback.message}
        </div>
      )}

      <div className="mt-3 flex flex-wrap items-center gap-3">
        <span className="font-mono text-[10px] text-on-surface-variant">
          Active version: {policy.activeVersion}
        </span>
        {conflict && (
          <span className="font-mono text-[10px] text-warning">
            Conflict — reload and review
          </span>
        )}
      </div>

      <div className="mt-3 flex flex-col gap-1">
        <label
          htmlFor="ai-policy-text"
          className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
        >
          Instruction text
        </label>
        <textarea
          id="ai-policy-text"
          rows={14}
          spellCheck={false}
          value={text ?? ''}
          onChange={(e) => setText(e.target.value)}
          aria-describedby={conflict ? 'ai-policy-conflict-note' : undefined}
          className="w-full rounded border border-outline-variant bg-surface-container-low px-3 py-2 font-mono text-[12px] text-on-surface"
        />
        {conflict && (
          <span id="ai-policy-conflict-note" className="font-mono text-[10px] text-warning">
            Unsaved draft retained — resolve the conflict before publishing.
          </span>
        )}
      </div>

      <div className="mt-3 flex flex-wrap gap-2">
        <button
          type="button"
          disabled={busy || !dirty || (text ?? '').trim().length === 0}
          onClick={() => void publish(text ?? '')}
          className={`rounded bg-primary px-3 py-1.5 font-mono text-[11px] font-semibold text-on-primary disabled:opacity-50 ${focusRingClass}`}
        >
          {busy ? 'Publishing…' : 'Publish new version'}
        </button>
        <button
          type="button"
          disabled={!dirty}
          onClick={() => setText(baselineText)}
          className={`rounded border border-outline-variant px-3 py-1.5 font-mono text-[11px] font-semibold text-on-surface disabled:opacity-50 ${focusRingClass}`}
        >
          Reset draft
        </button>
      </div>

      {policy.history.length > 1 && (
        <details className="mt-4">
          <summary className="cursor-pointer font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
            Version history ({policy.history.length})
          </summary>
          <ul className="mt-2 space-y-2">
            {policy.history.map((version) => (
              <li
                key={version.version}
                className="flex flex-wrap items-center gap-3 border-t border-outline-variant pt-2"
              >
                <span className="font-mono text-[10px] text-on-surface">
                  v{version.version}
                </span>
                <span className="font-mono text-[10px] text-on-surface-variant">
                  {version.publishedAt ?? ''}
                </span>
                <span className="min-w-[120px] flex-1 truncate font-mono text-[10px] text-on-surface-variant">
                  {version.text.split('\n')[0]}
                </span>
                <button
                  type="button"
                  onClick={() => restore(version.text)}
                  className={`rounded border border-outline-variant px-2 py-1 font-mono text-[10px] font-semibold text-on-surface ${focusRingClass}`}
                >
                  Load into editor
                </button>
              </li>
            ))}
          </ul>
        </details>
      )}
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*  Section root                                                             */
/* -------------------------------------------------------------------------- */

export function AdminAiSettings() {
  return (
    <div>
      <AiConnectionCard />
      <AiKeyCard />
      <AiPolicyCard />
      <p className="mt-2 text-xs text-on-surface-variant">
        When report generation is requested, the selected IOC&apos;s
        source-attributed evidence is sent to the configured model server.
      </p>
    </div>
  )
}
