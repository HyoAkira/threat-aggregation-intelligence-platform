import { putAdminCredential, putAdminSettings, putAiSettings } from './apiClient'
import { useAuthStore } from './authStore'
import { createSessionStorageStub } from '../test/fixtures'

describe('admin API client', () => {
  beforeEach(() => {
    ;(globalThis as { sessionStorage?: Storage }).sessionStorage =
      createSessionStorageStub() as Storage
    useAuthStore.setState({ session: null })
  })

  it('accepts a successful settings 204 without trying to parse JSON', async () => {
    const json = jest.fn()
    globalThis.fetch = jest.fn().mockResolvedValue({
      ok: true,
      status: 204,
      json,
    })

    await expect(
      putAdminSettings({
        logLevel: 'Information',
        providers: [{ id: 1, isEnabled: true, intervalMinutes: 5 }],
      }),
    ).resolves.toBeUndefined()
    expect(json).not.toHaveBeenCalled()
  })

  it('keeps AI connection values, instructions, and keys out of the provider/log settings PUT', async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      status: 204,
      json: jest.fn(),
    })
    globalThis.fetch = fetchMock

    await putAdminSettings({
      logLevel: 'Information',
      providers: [{ id: 1, isEnabled: true, intervalMinutes: 5 }],
    })

    const [, init] = fetchMock.mock.calls[0]
    const body = JSON.parse(init.body)
    expect(body).toEqual({
      logLevel: 'Information',
      providers: [{ id: 1, isEnabled: true, intervalMinutes: 5 }],
    })
    const serialized = JSON.stringify(body)
    expect(serialized).not.toContain('baseUrl')
    expect(serialized).not.toContain('modelId')
    expect(serialized).not.toContain('apiKey')
    expect(serialized).not.toContain('reportPolicy')
    expect(init.url ?? '/api/admin/settings').toContain('/api/admin/settings')
  })

  it('sends the AI settings PUT to its own endpoint without the key value', async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: jest
        .fn()
        .mockResolvedValue({ baseUrl: 'http://ollama:11434', modelId: 'llama3.1', enabled: true, apiKeyConfigured: true }),
    })
    globalThis.fetch = fetchMock

    const result = await putAiSettings({
      baseUrl: 'http://ollama:11434',
      modelId: 'llama3.1',
      enabled: true,
    })

    const [path, init] = fetchMock.mock.calls[0]
    expect(path).toBe('/api/admin/ai/settings')
    expect(init.url).toBeUndefined()
    expect(init.headers.Authorization).toBeUndefined()
    const body = JSON.parse(init.body)
    expect(body).toEqual({
      baseUrl: 'http://ollama:11434',
      modelId: 'llama3.1',
      enabled: true,
    })
    expect(JSON.stringify(body)).not.toContain('apiKey')
    expect(result.apiKeyConfigured).toBe(true)
  })

  it('never places the credential value in the URL or query string', async () => {
    const secret = 'sk-test-plaintext-key-1234567890'
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: jest.fn().mockResolvedValue({ identity: 'OpenAICompatible.ServerApiKey', configured: true }),
    })
    globalThis.fetch = fetchMock

    await putAdminCredential('OpenAICompatible.ServerApiKey', secret)

    const [path, init] = fetchMock.mock.calls[0]
    expect(String(path)).not.toContain(secret)
    expect(init.url).toBeUndefined()
    expect(JSON.parse(init.body).value).toBe(secret)
  })
})
