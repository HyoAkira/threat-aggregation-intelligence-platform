import { TextDecoder, TextEncoder } from 'node:util'

// jsdom does not provide the encoding globals that some isomorphic deps expect;
// the Node environment already has them, so this only fills the gap.
if (!('TextEncoder' in globalThis)) {
  Object.defineProperty(globalThis, 'TextEncoder', {
    value: TextEncoder,
    configurable: true,
  })
}
if (!('TextDecoder' in globalThis)) {
  Object.defineProperty(globalThis, 'TextDecoder', {
    value: TextDecoder,
    configurable: true,
  })
}
