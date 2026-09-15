import { useQueryClient } from '@tanstack/react-query'
import {
  adminProviderListKey,
  adminSummaryKey,
  adminSettingsKey,
  adminFetchJobKey,
} from './queryKeys'

/**
 * Returns a stable set of mutation invalidation helpers scoped to admin
 * resources. Each helper invalidates only the keys affected by a mutation,
 * keeping the query cache tight and avoiding unnecessary refetches.
 */
export function useAdminInvalidation() {
  const queryClient = useQueryClient()

  return {
    /** After any provider state change (fetch trigger, enable/disable, interval). */
    invalidateProviders: () =>
      queryClient.invalidateQueries({ queryKey: adminProviderListKey() }),

    /** After a fetch completes or history is relevant. */
    invalidateHistory: (providerId: number) =>
      queryClient.invalidateQueries({
        queryKey: ['admin', 'providers', providerId, 'history'],
      }),

    /** After any fetch that affects IOC counts. */
    invalidateSummary: () =>
      queryClient.invalidateQueries({ queryKey: adminSummaryKey() }),

    /** After settings save. */
    invalidateSettings: () =>
      queryClient.invalidateQueries({ queryKey: adminSettingsKey() }),

    /** After a fetch job reaches terminal state. */
    invalidateFetchJob: (jobId: string) =>
      queryClient.invalidateQueries({ queryKey: adminFetchJobKey(jobId) }),

    /**
     * After a successful fetch trigger: invalidate provider, history, and
     * summary in one batch so the dashboard reflects the new state.
     */
    invalidateAfterFetch: (providerId: number) => {
      queryClient.invalidateQueries({ queryKey: adminProviderListKey() })
      queryClient.invalidateQueries({
        queryKey: ['admin', 'providers', providerId, 'history'],
      })
      queryClient.invalidateQueries({ queryKey: adminSummaryKey() })
    },
  }
}
