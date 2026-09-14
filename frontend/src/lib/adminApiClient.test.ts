import { putAdminSettings } from './apiClient'
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
})
