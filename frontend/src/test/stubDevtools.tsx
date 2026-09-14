// Jest stubs for the client-only TanStack devtools panels. Their packages are
// ESM-only (no `require` export condition), and they never render meaningful
// content in the Node test environment.
export function TanStackDevtools() {
  return null
}
export function TanStackRouterDevtoolsPanel() {
  return null
}
