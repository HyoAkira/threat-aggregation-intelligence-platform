import { QueryClient } from '@tanstack/react-query'
import { isRetryableApiError } from './apiErrors'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      // 4xx responses are never retried automatically (including 403, which must
      // not retry indefinitely); unexpected 5xx/network failures retry twice.
      retry: (failureCount, error) => isRetryableApiError(error, failureCount),
    },
  },
})
