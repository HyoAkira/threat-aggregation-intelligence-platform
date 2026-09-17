/**
 * @jest-environment jsdom
 */
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AdminAiSettings } from './AdminAiSettings'
import {
  ApiError,
  clearAdminCredential,
  getAdminCredential,
  getAiReportPolicy,
  getAiSettings,
  putAdminCredential,
  putAiReportPolicy,
  putAiSettings,
} from '../lib/apiClient'
import { resetClientState } from '../test/renderApp'

jest.mock('../lib/apiClient', () => {
  const actual = jest.requireActual('../lib/apiClient')
  return {
    ...actual,
    getAiSettings: jest.fn(),
    putAiSettings: jest.fn(),
    getAiReportPolicy: jest.fn(),
    putAiReportPolicy: jest.fn(),
    getAdminCredential: jest.fn(),
    putAdminCredential: jest.fn(),
    clearAdminCredential: jest.fn(),
  }
})

const getAiSettingsMock = getAiSettings as jest.MockedFunction<
  typeof getAiSettings
>
const putAiSettingsMock = putAiSettings as jest.MockedFunction<
  typeof putAiSettings
>
const getAiReportPolicyMock = getAiReportPolicy as jest.MockedFunction<
  typeof getAiReportPolicy
>
const putAiReportPolicyMock = putAiReportPolicy as jest.MockedFunction<
  typeof putAiReportPolicy
>
const getAdminCredentialMock = getAdminCredential as jest.MockedFunction<
  typeof getAdminCredential
>
const putAdminCredentialMock = putAdminCredential as jest.MockedFunction<
  typeof putAdminCredential
>
const clearAdminCredentialMock = clearAdminCredential as jest.MockedFunction<
  typeof clearAdminCredential
>
void clearAdminCredentialMock

const settingsFixture = {
  baseUrl: 'http://ollama:11434',
  modelId: 'llama3.1',
  enabled: true,
  apiKeyConfigured: false,
}

const policyFixture = {
  activeVersion: 1,
  text: 'Sürüm bir yönergesi',
  authorUserId: null,
  publishedAt: '2026-09-17T10:00:00Z',
  history: [
    { version: 1, text: 'Sürüm bir yönergesi', authorUserId: null, publishedAt: '2026-09-17T10:00:00Z' },
  ],
}

function renderAiSettings() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <AdminAiSettings />
    </QueryClientProvider>,
  )
}

