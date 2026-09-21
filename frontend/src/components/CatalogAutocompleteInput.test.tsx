/** @jest-environment jsdom */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { getIocMetadataCatalog, getIocTagCatalog } from '../lib/apiClient'
import { CatalogAutocompleteInput } from './TagAutocompleteInput'

jest.mock('../lib/apiClient', () => ({
  ...jest.requireActual('../lib/apiClient'),
  getIocMetadataCatalog: jest.fn(),
  getIocTagCatalog: jest.fn(),
}))
const metadata = jest.mocked(getIocMetadataCatalog)
const tags = jest.mocked(getIocTagCatalog)
beforeEach(() => jest.resetAllMocks())

function mount(
  kind: 'tag' | 'malware' | 'target',
  value: string,
  multiple = false,
) {
  const onChange = jest.fn()
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <label htmlFor="filter">Filter</label>
      <CatalogAutocompleteInput
        id="filter"
        kind={kind}
        value={value}
        onChange={onChange}
        className="input"
        multiple={multiple}
      />
    </QueryClientProvider>,
  )
  return onChange
}

test.each(['malware', 'target'] as const)(
  '%s suggestions use the correct catalog and retain exact filter values',
  async (kind) => {
    metadata.mockResolvedValue([{ name: 'example', usageCount: 2 }])
    const onChange = mount(kind, ' ex ')
    await waitFor(() =>
      expect(document.querySelector('option')?.value).toBe('example'),
    )
    expect(metadata).toHaveBeenCalledWith(
      kind,
      { q: 'ex', limit: 8 },
      expect.any(AbortSignal),
    )
    const input = screen.getByRole('combobox', { name: 'Filter' })
    // Native datalist owns ArrowDown/Enter selection; no custom handler cancels keyboard events.
    input.focus()
    expect(document.activeElement).toBe(input)
    expect(fireEvent.keyDown(input, { key: 'ArrowDown' })).toBe(true)
    fireEvent.change(input, { target: { value: 'example' } })
    fireEvent.change(input, { target: { value: 'example' } })
    expect(onChange).toHaveBeenLastCalledWith('example')
  },
)

test.each(['tag', 'malware', 'target'] as const)(
  '%s blank queries do not fetch',
  (kind) => {
    mount(kind, '   ')
    expect(metadata).not.toHaveBeenCalled()
    expect(tags).not.toHaveBeenCalled()
    expect(document.querySelectorAll('option')).toHaveLength(0)
  },
)

test.each([false, true])(
  'empty or failed catalog keeps free text usable (failure=%s)',
  async (fail) => {
    if (fail) metadata.mockRejectedValue(new Error('offline'))
    else metadata.mockResolvedValue([])
    const onChange = mount('target', 'unknown')
    await waitFor(() => expect(metadata).toHaveBeenCalled())
    fireEvent.change(screen.getByRole('combobox'), {
      target: { value: 'manual' },
    })
    expect(onChange).toHaveBeenLastCalledWith('manual')
    expect(document.querySelectorAll('option')).toHaveLength(0)
  },
)

test('repeated tag selection is deduplicated case-insensitively', async () => {
  tags.mockResolvedValue([{ name: 'botnet', usageCount: 2 }])
  const onChange = mount('tag', 'botnet, bo', true)
  await waitFor(() => expect(document.querySelector('option')).not.toBeNull())
  fireEvent.change(screen.getByRole('combobox'), {
    target: { value: 'botnet' },
  })
  expect(onChange).toHaveBeenLastCalledWith('botnet, ')
})
