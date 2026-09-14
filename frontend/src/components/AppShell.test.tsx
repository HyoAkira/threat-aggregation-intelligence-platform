import { AppShell, ShellAuthArea, ShellNav } from './AppShell'
import { renderAppRoute, renderWithRouterContext } from '../test/renderApp'

const renderShell = () => renderWithRouterContext(<AppShell>Content</AppShell>)

describe('ShellNav navigation destinations', () => {
  it('exposes Search and Browse for a User session', async () => {
    const markup = await renderWithRouterContext(
      <ShellNav session={{ username: 'analyst', role: 'User' }} />,
    )
    expect(markup).toContain('Search')
    expect(markup).toContain('Browse')
    expect(markup).not.toContain('>Admin<')
    expect(markup).toContain('aria-label="Primary"')
  })

  it('additionally exposes Admin destinations for an Admin session', async () => {
    const markup = await renderWithRouterContext(
      <ShellNav session={{ username: 'root', role: 'Admin' }} />,
    )
    expect(markup).toContain('Search')
    expect(markup).toContain('Browse')
    expect(markup).toContain('>Admin<')
  })

  it('keeps User navigation when signed out', async () => {
    const markup = await renderWithRouterContext(<ShellNav session={null} />)
    expect(markup).toContain('Search')
    expect(markup).toContain('Browse')
    expect(markup).not.toContain('>Admin<')
  })
})

describe('ShellAuthArea', () => {
  it('offers a sign-in link when signed out', async () => {
    const markup = await renderWithRouterContext(
      <ShellAuthArea session={null} />,
    )
    expect(markup).toContain('Sign in')
  })

  it('shows the signed-in identity with role and a sign-out control', async () => {
    const markup = await renderWithRouterContext(
      <ShellAuthArea session={{ username: 'analyst', role: 'User' }} />,
    )
    expect(markup).toContain('Signed in as')
    expect(markup).toContain('analyst')
    expect(markup).toMatch(/\(<!-- -->User<!-- -->\)/)
    expect(markup).toContain('Sign out')

    const adminMarkup = await renderWithRouterContext(
      <ShellAuthArea session={{ username: 'root', role: 'Admin' }} />,
    )
    expect(adminMarkup).toMatch(/Administrator/)
  })
})

describe('AppShell structure (keyboard, landmarks, responsiveness)', () => {
  it('renders a skip link to the main landmark', async () => {
    const markup = await renderShell()
    expect(markup).toContain('Skip to main content')
    expect(markup).toContain('href="#main-content"')
    expect(markup).toContain('id="main-content"')
  })

  it('uses semantic landmarks: header, primary nav, main, footer', async () => {
    const markup = await renderShell()
    expect(markup).toContain('<header')
    expect(markup).toContain('<nav aria-label="Primary"')
    expect(markup).toContain('<main id="main-content"')
    expect(markup).toContain('<footer')
  })

  it('keeps interactive controls keyboard-visible via focus-visible styles', async () => {
    const markup = await renderShell()
    expect(markup).toContain('focus-visible:outline-2')
    expect(markup).toContain('focus-visible:outline-offset-2')
  })

  it('is responsive at mobile and desktop widths', async () => {
    const markup = await renderShell()
    // Wrapping layout works at narrow widths; padding widens from the md: breakpoint.
    expect(markup).toContain('flex-wrap')
    expect(markup).toContain('md:px-6')
    expect(markup).toContain('max-w-6xl')
  })

  it('does not duplicate the main landmark per page render', async () => {
    const markup = await renderAppRoute('/search')
    expect(markup.match(/<main /g)?.length).toBe(1)
    expect(markup).toContain('<h1')
  })
})
