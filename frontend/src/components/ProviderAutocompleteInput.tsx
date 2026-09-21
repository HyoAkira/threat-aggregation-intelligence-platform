import { useQuery } from '@tanstack/react-query'
import { getIocProviderNames } from '../lib/apiClient'
import { iocProviderNamesKey } from '../lib/queryKeys'

type ProviderAutocompleteInputProps = {
  id: string
  name?: string
  value: string
  onChange: (value: string) => void
  className: string
}

/**
 * Native datalist autocomplete for the provider multi-filter. The input stays
 * usable when the provider catalog is unavailable, while comma-separated
 * selections are rendered as removable chips.
 */
export function ProviderAutocompleteInput({
  id,
  name,
  value,
  onChange,
  className,
}: ProviderAutocompleteInputProps) {
  const rawTokens = value.split(',').map((token) => token.trim())
  const hasTrailingSeparator = value.trimEnd().endsWith(',')
  const committedProviders = rawTokens
    .filter(Boolean)
    .slice(0, hasTrailingSeparator ? undefined : -1)
  const draft = hasTrailingSeparator ? '' : (rawTokens.at(-1) ?? '')
  const providers = useQuery({
    queryKey: iocProviderNamesKey(),
    queryFn: ({ signal }) => getIocProviderNames(signal),
    staleTime: 30_000,
  })

  return (
    <div className="mt-1 space-y-1.5">
      {committedProviders.length > 0 ? (
        <div className="flex flex-wrap gap-1" aria-label="Selected providers">
          {committedProviders.map((provider) => (
            <span
              key={provider}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2 py-1 font-mono text-[10px] text-primary"
            >
              {provider}
              <button
                type="button"
                aria-label={`Remove ${provider}`}
                className="rounded-full px-0.5 font-semibold hover:bg-primary/15"
                onClick={() =>
                  onChange(
                    [
                      ...committedProviders.filter((item) => item !== provider),
                      draft,
                    ]
                      .filter(Boolean)
                      .join(', '),
                  )
                }
              >
                ×
              </button>
            </span>
          ))}
        </div>
      ) : null}
      <input
        id={id}
        name={name}
        multiple
        type="text"
        value={draft}
        list={`${id}-options`}
        aria-autocomplete="list"
        onChange={(event) => {
          const next = event.target.value
          const nextTokens = next.split(',').map((token) => token.trim())
          const nextValue = [...committedProviders, ...nextTokens]
            .filter(Boolean)
            .join(', ')
          const selectedProvider = providers.data?.some(
            (provider) => provider.toLowerCase() === next.trim().toLowerCase(),
          )
          onChange(
            next.endsWith(',') || selectedProvider
              ? `${nextValue}, `
              : nextValue,
          )
        }}
        className={className}
      />
      <datalist id={`${id}-options`}>
        {(providers.data ?? []).map((provider) => (
          <option key={provider} value={provider} />
        ))}
      </datalist>
    </div>
  )
}
