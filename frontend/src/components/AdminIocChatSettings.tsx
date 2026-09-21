import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import type { IocChatSettings } from '../lib/adminSchemas'
import {
  ApiError,
  getIocChatSettings,
  putAdminCredential,
  putIocChatSettings,
} from '../lib/apiClient'
import { adminIocChatSettingsKey } from '../lib/queryKeys'
import { focusRingClass } from './AppShell'
import { ErrorState, LoadingState } from './uiStates'

const chatKeyIdentity = 'IocChat.OpenAICompatible.ServerApiKey'

type Draft = { baseUrl: string; modelId: string; enabled: boolean }

type CredentialFeedback = {
  kind: 'success' | 'error'
  message: string
}

function toDraft(settings: IocChatSettings): Draft {
  return {
    baseUrl: settings.baseUrl ?? '',
    modelId: settings.modelId ?? '',
    enabled: settings.enabled,
  }
}

function validate(draft: Draft): { baseUrl?: string; modelId?: string } {
  const errors: { baseUrl?: string; modelId?: string } = {}
  if (draft.enabled && !draft.baseUrl.trim())
    errors.baseUrl = 'A Base URL is required while IOC chat is enabled.'
  if (draft.enabled && !draft.modelId.trim())
    errors.modelId = 'A model ID is required while IOC chat is enabled.'
  if (draft.baseUrl.trim().length > 2048)
    errors.baseUrl = 'The Base URL must be at most 2048 characters.'
  if (draft.modelId.trim().length > 200)
    errors.modelId = 'The model ID must be at most 200 characters.'
  if (draft.baseUrl.trim()) {
    try {
      const url = new URL(draft.baseUrl.trim())
      if (url.protocol !== 'http:' && url.protocol !== 'https:')
        throw new Error()
    } catch {
      errors.baseUrl = 'The Base URL must be an absolute HTTP or HTTPS URL.'
    }
  }
  return errors
}

export function AdminIocChatSettings() {
  const queryClient = useQueryClient()
  const settingsQuery = useQuery({
    queryKey: adminIocChatSettingsKey(),
    queryFn: ({ signal }) => getIocChatSettings(signal),
  })
  const [draft, setDraft] = useState<Draft | null>(null)
  const [errors, setErrors] = useState<{ baseUrl?: string; modelId?: string }>(
    {},
  )
  const [key, setKey] = useState('')
  const [feedback, setFeedback] = useState<CredentialFeedback | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (settingsQuery.data && !draft) setDraft(toDraft(settingsQuery.data))
  }, [settingsQuery.data, draft])

  if (settingsQuery.isPending)
    return <LoadingState label="Loading IOC chat settings…" />
  if (settingsQuery.isError || !draft) {
    return (
      <ErrorState
        title="Failed to load IOC chat settings"
        message={
          settingsQuery.error instanceof ApiError
            ? settingsQuery.error.message
            : undefined
        }
        onRetry={() => void settingsQuery.refetch()}
      />
    )
  }

  const saveSettings = async () => {
    const nextErrors = validate(draft)
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) return
    setSaving(true)
    setFeedback(null)
    try {
      const saved = await putIocChatSettings({
        baseUrl: draft.baseUrl.trim() || null,
        modelId: draft.modelId.trim() || null,
        enabled: draft.enabled,
      })
      setDraft(toDraft(saved))
      setFeedback({
        kind: 'success',
        message: 'IOC chat connection settings saved.',
      })
      await queryClient.invalidateQueries({
        queryKey: adminIocChatSettingsKey(),
      })
    } catch (error) {
      setFeedback({
        kind: 'error',
        message:
          error instanceof ApiError
            ? error.message
            : 'Save failed. Your draft was retained.',
      })
    } finally {
      setSaving(false)
    }
  }

  const saveKey = async () => {
    if (!key.trim()) {
      setFeedback({ kind: 'error', message: 'Enter an API key before saving.' })
      return
    }
    setSaving(true)
    setFeedback(null)
    try {
      await putAdminCredential(chatKeyIdentity, key)
      setKey('')
      setFeedback({ kind: 'success', message: 'IOC chat API key configured.' })
      await queryClient.invalidateQueries({
        queryKey: adminIocChatSettingsKey(),
      })
    } catch (error) {
      setFeedback({
        kind: 'error',
        message:
          error instanceof ApiError
            ? error.message
            : 'Could not save the IOC chat API key.',
      })
    } finally {
      setSaving(false)
    }
  }

  return (
    <section
      aria-labelledby="ioc-chat-settings-heading"
      className="mt-4 rounded border border-outline-variant bg-surface-container-lowest p-4"
    >
      <h3
        id="ioc-chat-settings-heading"
        className="font-semibold text-on-surface"
      >
        IOC chat model
      </h3>
      <p className="mt-1 text-sm text-on-surface-variant">
        Requires an OpenAI-compatible model endpoint that supports tool calls.
        This configuration and credential are independent from AI analysis.
      </p>
      <div className="mt-4 grid gap-4">
        <label className="grid gap-1 text-sm">
          Base URL
          <input
            value={draft.baseUrl}
            onChange={(event) =>
              setDraft({ ...draft, baseUrl: event.target.value })
            }
            aria-invalid={Boolean(errors.baseUrl)}
            className="rounded border border-outline-variant bg-surface-container-low px-3 py-2"
          />
          {errors.baseUrl ? (
            <span role="alert" className="text-error">
              {errors.baseUrl}
            </span>
          ) : null}
        </label>
        <label className="grid gap-1 text-sm">
          Model ID
          <input
            value={draft.modelId}
            onChange={(event) =>
              setDraft({ ...draft, modelId: event.target.value })
            }
            aria-invalid={Boolean(errors.modelId)}
            className="rounded border border-outline-variant bg-surface-container-low px-3 py-2"
          />
          {errors.modelId ? (
            <span role="alert" className="text-error">
              {errors.modelId}
            </span>
          ) : null}
        </label>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={draft.enabled}
            onChange={(event) =>
              setDraft({ ...draft, enabled: event.target.checked })
            }
          />
          Enable anonymous IOC chat
        </label>
      </div>
      <button
        type="button"
        disabled={saving}
        onClick={() => void saveSettings()}
        className={`mt-4 rounded bg-primary px-3 py-2 text-sm font-semibold text-on-primary disabled:opacity-50 ${focusRingClass}`}
      >
        {saving ? 'Saving…' : 'Save chat settings'}
      </button>
      <div className="mt-5 border-t border-outline-variant pt-4">
        <p className="text-sm">
          API key status:{' '}
          <strong>
            {settingsQuery.data.apiKeyConfigured
              ? 'Configured'
              : 'Not configured'}
          </strong>
        </p>
        <label className="mt-2 grid gap-1 text-sm">
          Replace API key
          <input
            type="password"
            autoComplete="new-password"
            value={key}
            onChange={(event) => setKey(event.target.value)}
            className="rounded border border-outline-variant bg-surface-container-low px-3 py-2"
          />
        </label>
        <button
          type="button"
          disabled={saving}
          onClick={() => void saveKey()}
          className={`mt-2 rounded border border-outline-variant px-3 py-2 text-sm font-semibold disabled:opacity-50 ${focusRingClass}`}
        >
          Set API key
        </button>
      </div>
      {feedback ? (
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
      ) : null}
    </section>
  )
}
