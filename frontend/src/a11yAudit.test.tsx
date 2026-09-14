import { queryClient } from './lib/queryClient'
import { browseKey, iocDetailKey, iocRelationsKey } from './lib/queryKeys'
import { defaultBrowseParams } from './lib/browseParams'
import { renderAppRoute, resetClientState } from './test/renderApp'
import { ipv4Group, makePaged, urlGroup } from './test/fixtures'

/**
 * Basic accessibility audit over the server-rendered application markup:
 * one h1 per page, labelled inputs, named interactive controls, a skip link,
 * landmark structure, sensible tab order, and visible keyboard focus styles.
 */
function auditPage(markup: string, name: string) {
  const issues: string[] = []

  const h1Count = (markup.match(/<h1[\s>]/g) ?? []).length
  if (h1Count !== 1)
    issues.push(`${name}: expected exactly one h1, found ${h1Count}`)

  if (!markup.includes('<html lang="en"'))
    issues.push(`${name}: html lang attribute missing`)

  // Every input needs a programmatically associated label.
  const inputTags = markup.match(/<input\b[^>]*>/g) ?? []
  for (const inputTag of inputTags) {
    const idMatch = inputTag.match(/\bid="([^"]+)"/)
    const hasAriaLabel = /\baria-label=/.test(inputTag)
    if (!idMatch && !hasAriaLabel) {
      issues.push(`${name}: input without id or aria-label: ${inputTag}`)
      continue
    }
    if (idMatch) {
      const labelPattern = new RegExp(`<label[^>]*for="${idMatch[1]}"[^>]*>`)
      if (!labelPattern.test(markup)) {
        issues.push(`${name}: no <label for="${idMatch[1]}"> for input`)
      }
    }
  }

  // Every button and link needs an accessible name (text content or aria-label).
  const buttonTags =
    markup.match(/<button\b[^>]*>((?:(?!<\/button>).)*)<\/button>/gs) ?? []
  for (const buttonTag of buttonTags) {
    const self = buttonTag.match(/^<button\b[^>]*>/)?.[0] ?? ''
    const hasAriaLabel = /\baria-label=/.test(self)
    const text = buttonTag.replace(/<[^>]+>/g, '').trim()
    if (!hasAriaLabel && text === '') {
      issues.push(`${name}: button without accessible name: ${self}`)
    }
  }
  const anchorTags = markup.match(/<a\b[^>]*>((?:(?!<\/a>).)*)<\/a>/gs) ?? []
  for (const anchorTag of anchorTags) {
    const self = anchorTag.match(/^<a\b[^>]*>/)?.[0] ?? ''
    const hasAriaLabel = /\baria-label=/.test(self)
    const text = anchorTag.replace(/<[^>]+>/g, '').trim()
    if (!hasAriaLabel && text === '') {
      issues.push(`${name}: link without accessible name: ${self}`)
    }
  }

  // Skip link and landmarks.
  if (!markup.includes('href="#main-content"'))
    issues.push(`${name}: skip link missing`)
  if (!markup.includes('<nav aria-label="Primary"'))
    issues.push(`${name}: primary nav missing`)
  if (!/<main id="main-content"/.test(markup))
    issues.push(`${name}: main landmark missing`)

  // No positive tabIndex (breaks keyboard order); -1 on the main target is fine.
  const positiveTabIndex =
    markup.match(/tabIndex="([1-9]\d*)"/g) ??
    markup.match(/tabindex="([1-9]\d*)"/g) ??
    []
  if (positiveTabIndex.length > 0) {
    issues.push(`${name}: positive tabindex present`)
  }

  // Keyboard focus must be visible on interactive controls.
  if (!markup.includes('focus-visible:outline-2')) {
    issues.push(`${name}: focus-visible styles missing`)
  }

  return issues
}

describe('basic accessibility audit', () => {
  beforeEach(() => resetClientState())
  afterEach(() => resetClientState())

  it('home page with results passes', async () => {
    queryClient.setQueryData(
      browseKey({ ...defaultBrowseParams, pageSize: 10 }),
      makePaged([ipv4Group, urlGroup], 1, 10, 45),
    )
    const issues = auditPage(await renderAppRoute('/'), 'home')
    expect(issues).toEqual([])
  })

  it('search page passes', async () => {
    const issues = auditPage(await renderAppRoute('/search'), 'search')
    expect(issues).toEqual([])
  })

  it('login page passes', async () => {
    const issues = auditPage(await renderAppRoute('/login'), 'login')
    expect(issues).toEqual([])
  })

  it('browse page with results passes', async () => {
    queryClient.setQueryData(
      browseKey(defaultBrowseParams),
      makePaged([urlGroup], 1, 20, 45),
    )
    const issues = auditPage(await renderAppRoute('/browse'), 'browse')
    expect(issues).toEqual([])
  })

  it('detail page with an active relation view passes', async () => {
    queryClient.setQueryData(iocDetailKey(urlGroup.id), urlGroup)
    queryClient.setQueryData(
      iocRelationsKey(urlGroup.id, 'urlHost', 1),
      makePaged([urlGroup], 1, 20, 45),
    )
    const issues = auditPage(
      await renderAppRoute('/iocs/7?relation=urlHost'),
      'detail',
    )
    expect(issues).toEqual([])
  })
})
