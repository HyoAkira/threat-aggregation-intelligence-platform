import { renderToStaticMarkup } from 'react-dom/server'
import { IocSourceCard } from './IocSourceCard'
import { phishTankSource, threatFoxSource, urlGroup } from '../test/fixtures'

const asSource = (value: unknown) =>
  value as Parameters<typeof IocSourceCard>[0]['source']

describe('IocSourceCard', () => {
  it('keeps provider attribution and record identity on the card', () => {
    const markup = renderToStaticMarkup(
      <IocSourceCard source={asSource(threatFoxSource)} />,
    )

    expect(markup).toContain('Source: ThreatFox')
    expect(markup).toContain('record tf-17')
    expect(markup).toContain('Port:')
    expect(markup).toContain('443')
  })

  it('renders stored false values as understandable No answers', () => {
    const markup = renderToStaticMarkup(
      <IocSourceCard source={asSource(phishTankSource)} />,
    )

    expect(markup).toContain('Verified:')
    expect(markup).toContain('>No<')
    expect(markup).toContain('Online:')
    expect(markup).toContain('>No<')
  })

  it('renders a stored zero confidence level and an empty target string', () => {
    const markup = renderToStaticMarkup(
      <IocSourceCard source={asSource(threatFoxSource)} />,
    )

    expect(markup).toContain('Confidence level:')
    expect(markup).toContain('>0<')
  })

  it('renders an empty target and empty PhishTank details explicitly', () => {
    const markup = renderToStaticMarkup(
      <IocSourceCard source={asSource(phishTankSource)} />,
    )

    expect(markup).toContain('Target:')
    expect(markup).toContain('(empty)')
    expect(markup).toContain('Details:')
    expect(markup).toContain('(no detail entries)')
  })

  it('produces no label for null or absent provider-inapplicable fields', () => {
    const threatFoxMarkup = renderToStaticMarkup(
      <IocSourceCard source={asSource(threatFoxSource)} />,
    )
    expect(threatFoxMarkup).not.toContain('Verified:')
    expect(threatFoxMarkup).not.toContain('Target:')
    expect(threatFoxMarkup).not.toContain('Details:')

    const phishTankMarkup = renderToStaticMarkup(
      <IocSourceCard source={asSource(phishTankSource)} />,
    )
    expect(phishTankMarkup).not.toContain('Malware')
    expect(phishTankMarkup).not.toContain('Threat type:')
    expect(phishTankMarkup).not.toContain('Port:')
  })

  it('renders the false-positive state per source', () => {
    const fresh = renderToStaticMarkup(
      <IocSourceCard source={asSource(threatFoxSource)} />,
    )
    expect(fresh).toContain('Not marked false positive')

    const stale = renderToStaticMarkup(
      <IocSourceCard source={asSource(phishTankSource)} />,
    )
    expect(stale).toContain('Marked false positive')
  })

  it('keeps each source’s tags on its own card', () => {
    const threatFoxMarkup = renderToStaticMarkup(
      <IocSourceCard source={asSource(threatFoxSource)} />,
    )
    expect(threatFoxMarkup).toContain('botnet')
    expect(threatFoxMarkup).toContain('credential_stealer')
    expect(threatFoxMarkup).not.toContain('phishing')

    const phishTankMarkup = renderToStaticMarkup(
      <IocSourceCard source={asSource(phishTankSource)} />,
    )
    expect(phishTankMarkup).toContain('phishing')
    expect(phishTankMarkup).not.toContain('credential_stealer')
  })

  it('labels the external details link and hardens the new tab', () => {
    const source = {
      ...phishTankSource,
      providerDetailsLink: 'https://phishtank.example/record/9',
    }
    const markup = renderToStaticMarkup(
      <IocSourceCard source={asSource(source)} />,
    )

    expect(markup).toContain('href="https://phishtank.example/record/9"')
    expect(markup).toContain('target="_blank"')
    expect(markup).toContain('rel="noopener noreferrer"')
    expect(markup).toContain('external link')
  })

  it('renders PhishTank details JSON when present', () => {
    const source = {
      ...phishTankSource,
      phishTankDetails: { online: true, submitted: '2026-02-01' },
    }
    const markup = renderToStaticMarkup(
      <IocSourceCard source={asSource(source)} />,
    )

    expect(markup).toContain('submitted')
    expect(markup).not.toContain('(no detail entries)')
  })

  it('renders both providers as separate labelled entries in a group', () => {
    const sources = urlGroup.sources
    const threatFoxMarkup = renderToStaticMarkup(
      <IocSourceCard source={sources[0]} />,
    )
    const phishTankMarkup = renderToStaticMarkup(
      <IocSourceCard source={sources[1]} />,
    )
    expect(threatFoxMarkup).toContain('Source: ThreatFox')
    expect(phishTankMarkup).toContain('Source: PhishTank')
  })
})
