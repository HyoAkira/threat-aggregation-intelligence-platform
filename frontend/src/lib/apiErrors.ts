import { z } from 'zod'

/** RFC 9457 problem detail as produced by the API error contract. */
export const apiProblemSchema = z.object({
  type: z.string().optional(),
  title: z.string().optional(),
  status: z.number().int().optional(),
  detail: z.string().optional(),
  code: z.string(),
  correlationId: z.string().nullable().optional(),
})

export type ApiProblem = z.infer<typeof apiProblemSchema>

/**
 * Error thrown for every non-success API response, response-validation failure, or
 * client-side session failure. Carries the machine-readable `code`, HTTP `status`,
 * and the request `correlationId` so UI states can classify and surface them.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string
  readonly problem?: ApiProblem

  constructor(
    status: number,
    code: string,
    message: string,
    correlationId?: string,
    problem?: ApiProblem,
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.correlationId = correlationId
    this.problem = problem
  }
}

/** Only unexpected server/network failures are retried automatically; 4xx never retries. */
export function isRetryableApiError(
  error: unknown,
  failureCount: number,
): boolean {
  if (failureCount >= 2) return false
  if (error instanceof ApiError) return error.status >= 500
  return true
}
