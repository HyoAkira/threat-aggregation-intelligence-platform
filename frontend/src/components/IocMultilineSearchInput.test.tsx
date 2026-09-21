/**
 * @jest-environment jsdom
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { getIocSuggestions } from '../lib/apiClient'
import { IocMultilineSearchInput } from './IocMultilineSearchInput'

jest.mock('../lib/apiClient', () => {
  const actual = jest.requireActual('../lib/apiClient')
  return { ...actual, getIocSuggestions: jest.fn() }
})

const getIocSuggestionsMock = getIocSuggestions as jest.MockedFunction<
  typeof getIocSuggestions
>

function renderEditor(value: string, onChange = jest.fn()) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  render(
    <QueryClientProvider client={client}>
      <IocMultilineSearchInput
        id="q"
        value={value}
        onChange={onChange}
        className="input"
      />
    </QueryClientProvider>,
  )
  return {
    onChange,
    textarea: screen.getByRole<HTMLTextAreaElement>('combobox'),
  }
}

describe('IocMultilineSearchInput', () => {
  beforeEach(() => getIocSuggestionsMock.mockReset())

  it('renders a real textarea that retains arbitrary free-form text', () => {
    const { textarea } = renderEditor('not an ioc at all!')
    expect(textarea.tagName).toBe('TEXTAREA')
    expect(textarea.value).toBe('not an ioc at all!')
  })

  it('replaces only the active line on selection', async () => {
    getIocSuggestionsMock.mockResolvedValue([
      { value: 'foobar.example', iocType: 'domain' },
    ])
    const { onChange, textarea } = renderEditor('1.2.3.4\nfoo\nbar')

    // Caret sits inside the second line, whose prefix is "foo".
    textarea.setSelectionRange(9, 9)
    fireEvent.select(textarea)

    await waitFor(() =>
      expect(screen.getByRole('option').textContent).toContain(
        'foobar.example',
      ),
    )
    fireEvent.mouseDown(screen.getByRole('option'))

    expect(onChange).toHaveBeenCalledWith('1.2.3.4\nfoobar.example\nbar')
  })

  it('does not query suggestions for a blank active line and keeps entry unblocked', async () => {
    const { onChange, textarea } = renderEditor('1.2.3.4\n\n')
    textarea.setSelectionRange(9, 9)
    fireEvent.select(textarea)

    expect(getIocSuggestionsMock).not.toHaveBeenCalled()

    fireEvent.change(textarea, { target: { value: '1.2.3.4\n\n9.9.9.9' } })
    expect(onChange).toHaveBeenCalledWith('1.2.3.4\n\n9.9.9.9')
  })

  it('keeps free-form entry working when no suggestions are returned', async () => {
    getIocSuggestionsMock.mockResolvedValue([])
    const { onChange, textarea } = renderEditor('evil.example')

    fireEvent.change(textarea, { target: { value: 'evil' } })

    await waitFor(() => expect(getIocSuggestionsMock).toHaveBeenCalled())
    expect(screen.queryByRole('option')).toBeNull()

    fireEvent.change(textarea, { target: { value: 'evil.example.io' } })
    expect(onChange).toHaveBeenLastCalledWith('evil.example.io')
  })
})
