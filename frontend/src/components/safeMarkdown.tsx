import type { ReactNode } from 'react'

/**
 * Minimal, dependency-free Markdown renderer for the AI report. It supports only the constructs
 * the report policy produces (## headings, - lists, paragraphs, **bold**) and it never creates
 * raw HTML or link elements: everything is rendered as React text nodes, so a malicious fixture
 * cannot inject an executable link or an HTML node. Raw HTML tags and link targets are stripped
 * to their visible text.
 */

const headingPattern = /^##\s+(.*)$/
const listItemPattern = /^-\s+(.*)$/
const boldPattern = /\*\*([^*]+)\*\*/g
const linkPattern = /\[([^\]]*)\]\([^)]*\)/g
const htmlTagPattern = /<[^>]*>/g

function sanitizeInline(text: string): ReactNode[] {
  let withoutLinks = text.replace(linkPattern, '$1')
  withoutLinks = withoutLinks.replace(htmlTagPattern, '')
  const segments: ReactNode[] = []
  let lastIndex = 0
  for (const match of withoutLinks.matchAll(boldPattern)) {
    const start = match.index ?? 0
    if (start > lastIndex) {
      segments.push(withoutLinks.slice(lastIndex, start))
    }
    segments.push(
      <strong key={`b-${start}`}>{match[1]}</strong>,
    )
    lastIndex = start + match[0].length
  }
  segments.push(withoutLinks.slice(lastIndex))
  return segments
}

export function renderSafeMarkdown(markdown: string): ReactNode[] {
  const nodes: ReactNode[] = []
  const lines = markdown.replace(/\r\n/g, '\n').split('\n')
  let listItems: ReactNode[] = []
  let paragraph: string[] = []
  let key = 0

  const flushList = () => {
    if (listItems.length > 0) {
      nodes.push(
        <ul key={`ul-${key++}`} className="list-disc pl-6">
          {listItems.map((item, index) => (
            <li key={`li-${key}-${index}`} className="leading-6">
              {item}
            </li>
          ))}
        </ul>,
      )
      listItems = []
    }
  }

  const flushParagraph = () => {
    if (paragraph.length > 0) {
      nodes.push(
        <p key={`p-${key++}`} className="break-words">
          {sanitizeInline(paragraph.join(' '))}
        </p>,
      )
      paragraph = []
    }
  }

  for (const line of lines) {
    const heading = headingPattern.exec(line)
    if (heading) {
      flushList()
      flushParagraph()
      nodes.push(
        <h3 key={`h-${key++}`} className="pt-2 font-semibold text-on-surface">
          {sanitizeInline(heading[1])}
        </h3>,
      )
      continue
    }

    const item = listItemPattern.exec(line)
    if (item) {
      flushParagraph()
      listItems.push(sanitizeInline(item[1]))
      continue
    }

    if (line.trim().length === 0) {
      flushList()
      flushParagraph()
      continue
    }

    paragraph.push(line.trim())
  }

  flushList()
  flushParagraph()
  return nodes
}
