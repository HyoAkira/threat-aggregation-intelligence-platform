import { renderToStaticMarkup } from 'react-dom/server'
import { Pagination } from './Pagination'

function render(page: number, pageSize: number, totalCount: number) {
  return renderToStaticMarkup(
    <Pagination
      page={page}
      pageSize={pageSize}
      totalCount={totalCount}
      onPage={() => {}}
    />,
  )
}

const disabledAttributeCount = (markup: string) =>
  (markup.match(/disabled=""/g) ?? []).length

describe('Pagination', () => {
  it('announces the page summary politely', () => {
    const markup = render(2, 20, 45)
    expect(markup).toContain('aria-live="polite"')
    expect(markup).toContain('Page 2 of 3 · 45 total')
  })

  it('keeps both controls enabled mid-list', () => {
    expect(disabledAttributeCount(render(2, 20, 45))).toBe(0)
  })

  it('disables previous on the first page and next on the last page', () => {
    expect(disabledAttributeCount(render(1, 20, 45))).toBe(1)
    expect(disabledAttributeCount(render(3, 20, 45))).toBe(1)
  })

  it('an empty result disables both controls and shows one page', () => {
    const markup = render(1, 20, 0)
    expect(disabledAttributeCount(markup)).toBe(2)
    expect(markup).toContain('Page 1 of 1 · 0 total')
  })

  it('labels the pagination landmark', () => {
    expect(render(1, 20, 0)).toContain('<nav aria-label="Pagination"')
  })

  it('supports a custom page label for relations', () => {
    const markup = renderToStaticMarkup(
      <Pagination
        page={2}
        pageSize={20}
        totalCount={45}
        onPage={() => {}}
        pageLabel="Relations page"
      />,
    )
    expect(markup).toContain('Relations page 2 of 3 · 45 total')
  })
})
