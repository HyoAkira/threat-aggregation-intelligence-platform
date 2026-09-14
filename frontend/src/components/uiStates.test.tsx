import { renderToStaticMarkup } from 'react-dom/server'
import {
  EmptyState,
  ErrorState,
  ForbiddenState,
  LoadingState,
} from './uiStates'

describe('LoadingState', () => {
  it('is announced politely as a status', () => {
    const markup = renderToStaticMarkup(<LoadingState label="Searching…" />)
    expect(markup).toContain('role="status"')
    expect(markup).toContain('aria-live="polite"')
    expect(markup).toContain('Searching…')
  })
})

describe('EmptyState', () => {
  it('renders a title, optional message, and optional action', () => {
    const markup = renderToStaticMarkup(
      <EmptyState
        title="No indicators found"
        message="Nothing matches."
        action={<a href="/x">Clear</a>}
      />,
    )
    expect(markup).toContain('role="status"')
    expect(markup).toContain('No indicators found')
    expect(markup).toContain('Nothing matches.')
    expect(markup).toContain('Clear')
  })
})

describe('ErrorState', () => {
  it('is assertive and shows a retry control', () => {
    const markup = renderToStaticMarkup(
      <ErrorState
        title="Search failed"
        message="Unexpected failure."
        correlationId="corr-42"
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('role="alert"')
    expect(markup).toContain('Search failed')
    expect(markup).toContain('Unexpected failure.')
    expect(markup).toContain('corr-42')
    expect(markup).toContain('Retry')
  })

  it('omits the retry control when no retry is offered', () => {
    const markup = renderToStaticMarkup(<ErrorState title="Search failed" />)
    expect(markup).not.toContain('Retry')
  })
})

describe('ForbiddenState', () => {
  it('presents access denial without a retry affordance', () => {
    const markup = renderToStaticMarkup(<ForbiddenState />)
    expect(markup).toContain('role="alert"')
    expect(markup).toContain('Access denied')
    expect(markup).not.toContain('Retry')
  })
})
