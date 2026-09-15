import { useQuery, useQueryClient } from '@tanstack/react-query'
import { createFileRoute, useBlocker } from '@tanstack/react-router'
import { useCallback, useEffect, useRef, useState } from 'react'
import { focusRingClass } from '../components/AppShell'
import { ErrorState, LoadingState } from '../components/uiStates'
import type {
  ProviderSettingRequest,
  SettingsResponse,
  SupportedLogLevel,
} from '../lib/adminSchemas'
import { supportedLogLevels } from '../lib/adminSchemas'
import { ApiError, getAdminSettings, putAdminSettings } from '../lib/apiClient'
import { adminSettingsKey } from '../lib/queryKeys'

export const Route = createFileRoute('/admin/settings')({
  component: AdminSettingsPage,
})

/* -------------------------------------------------------------------------- */
/*  Draft type                                                               */
/* -------------------------------------------------------------------------- */

type ProviderDraft = {
  id: number
  name: string
  isEnabled: boolean
  intervalMinutes: number
  minimumIntervalMinutes: number
}

type Draft = {
  logLevel: SupportedLogLevel
  providers: ProviderDraft[]
}

function baselineToDraft(baseline: SettingsResponse): Draft {
  return {
    logLevel: baseline.logLevel as SupportedLogLevel,
    providers: baseline.providers.map((p) => ({
      id: p.id,
      name: p.name,
      isEnabled: p.isEnabled,
      intervalMinutes: p.intervalMinutes,
      minimumIntervalMinutes: p.minimumIntervalMinutes,
    })),
  }
}

function draftToRequest(draft: Draft): {
  logLevel: string
  providers: ProviderSettingRequest[]
} {
  return {
    logLevel: draft.logLevel,
    providers: draft.providers.map((p) => ({
      id: p.id,
      isEnabled: p.isEnabled,
      intervalMinutes: p.intervalMinutes,
    })),
  }
}

function isDirty(baseline: Draft, draft: Draft): boolean {
  if (baseline.logLevel !== draft.logLevel) return true
  if (baseline.providers.length !== draft.providers.length) return true
  for (let i = 0; i < baseline.providers.length; i++) {
    const b = baseline.providers[i]
    const d = draft.providers[i]
    if (
      b.id !== d.id ||
      b.isEnabled !== d.isEnabled ||
      b.intervalMinutes !== d.intervalMinutes
    ) {
      return true
    }
  }
  return false
}

/* -------------------------------------------------------------------------- */
/*  Validation                                                               */
/* -------------------------------------------------------------------------- */

type FieldErrors = {
  logLevel?: string
  providers?: Record<number, { interval?: string }>
}

function validateDraft(draft: Draft): FieldErrors {
  const errors: FieldErrors = {}

  if (!supportedLogLevels.includes(draft.logLevel)) {
    errors.logLevel = `Must be one of: ${supportedLogLevels.join(', ')}`
  }

  const providerErrors: Record<number, { interval?: string }> = {}
  for (const p of draft.providers) {
    if (p.intervalMinutes < p.minimumIntervalMinutes) {
      providerErrors[p.id] = {
        interval: `Minimum interval is ${p.minimumIntervalMinutes} minutes`,
      }
    }
  }
  if (Object.keys(providerErrors).length > 0) {
    errors.providers = providerErrors
  }

  return errors
}

function hasErrors(errors: FieldErrors): boolean {
  if (errors.logLevel) return true
  if (errors.providers) {
    for (const key of Object.keys(errors.providers)) {
      const pe = errors.providers[key as unknown as number]
      if (pe.interval) return true
    }
  }
  return false
}

/* -------------------------------------------------------------------------- */
/*  Page component                                                           */
/* -------------------------------------------------------------------------- */

