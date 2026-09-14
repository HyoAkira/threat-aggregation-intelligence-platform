import { useState } from 'react'
import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router'
import { z } from 'zod'
import { ApiError, login } from '../lib/apiClient'
import { currentSession, startSession } from '../lib/authStore'
import { resolveLoginRedirect } from '../lib/loginRedirect'
import { focusRingClass } from '../components/AppShell'
import { ErrorState } from '../components/uiStates'

const loginSearchSchema = z.object({
  redirect: z.coerce.string().optional(),
})

export const Route = createFileRoute('/login')({
  validateSearch: loginSearchSchema,
  beforeLoad: ({ search }) => {
    if (currentSession()) {
      throw redirect({ to: resolveLoginRedirect(search.redirect) })
    }
  },
  component: LoginPage,
})

const inputClass = `w-full rounded-md border border-slate-400 px-3 py-2 text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700`

function LoginPage() {
  const navigate = useNavigate()
  const redirectTarget = resolveLoginRedirect(Route.useSearch().redirect)
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<{
    message: string
    correlationId?: string
  } | null>(null)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (pending) return
    if (username.trim() === '' || password === '') {
      setError({ message: 'Enter both a username and a password.' })
      return
    }
    setPending(true)
    setError(null)
    try {
      const response = await login(username.trim(), password)
      startSession(response)
      await navigate({ to: redirectTarget as '/' })
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 401) {
        setError({ message: 'Invalid username or password.' })
      } else if (cause instanceof ApiError) {
        setError({
          message: cause.message,
          correlationId: cause.correlationId,
        })
      } else {
        setError({ message: 'Sign-in failed unexpectedly. Please try again.' })
      }
    } finally {
      setPending(false)
    }
  }

  return (
    <section aria-labelledby="login-heading" className="mx-auto max-w-md">
      <h1 id="login-heading" className="text-2xl font-bold text-slate-900">
        Sign in
      </h1>
      <p className="mt-2 text-sm text-slate-700">
        Sign in to reach your protected workflows. Searching and browsing
        indicators stays open without an account.
      </p>
      <form onSubmit={handleSubmit} className="mt-6 space-y-4" noValidate>
        <div>
          <label
            htmlFor="login-username"
            className="block text-sm font-medium text-slate-800"
          >
            Username
          </label>
          <input
            id="login-username"
            name="username"
            type="text"
            autoComplete="username"
            required
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            className={`mt-1 ${inputClass}`}
          />
        </div>
        <div>
          <label
            htmlFor="login-password"
            className="block text-sm font-medium text-slate-800"
          >
            Password
          </label>
          <input
            id="login-password"
            name="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className={`mt-1 ${inputClass}`}
          />
        </div>
        {error ? (
          <ErrorState
            title="Sign-in failed"
            message={error.message}
            correlationId={error.correlationId}
          />
        ) : null}
        <button
          type="submit"
          disabled={pending}
          aria-busy={pending}
          className={`rounded-md bg-sky-800 px-4 py-2 font-medium text-white enabled:hover:bg-sky-900 disabled:cursor-wait disabled:opacity-60 ${focusRingClass}`}
        >
          {pending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </section>
  )
}
