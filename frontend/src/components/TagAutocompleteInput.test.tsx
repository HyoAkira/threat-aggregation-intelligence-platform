/**
 * @jest-environment jsdom
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { getIocTagCatalog } from '../lib/apiClient'
import { TagAutocompleteInput } from './TagAutocompleteInput'

jest.mock('../lib/apiClient', () => {
  const actual = jest.requireActual('../lib/apiClient')
  return { ...actual, getIocTagCatalog: jest.fn() }
})

const getIocTagCatalogMock = getIocTagCatalog as jest.MockedFunction<
  typeof getIocTagCatalog
>

test('renders catalog suggestions while keeping a normal text input', async () => {
  getIocTagCatalogMock.mockResolvedValue([
    { name: 'phishing', usageCount: 3 },
  ])
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })

  render(
    <QueryClientProvider client={client}>
      <TagAutocompleteInput
        id="tag"
        value="phi"
        onChange={() => undefined}
        className="input"
      />
    </QueryClientProvider>,
  )

  expect(screen.getByRole('combobox').getAttribute('list')).toBe('tag-options')
  await waitFor(() =>
    expect(
      document.querySelector('#tag-options option')?.getAttribute('value'),
    ).toBe('phishing'),
  )
})