function AdminSettingsPage() {
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState<Draft | null>(null)
  const [baseline, setBaseline] = useState<Draft | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [saveError, setSaveError] = useState<string | null>(null)
  const [saveSuccess, setSaveSuccess] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const saveTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const {
    data: settings,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: adminSettingsKey(),
    queryFn: ({ signal }) => getAdminSettings(signal),
  })

  // Initialize draft from fetched settings
  useEffect(() => {
    if (settings && !draft) {
      const d = baselineToDraft(settings)
      setDraft(d)
      setBaseline(d)
    }
  }, [settings, draft])

  // Dirty state
  const dirty = baseline && draft ? isDirty(baseline, draft) : false

  // Warn on navigation/reload when dirty
  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (dirty) {
        e.preventDefault()
      }
    }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [dirty])

  // Block route navigation when dirty
  useBlocker({
    condition: dirty,
    blockerFn: () => {
      const leave = window.confirm(
        'You have unsaved changes. Are you sure you want to leave?',
      )
      return !leave
    },
  })

  // Cleanup timeout on unmount
  useEffect(() => {
    return () => {
      if (saveTimeoutRef.current) clearTimeout(saveTimeoutRef.current)
    }
  }, [])

  const handleReset = useCallback(() => {
    if (baseline) {
      setDraft(baseline)
      setFieldErrors({})
      setSaveError(null)
      setSaveSuccess(false)
    }
  }, [baseline])

  const handleSave = useCallback(async () => {
    if (!draft) return

    const errors = validateDraft(draft)
    setFieldErrors(errors)
    if (hasErrors(errors)) return

    setIsSaving(true)
    setSaveError(null)
    setSaveSuccess(false)

    try {
      const request = draftToRequest(draft)
      await putAdminSettings(request)
      // PUT is an atomic 204 endpoint. The accepted draft is therefore the
      // new baseline; the invalidated GET below refreshes the authoritative
      // snapshot without treating the empty success body as malformed JSON.
      const newBaseline = draft
      setBaseline(newBaseline)
      setDraft(newBaseline)
      setFieldErrors({})
      setSaveSuccess(true)
      void queryClient.invalidateQueries({ queryKey: adminSettingsKey() })
      void queryClient.invalidateQueries({ queryKey: adminProviderListKey() })

      // Auto-clear success message after 30 seconds
      if (saveTimeoutRef.current) clearTimeout(saveTimeoutRef.current)
      saveTimeoutRef.current = setTimeout(() => setSaveSuccess(false), 30000)
    } catch (err) {
      if (err instanceof ApiError) {
        setSaveError(err.message)
      } else {
        setSaveError('An unexpected error occurred while saving.')
      }
    } finally {
      setIsSaving(false)
    }
  }, [draft, queryClient])

  const updateLogLevel = (value: SupportedLogLevel) => {
    if (!draft) return
    setDraft({ ...draft, logLevel: value })
    setSaveSuccess(false)
  }

  const updateProviderInterval = (id: number, value: string) => {
    if (!draft) return
    const parsed = parseInt(value, 10)
    setDraft({
      ...draft,
      providers: draft.providers.map((p) =>
        p.id === id ? { ...p, intervalMinutes: isNaN(parsed) ? 0 : parsed } : p,
      ),
    })
    setSaveSuccess(false)
  }

  const updateProviderEnabled = (id: number, value: boolean) => {
    if (!draft) return
    setDraft({
      ...draft,
      providers: draft.providers.map((p) =>
        p.id === id ? { ...p, isEnabled: value } : p,
      ),
    })
    setSaveSuccess(false)
  }

  if (isLoading) return <LoadingState label="Loading settings…" />

  if (isError && !settings) {
    return (
      <ErrorState
        title="Failed to load settings"
        message={error instanceof ApiError ? error.message : undefined}
        correlationId={
          error instanceof ApiError ? error.correlationId : undefined
        }
        onRetry={() =>
          void queryClient.invalidateQueries({ queryKey: adminSettingsKey() })
        }
      />
    )
  }

  if (!draft) return null

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold text-on-surface">
            Runtime Settings
          </h2>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            Non-secret provider and logging configuration. Changes apply within
            30 seconds of saving.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={handleReset}
            disabled={!dirty || isSaving}
            className={`rounded border border-outline-variant bg-surface-container-lowest px-3 py-1.5 font-mono text-[11px] font-semibold text-on-surface transition-colors hover:bg-surface-container-low disabled:cursor-not-allowed disabled:opacity-50 ${focusRingClass}`}
          >
            Reset
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={!dirty || isSaving}
            className={`rounded bg-primary px-3 py-1.5 font-mono text-[11px] font-semibold text-on-primary transition-colors hover:bg-primary-container disabled:cursor-not-allowed disabled:opacity-50 ${focusRingClass}`}
          >
            {isSaving ? 'Saving…' : 'Save Settings'}
          </button>
        </div>
      </div>

      {/* Success message */}
      {saveSuccess && (
        <div
          role="status"
          className="mt-4 rounded border border-tertiary/40 bg-tertiary/10 px-4 py-2 text-sm text-tertiary"
        >
          <span className="font-semibold">Settings saved.</span> Runtime
          behavior will reflect changes within 30 seconds.
        </div>
      )}

      {/* Save error */}
      {saveError && (
        <div
          role="alert"
          className="mt-4 rounded border border-error/40 bg-error-container px-4 py-2 text-sm text-on-error-container"
        >
          <span className="font-semibold">Save failed:</span> {saveError}
        </div>
      )}

      {/* Dirty indicator */}
      {dirty && !saveSuccess && (
        <div className="mt-4 rounded border border-warning/40 bg-warning-container px-4 py-2 text-sm text-on-warning-container">
          <span className="font-semibold">Unsaved changes</span> — you have
          modified settings that have not been saved.
        </div>
      )}

      {/* Log level */}
      <div className="mt-6 rounded border border-outline-variant bg-surface-container-lowest p-4">
        <h3 className="font-semibold text-on-surface">Logging</h3>
        <div className="mt-3 flex flex-col gap-1">
          <label
            htmlFor="log-level"
            className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
          >
            Log Level
          </label>
          <select
            id="log-level"
            value={draft.logLevel}
            onChange={(e) =>
              updateLogLevel(e.target.value as SupportedLogLevel)
            }
            className={`w-full max-w-xs rounded border bg-surface-container-low px-3 py-2 font-body text-[13px] text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary ${
              fieldErrors.logLevel ? 'border-error' : 'border-outline-variant'
            }`}
          >
            {supportedLogLevels.map((l) => (
              <option key={l} value={l}>
                {l}
              </option>
            ))}
          </select>
          {fieldErrors.logLevel && (
            <span className="font-mono text-[10px] text-error">
              {fieldErrors.logLevel}
            </span>
          )}
        </div>
      </div>

      {/* Provider intervals */}
      <div className="mt-4 rounded border border-outline-variant bg-surface-container-lowest">
        <div className="border-b border-outline-variant px-4 py-3">
          <h3 className="font-semibold text-on-surface">Provider Settings</h3>
        </div>
        <div className="divide-y divide-surface-container-low">
          {draft.providers.map((provider) => {
            const intervalError = fieldErrors.providers?.[provider.id]?.interval
            return (
              <div
                key={provider.id}
                className="flex flex-wrap items-center gap-4 px-4 py-3"
              >
                <div className="min-w-[160px] flex-1">
                  <div className="font-semibold text-on-surface">
                    {provider.name}
                  </div>
                  <div className="font-mono text-[10px] text-on-surface-variant">
                    ID: {provider.id}
                  </div>
                </div>

                {/* Enabled toggle */}
                <div className="flex items-center gap-2">
                  <label
                    htmlFor={`provider-enabled-${provider.id}`}
                    className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
                  >
                    Enabled
                  </label>
                  <button
                    id={`provider-enabled-${provider.id}`}
                    type="button"
                    role="switch"
                    aria-checked={provider.isEnabled}
                    onClick={() =>
                      updateProviderEnabled(provider.id, !provider.isEnabled)
                    }
                    className={`relative inline-flex h-5 w-9 cursor-pointer rounded-full transition-colors ${
                      provider.isEnabled
                        ? 'bg-primary'
                        : 'bg-surface-container-high'
                    } ${focusRingClass}`}
                  >
                    <span
                      className={`inline-block h-4 w-4 rounded-full bg-white transition-transform ${
                        provider.isEnabled ? 'translate-x-4' : 'translate-x-0.5'
                      } mt-0.5`}
                    />
                  </button>
                </div>

                {/* Interval */}
                <div className="flex flex-col gap-1">
                  <label
                    htmlFor={`provider-interval-${provider.id}`}
                    className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant"
                  >
                    Interval (min)
                  </label>
                  <input
                    id={`provider-interval-${provider.id}`}
                    type="number"
                    min={provider.minimumIntervalMinutes}
                    value={provider.intervalMinutes}
                    onChange={(e) =>
                      updateProviderInterval(provider.id, e.target.value)
                    }
                    className={`w-24 rounded border bg-surface-container-low px-3 py-1.5 font-mono text-[13px] text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary ${
                      intervalError ? 'border-error' : 'border-outline-variant'
                    }`}
                  />
                  {intervalError && (
                    <span className="font-mono text-[10px] text-error">
                      {intervalError}
                    </span>
                  )}
                </div>

                <span className="font-mono text-[10px] text-on-surface-variant">
                  min: {provider.minimumIntervalMinutes}m
                </span>
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
