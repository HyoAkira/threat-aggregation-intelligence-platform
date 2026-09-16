import { expect, test } from '@playwright/test'
import { createReadStream, existsSync, statSync } from 'node:fs'
import { createServer } from 'node:http'
import type { Server } from 'node:http'
import { extname, join, normalize, sep } from 'node:path'

/**
 * Test double for the static-host contract that the E14 frontend container
 * implements in nginx. It is not production configuration; it locks the
 * interface (same-origin `/api/*` proxying, client-route fallback, and 404 for
 * missing asset-like paths) that the static build depends on so E14 cannot
 * change it silently.
 */
const contentTypeByExtension: Record<string, string> = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
}

function createContractHost(root: string): Server {
  const entry = join(root, 'index.html')
  return createServer((req, res) => {
    const url = new URL(req.url ?? '/', 'http://localhost')
    const pathname = decodeURIComponent(url.pathname)

    // The API is proxied upstream on the same origin; it is never answered by
    // the SPA fallback.
    if (pathname === '/api' || pathname.startsWith('/api/')) {
      res.statusCode = 502
      res.end('Upstream API unavailable in this harness')
      return
    }

    const candidate = normalize(join(root, pathname))
    const insideRoot = candidate === root || candidate.startsWith(root + sep)
    if (insideRoot && existsSync(candidate) && statSync(candidate).isFile()) {
      res.statusCode = 200
      res.setHeader(
        'content-type',
        contentTypeByExtension[extname(candidate)] ??
          'application/octet-stream',
      )
      createReadStream(candidate).pipe(res)
      return
    }

    // A missing path that looks like a file must 404 rather than return the app.
    if (extname(pathname) !== '') {
      res.statusCode = 404
      res.end('Not found')
      return
    }

    // Non-file client routes receive the application entry document.
    res.statusCode = 200
    res.setHeader('content-type', 'text/html; charset=utf-8')
    createReadStream(entry).pipe(res)
  })
}

const distDir = join(process.cwd(), 'dist')

test('the production build is a static artifact with no Start server bundle', () => {
  expect(existsSync(join(distDir, 'index.html'))).toBe(true)
  expect(existsSync(join(distDir, 'assets'))).toBe(true)
  // TanStack Start previously emitted dist/server/server.js; the SPA must not.
  expect(existsSync(join(distDir, 'server'))).toBe(false)
})

test('the static host contract serves client routes, proxies /api, and 404s missing assets', async () => {
  const server = createContractHost(distDir)
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve))
  const address = server.address()
  const port = typeof address === 'object' && address ? address.port : 0
  const base = `http://127.0.0.1:${port}`

  try {
    for (const route of ['/', '/browse', '/admin/providers', '/iocs/7']) {
      const response = await fetch(`${base}${route}`)
      expect(response.status, `GET ${route}`).toBe(200)
      expect(await response.text(), `GET ${route}`).toContain('id="root"')
    }

    const api = await fetch(`${base}/api/iocs`)
    expect(api.status).toBe(502)
    expect(await api.text()).not.toContain('id="root"')

    const missingAsset = await fetch(`${base}/assets/does-not-exist.js`)
    expect(missingAsset.status).toBe(404)
  } finally {
    await new Promise<void>((resolve, reject) =>
      server.close((error) => (error ? reject(error) : resolve())),
    )
  }
})
