/**
 * @jest-environment jsdom
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { getIocProviderNames } from '../lib/apiClient'
import { ProviderAutocompleteInput } from './ProviderAutocompleteInput'

jest.mock('../lib/apiClient', () => {
  const actual = jest.requireActual('../lib/apiClient')
  return { ...actual, getIocProviderNames: jest.fn() }
})

const getIocProviderNamesMock = getIocProviderNames as jest.MockedFunction<
  typeof getIocProviderNames
>

test('offers provider suggestions and supports multiple removable selections', async () => {
  getIocProviderNamesMock.mockResolvedValue(['Abuse.ch', 'ThreatFox'])
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  let value = ''
  const onChange = jest.fn((next: string) => {
    value = next
  })

  const { rerender } = render(
    <QueryClientProvider client={client}>
      <ProviderAutocompleteInput
        id="provider"
        value={value}
        onChange={onChange}
        className="input"
      />
    </QueryClientProvider>,
  )

  await waitFor(() =>
    expect(
      document.querySelector<HTMLOptionElement>('#provider-options option')
        ?.value,
    ).toBe('Abuse.ch'),
  )
  fireEvent.change(screen.getByRole('combobox'), {
    target: { value: 'ThreatFox' },
  })
  expect(onChange).toHaveBeenLastCalledWith('ThreatFox, ')

  value = 'ThreatFox, Abuse.ch'
  rerender(
    <QueryClientProvider client={client}>
      <ProviderAutocompleteInput
        id="provider"
        value={value}
        onChange={onChange}
        className="input"
      />
    </QueryClientProvider>,
  )
  expect(screen.getByRole('button', { name: 'Remove ThreatFox' })).toBeTruthy()
  fireEvent.click(screen.getByRole('button', { name: 'Remove ThreatFox' }))
  expect(onChange).toHaveBeenLastCalledWith('Abuse.ch')
})