describe('Admin AI settings section', () => {
  beforeEach(() => {
    resetClientState()
    jest.resetAllMocks()
    getAiSettingsMock.mockResolvedValue(settingsFixture)
    getAiReportPolicyMock.mockResolvedValue(policyFixture)
    getAdminCredentialMock.mockResolvedValue({
      identity: 'OpenAICompatible.ServerApiKey',
      configured: false,
      keyVersion: null,
      updatedAt: null,
    })
  })

  afterEach(() => {
    cleanup()
    resetClientState()
  })

  it('renders connection values and accessible statuses', async () => {
    renderAiSettings()

    await screen.findByDisplayValue('http://ollama:11434')
    expect(screen.getByDisplayValue('llama3.1')).toBeTruthy()
    expect(screen.getByLabelText(/AI analysis enabled/i)).toBeTruthy()
    expect(screen.getAllByText('Not configured').length).toBeGreaterThan(0)
  })

  it('saves the connection through the dedicated endpoint and keeps the key out of it', async () => {
    putAiSettingsMock.mockResolvedValue(settingsFixture)
    const { container } = renderAiSettings()

    const modelInput = await screen.findByDisplayValue('llama3.1')
    fireEvent.change(modelInput, { target: { value: 'qwen2.5' } })
    fireEvent.click(screen.getByText('Save connection'))

    await waitFor(() => expect(putAiSettingsMock).toHaveBeenCalled())
    const request = putAiSettingsMock.mock.calls[0][0]
    const serialized = JSON.stringify(request)
    expect(serialized).not.toContain('apiKey')
    expect(serialized).not.toContain('reportPolicy')
    expect(serialized).not.toContain('providers')
    expect(request.modelId).toBe('qwen2.5')
    await screen.findByText('AI connection settings saved.')
    expect(container.textContent).toContain('AI connection settings saved.')
  })

  it('keeps the draft when the connection save fails', async () => {
    putAiSettingsMock.mockRejectedValue(
      new ApiError(503, 'upstream_unavailable', 'The service could not be reached. Please try again.'),
    )
    renderAiSettings()

    const urlInput = await screen.findByDisplayValue('http://ollama:11434')
    fireEvent.change(urlInput, { target: { value: 'http://other:8080' } })
    fireEvent.click(screen.getByText('Save connection'))

    await screen.findByRole('alert')
    expect(screen.getByDisplayValue('http://other:8080')).toBeTruthy()
  })

  it('clears the credential input after a successful key set and never shows the plaintext', async () => {
    putAdminCredentialMock.mockResolvedValue({
      identity: 'OpenAICompatible.ServerApiKey',
      configured: true,
      keyVersion: 1,
      updatedAt: '2026-09-17T10:05:00Z',
    })
    getAdminCredentialMock.mockResolvedValue({
      identity: 'OpenAICompatible.ServerApiKey',
      configured: true,
      keyVersion: 1,
      updatedAt: '2026-09-17T10:05:00Z',
    })
    renderAiSettings()

    const keyInput = (await screen.findByLabelText(/New key/i)) as HTMLInputElement
    await act(async () => {
      fireEvent.change(keyInput, { target: { value: 'sk-plaintext-secret' } })
    })
    fireEvent.click(screen.getByText('Set / replace'))

    await waitFor(() => expect(putAdminCredentialMock).toHaveBeenCalled())
    await waitFor(() => expect(keyInput.value).toBe(''))
    expect(screen.getByText('Configured')).toBeTruthy()
    expect(document.body.textContent).not.toContain('sk-plaintext-key')
  })

  it('publishes a new instruction version and shows the success state', async () => {
    putAiReportPolicyMock.mockResolvedValue({
      ...policyFixture,
      activeVersion: 2,
      text: 'Yeni yönerge',
      history: [
        ...policyFixture.history,
        { version: 2, text: 'Yeni yönerge', authorUserId: 'admin', publishedAt: '2026-09-17T11:00:00Z' },
      ],
    })
    renderAiSettings()

    const editor = (await screen.findByLabelText(/Instruction text/i)) as HTMLTextAreaElement
    fireEvent.change(editor, { target: { value: 'Yeni yönerge' } })
    fireEvent.click(screen.getByText('Publish new version'))

    await waitFor(() => expect(putAiReportPolicyMock).toHaveBeenCalled())
    const request = putAiReportPolicyMock.mock.calls[0][0]
    expect(request.expectedVersion).toBe(1)
    await screen.findByText(/Report instructions published|saved/i)
    expect(screen.getByText('Active version: 1')).toBeTruthy()
  })

  it('shows a conflict and retains the draft when another admin published first', async () => {
    putAiReportPolicyMock.mockRejectedValue(
      new ApiError(409, 'report_policy_conflict', 'The report policy was changed by another administrator.'),
    )
    renderAiSettings()

    const editor = (await screen.findByLabelText(/Instruction text/i)) as HTMLTextAreaElement
    fireEvent.change(editor, { target: { value: 'Çakışan taslak' } })
    fireEvent.click(screen.getByText('Publish new version'))

    await waitFor(() => expect(putAiReportPolicyMock).toHaveBeenCalled())
    expect(screen.getByRole("alert").textContent).toMatch(/newer version/i)
    expect(screen.getByDisplayValue('Çakışan taslak')).toBeTruthy()
  })
})
