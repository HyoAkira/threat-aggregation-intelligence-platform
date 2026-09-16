// Force a deterministic runtime timezone so local-time presentation is testable
// regardless of the developer machine's zone. Production uses the browser's zone.
process.env.TZ = 'UTC'

export default {
  preset: 'ts-jest',
  testEnvironment: 'node',
  testMatch: ['<rootDir>/src/**/*.test.{js,jsx,ts,tsx}'],
  setupFiles: ['<rootDir>/src/test/globals.ts'],
  transform: {
    '^.+\\.tsx?$': ['ts-jest', { tsconfig: '<rootDir>/tsconfig.jest.json' }],
  },
  moduleNameMapper: {
    '^(\\.{1,2}/.*)\\.js$': '$1',
    '\\.css\\?url$': '<rootDir>/src/test/stubCssModule.ts',
    '\\.css$': '<rootDir>/src/test/stubCssModule.ts',
    '^@tanstack/react-devtools$': '<rootDir>/src/test/stubDevtools.tsx',
  },
}
