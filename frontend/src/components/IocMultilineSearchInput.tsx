import { useMemo, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getIocSuggestions } from '../lib/apiClient'
import { iocSuggestionsKey } from '../lib/queryKeys'

const suggestionLimit = 8

type IocMultilineSearchInputProps = {
  id: string
  name?: string
  value: string
  onChange: (value: string) => void
  className: string
  'aria-describedby'?: string
}

type ActiveLine = { index: number; text: string }

function resolveActiveLine(text: string, caret: number): ActiveLine {
  const clamped = Math.max(0, Math.min(caret, text.length))
  const before = text.slice(0, clamped)
  const index = before.split('\n').length - 1
  const lines = text.split('\n')
  return { index, text: lines[index].trim() }
}

function replaceLine(text: string, index: number, replacement: string): string {
  const lines = text.split('\n')
  if (index < 0 || index >= lines.length) return text
  lines[index] = replacement
  return lines.join('\n')
}

/**
 * Accessible multiline IOC editor: one value per line, with active-line prefix suggestions.
 * The editor always keeps arbitrary free-form text editable and never classifies values; only the
 * line containing the caret is queried for suggestions. Selecting a suggestion replaces that line
 * alone. Keyboard navigation follows the ARIA combobox/listbox pattern.
 */
export function IocMultilineSearchInput({
  id,
  name,
  value,
  onChange,
  className,
  'aria-describedby': describedBy,
}: IocMultilineSearchInputProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const [activeLine, setActiveLine] = useState<ActiveLine>({
    index: 0,
    text: '',
  })
  const [open, setOpen] = useState(false)
  const [highlight, setHighlight] = useState(-1)

  const suggestions = useQuery({
    queryKey: iocSuggestionsKey(activeLine.text),
    queryFn: ({ signal }) =>
      getIocSuggestions({ q: activeLine.text, limit: suggestionLimit }, signal),
    enabled: activeLine.text.length > 0,
    staleTime: 30_000,
  })

  const items = useMemo(() => suggestions.data ?? [], [suggestions.data])
  const listVisible = open && items.length > 0

  function syncActiveLine(target: HTMLTextAreaElement) {
    setActiveLine(resolveActiveLine(target.value, target.selectionStart))
  }

  function chooseSuggestion(suggestion: string) {
    onChange(
      replaceLine(
        textareaRef.current?.value ?? value,
        activeLine.index,
        suggestion,
      ),
    )
    setOpen(false)
    setHighlight(-1)
  }

  const activeOption =
    listVisible && highlight >= 0 ? items[highlight] : undefined

  return (
    <div className="mt-1 space-y-1.5">
      <textarea
        id={id}
        name={name}
        ref={textareaRef}
        required
        rows={4}
        autoComplete="off"
        spellCheck={false}
        value={value}
        role="combobox"
        aria-expanded={listVisible}
        aria-controls={`${id}-suggestions`}
        aria-autocomplete="list"
        aria-activedescendant={
          activeOption ? `${id}-suggestion-${highlight}` : undefined
        }
        aria-describedby={describedBy}
        onChange={(event) => {
          onChange(event.target.value)
          syncActiveLine(event.target)
          setOpen(true)
          setHighlight(-1)
        }}
        onSelect={(event) => {
          syncActiveLine(event.currentTarget)
          setOpen(true)
        }}
        onClick={(event) => {
          syncActiveLine(event.currentTarget)
          setOpen(true)
        }}
        onBlur={() => setOpen(false)}
        onKeyDown={(event) => {
          if (event.key === 'ArrowDown' && items.length > 0) {
            event.preventDefault()
            setOpen(true)
            setHighlight((current) => (current + 1) % items.length)
            return
          }
          if (event.key === 'ArrowUp' && items.length > 0) {
            event.preventDefault()
            setOpen(true)
            setHighlight((current) =>
              current <= 0 ? items.length - 1 : current - 1,
            )
            return
          }
          if (event.key === 'Escape') {
            setOpen(false)
            setHighlight(-1)
            return
          }
          if (
            event.key === 'Enter' &&
            listVisible &&
            highlight >= 0 &&
            items[highlight]
          ) {
            event.preventDefault()
            chooseSuggestion(items[highlight].value)
          }
        }}
        className={className}
        placeholder={'194.26.29.112\nevil-domain.ru\nhttps://…'}
      />
      <p className="text-xs text-on-surface-variant">
        Enter one IOC per line. Blank lines are ignored; a single line uses
        exact search.
      </p>
      {suggestions.isFetching && activeLine.text.length > 0 ? (
        <p role="status" className="text-xs text-outline">
          Loading suggestions…
        </p>
      ) : null}
      {listVisible ? (
        <ul
          id={`${id}-suggestions`}
          role="listbox"
          aria-label="IOC suggestions"
          className="max-h-56 overflow-auto rounded border border-outline-variant bg-surface-container-lowest py-1"
        >
          {items.map((item, index) => (
            <li
              key={`${item.iocType}:${item.value}`}
              id={`${id}-suggestion-${index}`}
              role="option"
              aria-selected={index === highlight}
              className={`flex cursor-pointer items-center justify-between gap-3 px-3 py-1.5 font-mono text-sm ${
                index === highlight
                  ? 'bg-primary/10 text-primary'
                  : 'text-on-surface hover:bg-surface-container-low'
              }`}
              onMouseDown={(event) => {
                event.preventDefault()
                chooseSuggestion(item.value)
              }}
            >
              <span className="break-all">{item.value}</span>
              <span className="text-[10px] uppercase tracking-wider text-outline">
                {item.iocType}
              </span>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  )
}
