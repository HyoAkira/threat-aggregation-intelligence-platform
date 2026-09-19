import { useQuery } from '@tanstack/react-query'
import { getIocTagCatalog } from '../lib/apiClient'
import { iocTagCatalogKey } from '../lib/queryKeys'

type TagAutocompleteInputProps = {
  id: string
  value: string
  onChange: (value: string) => void
  className: string
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
}: TagAutocompleteInputProps) {
  const query = value.trim()
  const suggestions = useQuery({
    queryKey: iocTagCatalogKey(query),
    queryFn: ({ signal }) => getIocTagCatalog({ q: query, limit: 8 }, signal),
    enabled: query.length > 0,
    staleTime: 30_000,
  })

  return (
    <>
      <input
        id={id}
        type="text"
        value={value}
        list={`${id}-options`}
        onChange={(event) => onChange(event.target.value)}
        className={className}
      />
      <datalist id={`${id}-options`}>
        {(suggestions.data ?? []).map((tag) => (
          <option key={tag.name} value={tag.name} label={`${tag.usageCount} uses`} />
        ))}
      </datalist>
    </>
  )
}
