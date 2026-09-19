/**
 * @jest-environment jsdom
 */
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AdminIocChatSettings } from './AdminIocChatSettings'
import { ApiError, getIocChatSettings, putAdminCredential, putIocChatSettings } from '../lib/apiClient'

jest.mock('../lib/apiClient', () => {
  const actual = jest.requireActual('../lib/apiClient')
  return { ...actual, getIocChatSettings: jest.fn(), putIocChatSettings: jest.fn(), putAdminCredential: jest.fn() }
})

const getMock = getIocChatSettings as jest.MockedFunction<typeof getIocChatSettings>
const putSettingsMock = putIocChatSettings as jest.MockedFunction<typeof putIocChatSettings>
const putKeyMock = putAdminCredential as jest.MockedFunction<typeof putAdminCredential>

const fixture = { baseUrl: 'http://model:11434', modelId: 'ioc-model', enabled: false, apiKeyConfigured: false }

function renderSettings() {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AdminIocChatSettings />
    </QueryClientProvider>,
  )
}

describe('AdminIocChatSettings', () => {
  beforeEach(() => {
    jest.resetAllMocks()
    getMock.mockResolvedValue(fixture)
  })

  afterEach(cleanup)

  it('validates enabled settings accessibly without sending invalid data', async () => {
    renderSettings()
    const enabled = await screen.findByRole('checkbox', { name: /Enable anonymous IOC chat/i })
    fireEvent.click(enabled)
    fireEvent.change(screen.getByLabelText('Base URL'), { target: { value: 'not-a-url' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save chat settings' }))

    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(putSettingsMock).not.toHaveBeenCalled()
  })

  it('retains the draft after save failure and clears plaintext key after success', async () => {
    putSettingsMock.mockRejectedValue(new ApiError(503, 'failure', 'Save failed'))
    renderSettings()
    const baseUrl = await screen.findByLabelText('Base URL')
    fireEvent.change(baseUrl, { target: { value: 'http://changed:11434' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save chat settings' }))
    await screen.findByText('Save failed')
    expect(screen.getByDisplayValue('http://changed:11434')).toBeTruthy()

    putKeyMock.mockResolvedValue({ identity: 'IocChat.OpenAICompatible.ServerApiKey', configured: true, keyVersion: 1, updatedAt: '2026-09-18T00:00:00Z' })
    const key = screen.getByLabelText('Replace API key') as HTMLInputElement
    fireEvent.change(key, { target: { value: 'secret-value' } })
    fireEvent.click(screen.getByRole('button', { name: 'Set API key' }))
    await waitFor(() => expect(key.value).toBe(''))
    expect(screen.queryByText('secret-value')).toBeNull()
  })
})