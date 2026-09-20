import { useQuery } from '@tanstack/react-query'
import { getIocTagCatalog } from '../lib/apiClient'
import { iocTagCatalogKey } from '../lib/queryKeys'

type TagAutocompleteInputProps = {
  id: string
  value: string
  onChange: (value: string) => void
  className: string
  multiple?: boolean
}

/**
 * Native datalist autocomplete keeps tag entry accessible and dependency-free.
 * A failed catalog request intentionally leaves the ordinary text input usable.
 */
export function TagAutocompleteInput({
  id,
  value,
  onChange,
  className,
  multiple = false,
}: TagAutocompleteInputProps) {
  const rawTokens = value.split(',').map((token) => token.trim())
  const hasTrailingSeparator = value.trimEnd().endsWith(',')
  const committedTags = rawTokens
    .filter(Boolean)
    .slice(0, hasTrailingSeparator ? undefined : -1)
  const draft = hasTrailingSeparator ? '' : (rawTokens.at(-1) ?? '')
  const query = multiple ? draft : value.trim()
  const suggestions = useQuery({
    queryKey: iocTagCatalogKey(query),
    queryFn: ({ signal }) => getIocTagCatalog({ q: query, limit: 8 }, signal),
    enabled: query.length > 0,
    staleTime: 30_000,
  })

  return (
    <div className="mt-1 space-y-1.5">
      {multiple && committedTags.length > 0 ? (
        <div className="flex flex-wrap gap-1" aria-label="Selected tags">
          {committedTags.map((tag) => (
            <span
              key={tag}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2 py-1 font-mono text-[10px] text-primary"
            >
              {tag}
              <button
                type="button"
                aria-label={`Remove ${tag}`}
                className="rounded-full px-0.5 font-semibold hover:bg-primary/15"
                onClick={() =>
                  onChange(
                    [...committedTags.filter((item) => item !== tag), draft]
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
        type="text"
        value={multiple ? draft : value}
        list={`${id}-options`}
        onChange={(event) => {
          if (!multiple) {
            onChange(event.target.value)
            return
          }
          const next = event.target.value
          const nextTokens = next.split(',').map((token) => token.trim())
          const nextValue = [...committedTags, ...nextTokens]
            .filter(Boolean)
            .join(', ')
          const selectedSuggestion = suggestions.data?.some(
            (tag) => tag.name.toLowerCase() === next.trim().toLowerCase(),
          )
          onChange(
            next.endsWith(',') || selectedSuggestion
              ? `${nextValue}, `
              : nextValue,
          )
        }}
        className={className}
      />
      <datalist id={`${id}-options`}>
        {(suggestions.data ?? []).map((tag) => (
          <option
            key={tag.name}
            value={tag.name}
            label={`${tag.usageCount} uses`}
          />
        ))}
      </datalist>
    </div>
  )
}
